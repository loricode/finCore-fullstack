using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Integration;
using FinCore.Domain.Operations;
using FluentValidation;

namespace FinCore.Application.Integration;

internal sealed partial class ReceiveBankEventCommandValidator : AbstractValidator<ReceiveBankEventCommand>
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,49}$")]
    private static partial Regex ProviderName();

    public ReceiveBankEventCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().Must(p => ProviderName().IsMatch(p ?? ""))
            .WithMessage("El proveedor debe tener de 2 a 50 caracteres: minúsculas, números o guion.");
        RuleFor(x => x.ExternalEventId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EventType).Equal("movement.created").WithMessage("Solo se admite el evento movement.created.");
        RuleFor(x => x.Movement).NotNull().WithMessage("Falta el movimiento.");
        When(x => x.Movement is not null, () =>
        {
            RuleFor(x => x.Movement!.AccountNumber).NotEmpty().MaximumLength(40).OverridePropertyName("movement.accountNumber");
            RuleFor(x => x.Movement!.MovementId).NotEmpty().MaximumLength(60).OverridePropertyName("movement.movementId");
            RuleFor(x => x.Movement!.Reference).MaximumLength(40).OverridePropertyName("movement.reference");
            RuleFor(x => x.Movement!.Description).MaximumLength(250).OverridePropertyName("movement.description");
            RuleFor(x => x.Movement!.Amount)
                .NotEqual(0).Must(a => Math.Abs(a) <= TransactionRules.MaxAmount && decimal.Round(a, 2) == a)
                .WithMessage("El valor debe ser distinto de cero, con máximo 2 decimales.").OverridePropertyName("movement.amount");
        });
    }
}

/// <summary>
/// Procesa un evento: busca la cuenta y agrega el movimiento al extracto WEBHOOK del día.
/// Lo usan la API (al recibir) y el Worker (para eventos que quedaron sin procesar por una falla transitoria).
/// </summary>
public sealed class BankEventProcessor(
    ICurrentUser currentUser,
    IAccountRepository accounts,
    IBankMovementStore movements,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task ProcessAsync(InboundEvent inboundEvent, BankEventMovement movement, CancellationToken ct)
    {
        inboundEvent.StartAttempt();
        await unitOfWork.SaveChangesAsync(ct); // el intento queda contado aunque lo siguiente falle

        var now = clock.GetUtcNow().UtcDateTime;
        var account = await accounts.GetByBankAccountNumberAsync(currentUser.CompanyId ?? 0, movement.AccountNumber.Trim(), ct);
        if (account is null || !account.IsActive)
        {
            inboundEvent.MarkFailed($"La cuenta bancaria {Mask(movement.AccountNumber)} no existe o está inactiva.", now);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var detailId = await movements.AddWebhookMovementAsync(account, movement, ct);
            await unitOfWork.SaveChangesAsync(ct);
            if (detailId is null) inboundEvent.MarkIgnored("El movimiento ya estaba cargado (llegó por otro canal).", now);
            else inboundEvent.MarkProcessed(detailId.Value, now);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    /// <summary>Los errores guardados no exponen el número de cuenta completo.</summary>
    internal static string Mask(string accountNumber) =>
        accountNumber.Length <= 4 ? accountNumber : "***" + accountNumber[^4..];
}

/// <summary>
/// POST /integrations/bank-events · Recibe un evento del adaptador Node.js (spec integracion-webhooks).
/// - Mismo (proveedor, id) y mismo contenido → devuelve el resultado original (Duplicate = true).
/// - Mismo id con otro contenido → 409.
/// - Error de negocio (cuenta inexistente) → evento FAILED; la API responde 422 y Node no reintenta.
/// </summary>
public sealed class ReceiveBankEventHandler(
    IValidator<ReceiveBankEventCommand> validator,
    IInboundEventRepository events,
    BankEventProcessor processor,
    IUnitOfWork unitOfWork,
    IRequestContext request,
    TimeProvider clock)
{
    public async Task<Result<BankEventResult>> HandleAsync(ReceiveBankEventCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(command.RawPayload)));

        var existing = await events.GetAsync(command.Provider, command.ExternalEventId, ct);
        if (existing is not null)
        {
            if (existing.PayloadHash != hash)
                return Error.Conflict("integration.event_conflict", "Ya se recibió un evento con ese id y otro contenido.");
            if (!existing.CanBeProcessed)
                return ToResult(existing, duplicate: true);
            await processor.ProcessAsync(existing, command.Movement!, ct); // quedó a medias: se completa ahora
            return ToResult(existing, duplicate: true);
        }

        var inboundEvent = InboundEvent.Receive(command.Provider, command.ExternalEventId, command.EventType, command.RawPayload,
            hash, request.CorrelationId, clock.GetUtcNow().UtcDateTime);
        events.Add(inboundEvent);
        await unitOfWork.SaveChangesAsync(ct); // el evento queda registrado antes de procesarlo

        await processor.ProcessAsync(inboundEvent, command.Movement!, ct);
        return ToResult(inboundEvent, duplicate: false);
    }

    internal static BankEventResult ToResult(InboundEvent e, bool duplicate) =>
        new(e.Id, e.Status.ToString().ToUpperInvariant(), e.BankStatementDetailId, duplicate, e.LastError);
}
