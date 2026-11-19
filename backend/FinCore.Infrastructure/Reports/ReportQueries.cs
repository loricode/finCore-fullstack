using FinCore.Application.Reports;
using FinCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Reports;

/// <summary>Ejecuta los procedimientos rpt.* (los mismos que usan los informes SSRS).</summary>
internal sealed class ReportQueries(FinCoreDbContext db) : IReportQueries
{
    public async Task<IReadOnlyList<BranchSummaryRow>> BranchSummaryAsync(int userId, ReportPeriod p, CancellationToken ct) =>
        (await db.Database.SqlQuery<BranchSummarySql>(
            $"EXEC rpt.usp_BranchSummary @UserId = {userId}, @DateFrom = {p.From}, @DateTo = {p.To}").ToListAsync(ct))
        .Select(r => new BranchSummaryRow(r.BranchId, r.BranchCode, r.BranchName, r.City, r.TotalOperations, r.TotalIncome,
            r.TotalExpense, r.NetAmount, r.PendingCount, r.ProcessedCount, r.ReconciledCount, r.RejectedCount, r.ReversedCount))
        .ToList();

    public async Task<IReadOnlyList<OperationsByPeriodRow>> OperationsByPeriodAsync(int userId, ReportPeriod p, string granularity,
        int? branchId, CancellationToken ct) =>
        (await db.Database.SqlQuery<OperationsByPeriodSql>(
            $"EXEC rpt.usp_OperationsByPeriod @UserId = {userId}, @DateFrom = {p.From}, @DateTo = {p.To}, @Granularity = {granularity}, @BranchId = {branchId}")
            .ToListAsync(ct))
        .Select(r => new OperationsByPeriodRow(r.PeriodStart, r.TransactionTypeCode, r.TransactionTypeName, r.OperationCount,
            r.EffectiveCount, r.EffectiveAmount))
        .ToList();

    public async Task<IReadOnlyList<UserActivityRow>> UserActivityAsync(int userId, ReportPeriod p, CancellationToken ct) =>
        (await db.Database.SqlQuery<UserActivitySql>(
            $"EXEC rpt.usp_UserActivity @UserId = {userId}, @DateFrom = {p.From}, @DateTo = {p.To}").ToListAsync(ct))
        .Select(r => new UserActivityRow(r.UserId, r.UserName, r.FullName, r.IsActive, r.LastLoginAtUtc, r.OperationsCreated,
            r.AmountCreated, r.StatusChanges, r.AuditActions, r.LastActionAtUtc))
        .ToList();

    public async Task<FinancialIndicatorsDto> IndicatorsAsync(int userId, ReportPeriod p, CancellationToken ct)
    {
        var r = (await db.Database.SqlQuery<IndicatorsSql>(
            $"EXEC rpt.usp_FinancialIndicators @UserId = {userId}, @DateFrom = {p.From}, @DateTo = {p.To}").ToListAsync(ct)).Single();
        return new FinancialIndicatorsDto(r.DateFrom, r.DateTo, r.OperationCount, r.PendingCount, r.ProcessedCount, r.ReconciledCount,
            r.RejectedCount, r.ReversedCount, r.TotalIncome, r.TotalExpense, r.NetAmount, r.ReconciliationCoveragePct,
            r.OpenDifferenceCount, r.OpenDifferenceAmount, r.EtlExecutionCount, r.EtlRowsRead, r.EtlRowsRejected, r.EtlRejectionPct);
    }

    // Filas tal como las devuelven los procedimientos (nombres de columna = nombres de propiedad).
    private sealed class BranchSummarySql
    {
        public int BranchId { get; set; }
        public string BranchCode { get; set; } = "";
        public string BranchName { get; set; } = "";
        public string City { get; set; } = "";
        public int TotalOperations { get; set; }
        [Precision(18, 2)] public decimal TotalIncome { get; set; }
        [Precision(18, 2)] public decimal TotalExpense { get; set; }
        [Precision(18, 2)] public decimal NetAmount { get; set; }
        public int PendingCount { get; set; }
        public int ProcessedCount { get; set; }
        public int ReconciledCount { get; set; }
        public int RejectedCount { get; set; }
        public int ReversedCount { get; set; }
    }

    private sealed class OperationsByPeriodSql
    {
        public DateOnly PeriodStart { get; set; }
        public string TransactionTypeCode { get; set; } = "";
        public string TransactionTypeName { get; set; } = "";
        public int OperationCount { get; set; }
        public int EffectiveCount { get; set; }
        [Precision(18, 2)] public decimal EffectiveAmount { get; set; }
    }

    private sealed class UserActivitySql
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string FullName { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
        public int OperationsCreated { get; set; }
        [Precision(18, 2)] public decimal AmountCreated { get; set; }
        public int StatusChanges { get; set; }
        public int AuditActions { get; set; }
        public DateTime? LastActionAtUtc { get; set; }
    }

    private sealed class IndicatorsSql
    {
        public DateOnly DateFrom { get; set; }
        public DateOnly DateTo { get; set; }
        public int OperationCount { get; set; }
        public int PendingCount { get; set; }
        public int ProcessedCount { get; set; }
        public int ReconciledCount { get; set; }
        public int RejectedCount { get; set; }
        public int ReversedCount { get; set; }
        [Precision(18, 2)] public decimal TotalIncome { get; set; }
        [Precision(18, 2)] public decimal TotalExpense { get; set; }
        [Precision(18, 2)] public decimal NetAmount { get; set; }
        [Precision(18, 2)] public decimal ReconciliationCoveragePct { get; set; }
        public int OpenDifferenceCount { get; set; }
        [Precision(18, 2)] public decimal OpenDifferenceAmount { get; set; }
        public int EtlExecutionCount { get; set; }
        public int EtlRowsRead { get; set; }
        public int EtlRowsRejected { get; set; }
        [Precision(18, 2)] public decimal EtlRejectionPct { get; set; }
    }
}
