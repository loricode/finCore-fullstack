namespace FinCore.Infrastructure.Persistence;

// Modelos de persistencia que no son entidades de dominio: tablas de relación y catálogos,
// la auditoría (el dominio no la conoce; la escribe IAuditLog) y filas de vistas y funciones.

internal sealed class RoleRecord
{
    public short Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

internal sealed class PermissionRecord
{
    public short Id { get; set; }
    public string Code { get; set; } = string.Empty;
}

internal sealed class RolePermissionRecord
{
    public short RoleId { get; set; }
    public short PermissionId { get; set; }
}

internal sealed class UserRoleRecord
{
    public int UserId { get; set; }
    public short RoleId { get; set; }
    public int? AssignedByUserId { get; set; }
}

internal sealed class UserBranchRecord
{
    public int UserId { get; set; }
    public int BranchId { get; set; }
    public int CompanyId { get; set; }
    public int? AssignedByUserId { get; set; }
}

internal sealed class UserWindowsAccountRecord
{
    public int UserId { get; set; }
    public string WindowsAccount { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; }
    public int? AssignedByUserId { get; set; }
}

internal sealed class AuditLogRecord
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>Fila de sec.tvf_AuthorizedBranches.</summary>
internal sealed class AuthorizedBranchRow
{
    public int BranchId { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

/// <summary>Fila de la vista fin.vw_Transactions.</summary>
internal sealed class TransactionViewRow
{
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string TransactionTypeCode { get; set; } = string.Empty;
    public string TransactionTypeName { get; set; } = string.Empty;
    public byte StatusId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateOnly OperationDate { get; set; }
    public decimal Amount { get; set; }
    public short Direction { get; set; }
    public decimal SignedAmount { get; set; }
    public string? Description { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyAccount { get; set; }
    public Guid? OriginalPublicId { get; set; }
    public Guid? ReversalPublicId { get; set; }
    public string? RejectionReason { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsEffective { get; set; }
}

/// <summary>Resultado de la consulta del historial de estados.</summary>
internal sealed class HistoryRow
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string? ChangedBy { get; set; }
    public string? CorrelationId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Cuenta con saldo (fin.tvf_AccountBalance).</summary>
internal sealed class AccountRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}
