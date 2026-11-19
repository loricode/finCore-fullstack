namespace FinCore.Application.Common.Abstractions;

/// <summary>Acciones auditadas. Se guardan en audit.AuditLogs.Action (máx. 30 caracteres).</summary>
public static class AuditActions
{
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string LoginLocked = "LOGIN_LOCKED";
    public const string Logout = "LOGOUT";
    public const string TokenRefreshed = "TOKEN_REFRESHED";
    public const string RefreshTokenReuse = "REFRESH_TOKEN_REUSE";

    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserBranchesChanged = "USER_BRANCHES_CHANGED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserActivated = "USER_ACTIVATED";
    public const string UserWindowsAccountChanged = "USER_WINDOWS_ACCOUNT";

    public const string TransactionCreated = "TRANSACTION_CREATED";
    public const string TransactionUpdated = "TRANSACTION_UPDATED";
    public const string TransactionProcessed = "TRANSACTION_PROCESSED";
    public const string TransactionRejected = "TRANSACTION_REJECTED";
    public const string TransactionReversed = "TRANSACTION_REVERSED";

    public const string ReconciliationRun = "RECONCILIATION_RUN";
    public const string ReconciliationResolved = "RECONCILIATION_RESOLVED";
    public const string StatementImported = "STATEMENT_IMPORTED";
}

/// <summary>
/// Entrada de auditoría. <paramref name="Details"/> va a NewValues y <paramref name="OldDetails"/> a OldValues, como JSON.
/// NUNCA deben contener contraseñas, hashes ni tokens.
/// </summary>
public sealed record AuditEntry(
    string Action,
    string EntityName,
    string? EntityId,
    int? UserId,
    string? UserName,
    int? CompanyId,
    object? Details = null,
    object? OldDetails = null,
    int? BranchId = null);

/// <summary>
/// Registro de auditoría. La entrada se guarda con el siguiente <see cref="IUnitOfWork.SaveChangesAsync"/>,
/// en la misma transacción que el cambio auditado. CorrelationId e IP los agrega la implementación.
/// </summary>
public interface IAuditLog
{
    void Record(AuditEntry entry);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Ejecuta varios SaveChanges en una sola transacción de base de datos (todo o nada). Se usa cuando se necesita
    /// un Id generado por SQL Server a mitad del caso de uso. Compatible con la estrategia de reintentos.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);
}
