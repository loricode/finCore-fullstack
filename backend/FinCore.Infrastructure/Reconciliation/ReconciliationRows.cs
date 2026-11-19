namespace FinCore.Infrastructure.Reconciliation;

// Filas de vistas y consultas de conciliación (tipos sin llave de EF Core).

internal sealed class ReconciliationSummaryRow
{
    public int ReconciliationId { get; set; }
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public byte DateToleranceDays { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? ExecutedBy { get; set; }
    public string? CorrelationId { get; set; }
    public int InternalCount { get; set; }
    public int BankCount { get; set; }
    public int MatchedCount { get; set; }
    public int DifferenceCount { get; set; }
    public int AmountMismatchCount { get; set; }
    public int DateMismatchCount { get; set; }
    public int MissingInBankCount { get; set; }
    public int MissingInternalCount { get; set; }
    public int DuplicateReferenceCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int OpenDifferenceCount { get; set; }
    public decimal NetDifferenceAmount { get; set; }
    public string? ErrorMessage { get; set; }
}

internal sealed class ReconciliationDetailRow
{
    public long Id { get; set; }
    public string ResultTypeCode { get; set; } = string.Empty;
    public string ResultTypeName { get; set; } = string.Empty;
    public bool RequiresAction { get; set; }
    public Guid? TransactionPublicId { get; set; }
    public string? InternalReference { get; set; }
    public DateOnly? OperationDate { get; set; }
    public string? BankReference { get; set; }
    public DateOnly? MovementDate { get; set; }
    public string? BankDescription { get; set; }
    public decimal? InternalAmount { get; set; }
    public decimal? BankAmount { get; set; }
    public decimal DifferenceAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNote { get; set; }
}

internal sealed class OpenDifferenceRow
{
    public long ReconciliationDetailId { get; set; }
    public int ReconciliationId { get; set; }
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public string ResultTypeCode { get; set; } = string.Empty;
    public string ResultTypeName { get; set; } = string.Empty;
    public Guid? TransactionPublicId { get; set; }
    public string? InternalReference { get; set; }
    public DateOnly? OperationDate { get; set; }
    public string? BankReference { get; set; }
    public DateOnly? MovementDate { get; set; }
    public decimal? InternalAmount { get; set; }
    public decimal? BankAmount { get; set; }
    public decimal DifferenceAmount { get; set; }
    public string? Notes { get; set; }
}

internal sealed class EtlExecutionRow
{
    public long EtlExecutionId { get; set; }
    public int CompanyId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int RowsRead { get; set; }
    public int RowsLoaded { get; set; }
    public int RowsRejected { get; set; }
    public string? ErrorCodes { get; set; }
    public string? ErrorMessage { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
}

internal sealed class EtlErrorRow
{
    public int RowNumber { get; set; }
    public string? SourceKey { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
