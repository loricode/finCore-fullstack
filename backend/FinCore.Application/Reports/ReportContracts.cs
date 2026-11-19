namespace FinCore.Application.Reports;

public sealed record BranchSummaryRow(
    int BranchId, string BranchCode, string BranchName, string City, int TotalOperations,
    decimal TotalIncome, decimal TotalExpense, decimal NetAmount,
    int PendingCount, int ProcessedCount, int ReconciledCount, int RejectedCount, int ReversedCount);

public sealed record OperationsByPeriodRow(
    DateOnly PeriodStart, string TransactionTypeCode, string TransactionTypeName, int OperationCount, int EffectiveCount, decimal EffectiveAmount);

public sealed record UserActivityRow(
    int UserId, string UserName, string FullName, bool IsActive, DateTime? LastLoginAtUtc,
    int OperationsCreated, decimal AmountCreated, int StatusChanges, int AuditActions, DateTime? LastActionAtUtc);

public sealed record FinancialIndicatorsDto(
    DateOnly DateFrom, DateOnly DateTo, int OperationCount, int PendingCount, int ProcessedCount, int ReconciledCount,
    int RejectedCount, int ReversedCount, decimal TotalIncome, decimal TotalExpense, decimal NetAmount,
    decimal ReconciliationCoveragePct, int OpenDifferenceCount, decimal OpenDifferenceAmount,
    int EtlExecutionCount, int EtlRowsRead, int EtlRowsRejected, decimal EtlRejectionPct);

public sealed record ReportPeriod(DateOnly From, DateOnly To);

/// <summary>
/// Ejecuta los procedimientos rpt.* con el usuario de la sesión. Los mismos procedimientos alimentan los informes SSRS:
/// un solo cálculo, dos presentaciones (spec reportes-financieros). Permisos y alcance los valida también SQL (50300-50304).
/// </summary>
public interface IReportQueries
{
    Task<IReadOnlyList<BranchSummaryRow>> BranchSummaryAsync(int userId, ReportPeriod period, CancellationToken ct);
    Task<IReadOnlyList<OperationsByPeriodRow>> OperationsByPeriodAsync(int userId, ReportPeriod period, string granularity, int? branchId, CancellationToken ct);
    Task<IReadOnlyList<UserActivityRow>> UserActivityAsync(int userId, ReportPeriod period, CancellationToken ct);
    Task<FinancialIndicatorsDto> IndicatorsAsync(int userId, ReportPeriod period, CancellationToken ct);
}

/// <summary>Un archivo exportado.</summary>
public sealed record ExportedFile(string FileName, string ContentType, byte[] Content);
