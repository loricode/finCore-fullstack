using System.Text.RegularExpressions;
using FinCore.Domain.Common;
using FinCore.Domain.Operations;
using FluentValidation;

namespace FinCore.Application.Operations;

/// <summary>
/// Validación de formato de las operaciones (RN-01 a RN-04): devuelve 400 con el error de cada campo.
/// El dominio vuelve a proteger las mismas invariantes; aquí solo se dan mensajes claros por campo.
/// </summary>
internal static partial class OperationRules
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_/.-]{3,39}$")]
    public static partial Regex Reference();

    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).WithMessage("El valor debe ser mayor que cero.")
            .LessThanOrEqualTo(TransactionRules.MaxAmount).WithMessage("El valor no puede superar 5.000.000.000.")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("El valor admite máximo 2 decimales.");

    public static IRuleBuilderOptions<T, DateOnly> ValidOperationDate<T>(this IRuleBuilder<T, DateOnly> rule, TimeProvider clock) =>
        rule.Must(d =>
            {
                var today = BusinessCalendar.Today(clock.GetUtcNow().UtcDateTime);
                return d <= today && d >= today.AddDays(-TransactionRules.MaxBackdatedDays);
            })
            .WithMessage($"La fecha no puede ser futura ni anterior a {TransactionRules.MaxBackdatedDays} días.");

    public static IRuleBuilderOptions<T, string?> Optional<T>(this IRuleBuilder<T, string?> rule, int max, string field) =>
        rule.MaximumLength(max).WithMessage($"{field} no puede superar {max} caracteres.");
}

internal sealed class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    public RegisterTransactionCommandValidator(TimeProvider clock)
    {
        RuleFor(x => x.AccountId).GreaterThan(0).WithMessage("Selecciona la cuenta.");
        RuleFor(x => x.Type).Must(t => t is not null && TransactionCodes.RegistrableTypes.ContainsKey(t.ToUpperInvariant()))
            .WithMessage("El tipo debe ser ING, EGR, TRF o PAG.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("La referencia es obligatoria.")
            .Must(r => OperationRules.Reference().IsMatch(r?.Trim() ?? ""))
            .WithMessage("La referencia debe tener de 4 a 40 caracteres: letras, números, guion, punto, barra o guion bajo.");
        RuleFor(x => x.Amount).ValidAmount();
        RuleFor(x => x.OperationDate).ValidOperationDate(clock);
        RuleFor(x => x.Description).Optional(250, "La descripción");
        RuleFor(x => x.CounterpartyName).Optional(150, "La contraparte");
        RuleFor(x => x.CounterpartyAccount).Optional(40, "La cuenta de la contraparte");
        RuleFor(x => x.IdempotencyKey).Optional(64, "La Idempotency-Key");

        When(x => x.Type is not null && TransactionCodes.RegistrableTypes.TryGetValue(x.Type.ToUpperInvariant(), out var t)
                  && t.RequiresCounterpartyName(), () =>
            RuleFor(x => x.CounterpartyName).NotEmpty().WithMessage("Transferencias y pagos requieren la contraparte."));
        When(x => string.Equals(x.Type, "TRF", StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(x => x.CounterpartyAccount).NotEmpty().WithMessage("Una transferencia requiere la cuenta destino."));
    }
}

internal sealed class UpdateTransactionCommandValidator : AbstractValidator<UpdateTransactionCommand>
{
    public UpdateTransactionCommandValidator(TimeProvider clock)
    {
        RuleFor(x => x.Amount).ValidAmount();
        RuleFor(x => x.OperationDate).ValidOperationDate(clock);
        RuleFor(x => x.Description).Optional(250, "La descripción");
        RuleFor(x => x.CounterpartyName).Optional(150, "La contraparte");
        RuleFor(x => x.CounterpartyAccount).Optional(40, "La cuenta de la contraparte");
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("Falta la versión del registro (rowVersion).");
    }
}

internal sealed class ReasonValidator : AbstractValidator<ChangeTransactionStatusCommand>
{
    public ReasonValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(200).WithMessage("El motivo no puede superar 200 caracteres.");
    }
}

internal sealed class TransactionFilterValidator : AbstractValidator<TransactionFilter>
{
    public TransactionFilterValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página empieza en 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
        RuleFor(x => x.Status).Must(s => s is null || TransactionCodes.Statuses.Contains(s.ToUpperInvariant()))
            .WithMessage("Estado desconocido.");
        RuleFor(x => x.Type).Must(t => t is null || TransactionCodes.AllTypes.Contains(t.ToUpperInvariant()))
            .WithMessage("Tipo desconocido.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithName("to").WithMessage("La fecha final debe ser mayor o igual a la inicial.");
        RuleFor(x => x.Search).MaximumLength(40);
    }
}
