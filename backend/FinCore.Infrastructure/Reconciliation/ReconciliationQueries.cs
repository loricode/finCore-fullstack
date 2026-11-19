using System.Text.Json;
using FinCore.Application.Operations;
using FinCore.Application.Reconciliation;
using FinCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Reconciliation;

/// <summary>Lecturas de conciliación y ETL sobre las vistas rec.vw_* y etl.vw_* de la base.</summary>
internal sealed class ReconciliationQueries(FinCoreDbContext db) : IReconciliationQueries
{
    public async Task<PagedResult<ReconciliationSummaryDto>> ListAsync(IReadOnlyCollection<int> branchIds, int? accountId,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<ReconciliationSummaryRow>().AsNoTracking().Where(r => branchIds.Contains(r.BranchId));
        if (accountId is { } id) query = query.Where(r => r.AccountId == id);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.StartedAtUtc).ThenByDescending(r => r.ReconciliationId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ReconciliationSummaryDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<ReconciliationSummaryDto?> GetSummaryAsync(int reconciliationId, IReadOnlyCollection<int> branchIds, CancellationToken ct)
    {
        var row = await db.Set<ReconciliationSummaryRow>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.ReconciliationId == reconciliationId && branchIds.Contains(r.BranchId), ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<ReconciliationDetailDto>> GetDetailsAsync(int reconciliationId, CancellationToken ct) =>
        (await db.Set<ReconciliationDetailRow>().FromSql($"""
            SELECT d.Id, rt.Code AS ResultTypeCode, rt.Name AS ResultTypeName, rt.RequiresAction,
                   t.PublicId AS TransactionPublicId, t.Reference AS InternalReference, t.OperationDate,
                   bd.BankReference, bd.MovementDate, bd.Description AS BankDescription,
                   d.InternalAmount, d.BankAmount, d.DifferenceAmount, d.Notes,
                   d.ResolvedAtUtc, u.UserName AS ResolvedBy, d.ResolutionNote
            FROM rec.ReconciliationDetails d
            JOIN rec.ReconciliationResultTypes rt ON rt.Id = d.ResultTypeId
            LEFT JOIN fin.Transactions t          ON t.Id = d.TransactionId
            LEFT JOIN rec.BankStatementDetails bd ON bd.Id = d.BankStatementDetailId
            LEFT JOIN sec.Users u                 ON u.Id = d.ResolvedByUserId
            WHERE d.ReconciliationId = {reconciliationId}
            """).AsNoTracking().ToListAsync(ct))
        .OrderByDescending(d => d.RequiresAction).ThenBy(d => d.ResultTypeCode).ThenBy(d => d.OperationDate ?? d.MovementDate)
        .Select(d => new ReconciliationDetailDto(d.Id, d.ResultTypeCode, d.ResultTypeName, d.RequiresAction, d.TransactionPublicId,
            d.InternalReference, d.OperationDate, d.BankReference, d.MovementDate, d.BankDescription, d.InternalAmount, d.BankAmount,
            d.DifferenceAmount, d.Notes, d.ResolvedAtUtc, d.ResolvedBy, d.ResolutionNote))
        .ToList();

    public async Task<IReadOnlyList<OpenDifferenceDto>> GetOpenDifferencesAsync(IReadOnlyCollection<int> branchIds, CancellationToken ct)
    {
        // Las sucursales viajan como un parámetro JSON (OPENJSON): consulta parametrizada.
        var branchesJson = JsonSerializer.Serialize(branchIds);
        var rows = await db.Set<OpenDifferenceRow>().FromSql($"""
            SELECT o.ReconciliationDetailId, o.ReconciliationId, o.AccountId, a.Code AS AccountCode, o.BranchId, b.Name AS BranchName,
                   o.PeriodFrom, o.PeriodTo, o.ResultTypeCode, o.ResultTypeName, t.PublicId AS TransactionPublicId,
                   o.InternalReference, o.OperationDate, o.BankReference, o.MovementDate,
                   o.InternalAmount, o.BankAmount, o.DifferenceAmount, o.Notes
            FROM rec.vw_OpenDifferences o
            JOIN fin.Accounts a     ON a.Id = o.AccountId
            JOIN sec.Branches b     ON b.Id = o.BranchId
            LEFT JOIN fin.Transactions t ON t.Id = o.TransactionId
            WHERE o.BranchId IN (SELECT CAST(value AS INT) FROM OPENJSON({branchesJson}))
            """).AsNoTracking().ToListAsync(ct);

        return rows.OrderBy(r => r.BranchName).ThenBy(r => r.ResultTypeCode).ThenBy(r => r.OperationDate ?? r.MovementDate)
            .Select(r => new OpenDifferenceDto(r.ReconciliationDetailId, r.ReconciliationId, r.AccountId, r.AccountCode, r.BranchId,
                r.BranchName, r.PeriodFrom, r.PeriodTo, r.ResultTypeCode, r.ResultTypeName, r.TransactionPublicId, r.InternalReference,
                r.OperationDate, r.BankReference, r.MovementDate, r.InternalAmount, r.BankAmount, r.DifferenceAmount, r.Notes))
            .ToList();
    }

    public async Task<PagedResult<EtlExecutionDto>> ListEtlExecutionsAsync(int companyId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<EtlExecutionRow>().AsNoTracking().Where(e => e.CompanyId == companyId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.EtlExecutionId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<EtlExecutionDto>(rows.Select(e => new EtlExecutionDto(e.EtlExecutionId, e.PackageName, e.SourceSystem,
            e.SourceName, e.Status, e.StartedAtUtc, e.FinishedAtUtc, e.RowsRead, e.RowsLoaded, e.RowsRejected, e.ErrorCodes,
            e.ErrorMessage, e.TriggeredBy)).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<EtlErrorDto>> GetEtlErrorsAsync(int companyId, long etlExecutionId, int max, CancellationToken ct) =>
        (await db.Set<EtlErrorRow>().FromSql($"""
            SELECT TOP ({max}) d.RowNumber, d.SourceKey, d.ErrorCode, d.ErrorMessage
            FROM etl.ETLExecutionDetails d
            JOIN etl.ETLExecutions e ON e.Id = d.EtlExecutionId
            WHERE d.EtlExecutionId = {etlExecutionId} AND e.CompanyId = {companyId}
            ORDER BY d.RowNumber
            """).AsNoTracking().ToListAsync(ct))
        .Select(e => new EtlErrorDto(e.RowNumber, e.SourceKey, e.ErrorCode, e.ErrorMessage))
        .ToList();

    private static ReconciliationSummaryDto ToDto(ReconciliationSummaryRow r) => new(
        r.ReconciliationId, r.AccountId, r.AccountCode, r.BranchId, r.PeriodFrom, r.PeriodTo, r.DateToleranceDays, r.Status,
        r.StartedAtUtc, r.FinishedAtUtc, r.ExecutedBy, r.CorrelationId, r.InternalCount, r.BankCount, r.MatchedCount,
        r.DifferenceCount, r.AmountMismatchCount, r.DateMismatchCount, r.MissingInBankCount, r.MissingInternalCount,
        r.DuplicateReferenceCount, r.PendingReviewCount, r.OpenDifferenceCount, r.NetDifferenceAmount, r.ErrorMessage);
}
