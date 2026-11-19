using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Domain.Operations;
using FinCore.Domain.Security;
using FluentValidation;

namespace FinCore.Application.Operations;

/// <summary>
/// HU-06 y HU-07 · Registrar una operación.
/// - La cuenta debe pertenecer a una sucursal autorizada (403 si no).
/// - Idempotencia (RN-11): con la misma Idempotency-Key y los mismos datos devuelve la original; con otros datos, 422.
/// - Referencia única por empresa (RN-03): 409.
/// </summary>
public sealed class RegisterTransactionHandler(
    IValidator<RegisterTransactionCommand> validator,
    ICurrentUser currentUser,
    OperationScope scope,
    IAccountRepository accounts,
    ITransactionRepository transactions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<RegisterTransactionResult>> HandleAsync(RegisterTransactionCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;

        var account = await accounts.GetByIdAsync(command.AccountId, ct);
        if (account is null || account.CompanyId != companyId)
            return Error.NotFound("account.not_found", "La cuenta no existe.");
        if (!(await scope.AuthorizedBranchIdsAsync(ct)).Contains(account.BranchId))
            return Error.Forbidden("transaction.branch_forbidden", "No tienes autorización para registrar operaciones en esa sucursal.");

        var type = TransactionCodes.RegistrableTypes[command.Type.ToUpperInvariant()];
        var reference = TransactionRules.NormalizeReference(command.Reference);

        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var previous = await transactions.GetByIdempotencyKeyAsync(companyId, command.IdempotencyKey.Trim(), ct);
            if (previous is not null)
            {
                var sameRequest = previous.AccountId == account.Id && previous.Type == type && previous.Reference == reference
                                  && previous.OperationDate == command.OperationDate && previous.Amount == command.Amount;
                return sameRequest
                    ? new RegisterTransactionResult(previous.PublicId, Created: false)
                    : new Error("transaction.idempotency_conflict", "La Idempotency-Key ya se usó con otros datos.", ErrorType.Unprocessable);
            }
        }

        if (await transactions.ReferenceExistsAsync(companyId, reference, ct))
            return Error.Conflict("transaction.duplicate_reference", $"Ya existe una operación con la referencia {reference}.");

        var transaction = Transaction.Register(new NewTransaction(account, type, reference, command.OperationDate, command.Amount,
            command.Description, command.CounterpartyName, command.CounterpartyAccount, command.IdempotencyKey),
            userId, clock.GetUtcNow().UtcDateTime);
        transactions.Add(transaction);

        auditLog.Record(new AuditEntry(AuditActions.TransactionCreated, "Transaction", transaction.PublicId.ToString(), userId,
            currentUser.UserName, companyId, Snapshot(transaction), BranchId: transaction.BranchId));
        await unitOfWork.SaveChangesAsync(ct);
        return new RegisterTransactionResult(transaction.PublicId, Created: true);
    }

    internal static object Snapshot(Transaction t) => new
    {
        t.Reference, type = t.Type.Code(), t.AccountId, t.OperationDate, t.Amount, t.Direction,
        t.Description, t.CounterpartyName, t.CounterpartyAccount, status = t.Status.ToString(),
    };
}

/// <summary>RN-06 · Editar una operación pendiente, con concurrencia optimista (rowVersion).</summary>
public sealed class UpdateTransactionHandler(
    IValidator<UpdateTransactionCommand> validator,
    ICurrentUser currentUser,
    OperationScope scope,
    ITransactionRepository transactions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(UpdateTransactionCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;

        var loaded = await scope.LoadAsync(command.PublicId, ct);
        if (!loaded.IsSuccess) return loaded.Error!;
        var transaction = loaded.Value;

        var rowVersion = Guard.FromBase64(command.RowVersion);
        if (rowVersion is null) return new Error("validation.rowVersion", "La versión del registro no es válida.", ErrorType.Validation);
        transactions.SetExpectedRowVersion(transaction, rowVersion);

        var before = RegisterTransactionHandler.Snapshot(transaction);
        transaction.UpdatePending(new PendingTransactionChanges(command.OperationDate, command.Amount, command.Description,
                command.CounterpartyName, command.CounterpartyAccount),
            userId, currentUser.HasPermission(Permissions.OperationsProcess), clock.GetUtcNow().UtcDateTime);

        auditLog.Record(new AuditEntry(AuditActions.TransactionUpdated, "Transaction", transaction.PublicId.ToString(), userId,
            currentUser.UserName, companyId, RegisterTransactionHandler.Snapshot(transaction), before, transaction.BranchId));
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>HU-09 · Procesar (con doble control) o rechazar (con motivo) una operación pendiente.</summary>
public sealed class ProcessTransactionHandler(
    ICurrentUser currentUser, OperationScope scope, IAuditLog auditLog, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(ChangeTransactionStatusCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;
        var loaded = await scope.LoadAsync(command.PublicId, ct);
        if (!loaded.IsSuccess) return loaded.Error!;

        var transaction = loaded.Value;
        transaction.Process(userId, clock.GetUtcNow().UtcDateTime);
        auditLog.Record(new AuditEntry(AuditActions.TransactionProcessed, "Transaction", transaction.PublicId.ToString(), userId,
            currentUser.UserName, companyId, new { transaction.Reference, transaction.Amount }, BranchId: transaction.BranchId));
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class RejectTransactionHandler(
    IValidator<ChangeTransactionStatusCommand> validator,
    ICurrentUser currentUser, OperationScope scope, IAuditLog auditLog, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(ChangeTransactionStatusCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;
        var loaded = await scope.LoadAsync(command.PublicId, ct);
        if (!loaded.IsSuccess) return loaded.Error!;

        var transaction = loaded.Value;
        transaction.Reject(command.Reason!, userId, clock.GetUtcNow().UtcDateTime);
        auditLog.Record(new AuditEntry(AuditActions.TransactionRejected, "Transaction", transaction.PublicId.ToString(), userId,
            currentUser.UserName, companyId, new { transaction.Reference, reason = transaction.RejectionReason }, BranchId: transaction.BranchId));
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>HU-10 · Revertir: crea la operación de reversión y deja la original Revertida, en una sola transacción.</summary>
public sealed class ReverseTransactionHandler(
    IValidator<ChangeTransactionStatusCommand> validator,
    ICurrentUser currentUser, OperationScope scope, ITransactionRepository transactions,
    IAuditLog auditLog, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<Guid>> HandleAsync(ChangeTransactionStatusCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;
        var loaded = await scope.LoadAsync(command.PublicId, ct);
        if (!loaded.IsSuccess) return loaded.Error!;

        var original = loaded.Value;
        var reversal = original.Reverse(command.Reason!, userId, clock.GetUtcNow().UtcDateTime);
        if (await transactions.ReferenceExistsAsync(companyId, reversal.Reference, ct))
            return Error.Conflict("transaction.duplicate_reference", $"Ya existe una operación con la referencia {reversal.Reference}.");

        transactions.Add(reversal);
        auditLog.Record(new AuditEntry(AuditActions.TransactionReversed, "Transaction", original.PublicId.ToString(), userId,
            currentUser.UserName, companyId,
            new { original.Reference, reversalReference = reversal.Reference, reversalPublicId = reversal.PublicId, reason = command.Reason },
            BranchId: original.BranchId));
        await unitOfWork.SaveChangesAsync(ct);   // inserta la reversión y actualiza la original en la misma transacción
        return reversal.PublicId;
    }
}
