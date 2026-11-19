/*
    rec.vw_OpenDifferences · Diferencias sin resolver de la última conciliación completada de cada cuenta.
    Es la bandeja de trabajo del supervisor: lo que hay que revisar hoy.
    (Cada conciliación evalúa de nuevo todo lo no conciliado del período, por eso basta la última.)
*/
CREATE OR ALTER VIEW rec.vw_OpenDifferences
AS
WITH LastRun AS
(
    SELECT r.Id, r.AccountId, r.PeriodFrom, r.PeriodTo, r.FinishedAtUtc,
           ROW_NUMBER() OVER (PARTITION BY r.AccountId ORDER BY r.FinishedAtUtc DESC, r.Id DESC) AS rn
    FROM rec.Reconciliations r
    WHERE r.Status = 'COMPLETED'
)
SELECT
    d.Id                 AS ReconciliationDetailId,
    lr.Id                AS ReconciliationId,
    lr.AccountId,
    a.BranchId,
    lr.PeriodFrom,
    lr.PeriodTo,
    rt.Code              AS ResultTypeCode,
    rt.Name              AS ResultTypeName,
    d.TransactionId,
    t.Reference          AS InternalReference,
    t.OperationDate,
    d.BankStatementDetailId,
    bd.BankReference,
    bd.MovementDate,
    d.InternalAmount,
    d.BankAmount,
    d.DifferenceAmount,
    d.Notes
FROM LastRun lr
JOIN fin.Accounts a                       ON a.Id = lr.AccountId
JOIN rec.ReconciliationDetails d          ON d.ReconciliationId = lr.Id
JOIN rec.ReconciliationResultTypes rt     ON rt.Id = d.ResultTypeId
LEFT JOIN fin.Transactions t              ON t.Id = d.TransactionId
LEFT JOIN rec.BankStatementDetails bd     ON bd.Id = d.BankStatementDetailId
WHERE lr.rn = 1
  AND rt.RequiresAction = 1
  AND d.ResolvedAtUtc IS NULL;
