/* rec.vw_ReconciliationSummary · Una fila por conciliación con el conteo de cada tipo de resultado. */
CREATE OR ALTER VIEW rec.vw_ReconciliationSummary
AS
SELECT
    r.Id                AS ReconciliationId,
    r.AccountId,
    a.Code              AS AccountCode,
    a.BranchId,
    r.PeriodFrom,
    r.PeriodTo,
    r.DateToleranceDays,
    r.Status,
    r.StartedAtUtc,
    r.FinishedAtUtc,
    DATEDIFF(MILLISECOND, r.StartedAtUtc, r.FinishedAtUtc) AS DurationMs,
    r.ExecutedByUserId,
    r.ExecutedBy,
    r.CorrelationId,
    r.InternalCount,
    r.BankCount,
    r.MatchedCount,
    r.DifferenceCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId = 2 THEN 1 ELSE 0 END), 0) AS AmountMismatchCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId = 3 THEN 1 ELSE 0 END), 0) AS DateMismatchCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId = 4 THEN 1 ELSE 0 END), 0) AS MissingInBankCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId = 5 THEN 1 ELSE 0 END), 0) AS MissingInternalCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId = 6 THEN 1 ELSE 0 END), 0) AS DuplicateReferenceCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId = 7 THEN 1 ELSE 0 END), 0) AS PendingReviewCount,
    ISNULL(SUM(CASE WHEN d.ResultTypeId <> 1 AND d.ResolvedAtUtc IS NULL THEN 1 ELSE 0 END), 0) AS OpenDifferenceCount,
    CAST(ISNULL(SUM(CASE WHEN d.ResultTypeId <> 1 THEN d.DifferenceAmount ELSE 0 END), 0) AS DECIMAL(18,2)) AS NetDifferenceAmount,
    r.ErrorMessage
FROM rec.Reconciliations r
JOIN fin.Accounts a                     ON a.Id = r.AccountId
LEFT JOIN rec.ReconciliationDetails d   ON d.ReconciliationId = r.Id
GROUP BY
    r.Id, r.AccountId, a.Code, a.BranchId, r.PeriodFrom, r.PeriodTo, r.DateToleranceDays, r.Status,
    r.StartedAtUtc, r.FinishedAtUtc, r.ExecutedByUserId, r.ExecutedBy, r.CorrelationId,
    r.InternalCount, r.BankCount, r.MatchedCount, r.DifferenceCount, r.ErrorMessage;
