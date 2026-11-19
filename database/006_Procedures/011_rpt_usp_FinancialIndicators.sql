/*
    rpt.usp_FinancialIndicators · Indicadores financieros de un período en las sucursales autorizadas (una fila).
    Base del informe "Indicadores financieros" (SSRS) y de GET /api/v1/reports/indicators.

      Ingresos / Egresos / Neto      operaciones efectivas (procesadas o conciliadas, sin pares de reversión)
      Cobertura de conciliación      % de operaciones efectivas ya conciliadas con el banco
      Diferencias abiertas           de la última conciliación de cada cuenta (rec.vw_OpenDifferences)
      Calidad de las cargas          filas rechazadas sobre leídas en las cargas ETL del período (empresa)
*/
CREATE OR ALTER PROCEDURE rpt.usp_FinancialIndicators
    @UserId    INT,
    @DateFrom  DATE,
    @DateTo    DATE
AS
BEGIN
    SET NOCOUNT ON;

    EXEC rpt.usp_AssertCanReadReports @UserId = @UserId, @DateFrom = @DateFrom, @DateTo = @DateTo;

    DECLARE @CompanyId INT = (SELECT CompanyId FROM sec.Users WHERE Id = @UserId);
    DECLARE @FromUtc DATETIME2(3) = DATEADD(HOUR, 5, CAST(@DateFrom AS DATETIME2(3)));
    DECLARE @ToUtc   DATETIME2(3) = DATEADD(HOUR, 5, CAST(DATEADD(DAY, 1, @DateTo) AS DATETIME2(3)));

    WITH Ops AS
    (
        SELECT t.StatusId, t.SignedAmount,
               CASE WHEN t.StatusId IN (2, 3) AND t.TransactionTypeId <> 5 THEN 1 ELSE 0 END AS IsEffective
        FROM fin.Transactions t
        JOIN sec.tvf_AuthorizedBranches(@UserId) ab ON ab.BranchId = t.BranchId
        WHERE t.OperationDate BETWEEN @DateFrom AND @DateTo
    ),
    Totals AS
    (
        SELECT
            COUNT(*)                                                                         AS OperationCount,
            SUM(CASE WHEN StatusId = 1 THEN 1 ELSE 0 END)                                    AS PendingCount,
            SUM(CASE WHEN StatusId = 2 THEN 1 ELSE 0 END)                                    AS ProcessedCount,
            SUM(CASE WHEN StatusId = 3 THEN 1 ELSE 0 END)                                    AS ReconciledCount,
            SUM(CASE WHEN StatusId = 4 THEN 1 ELSE 0 END)                                    AS RejectedCount,
            SUM(CASE WHEN StatusId = 5 THEN 1 ELSE 0 END)                                    AS ReversedCount,
            SUM(IsEffective)                                                                 AS EffectiveCount,
            SUM(CASE WHEN IsEffective = 1 AND StatusId = 3 THEN 1 ELSE 0 END)                AS EffectiveReconciledCount,
            SUM(CASE WHEN IsEffective = 1 AND SignedAmount > 0 THEN SignedAmount ELSE 0 END) AS TotalIncome,
            SUM(CASE WHEN IsEffective = 1 AND SignedAmount < 0 THEN -SignedAmount ELSE 0 END) AS TotalExpense
        FROM Ops
    ),
    Differences AS
    (
        SELECT COUNT(*) AS OpenDifferenceCount, SUM(ABS(o.DifferenceAmount)) AS OpenDifferenceAmount
        FROM rec.vw_OpenDifferences o
        JOIN sec.tvf_AuthorizedBranches(@UserId) ab ON ab.BranchId = o.BranchId
    ),
    Loads AS
    (
        SELECT COUNT(*) AS EtlExecutionCount, SUM(RowsRead) AS EtlRowsRead, SUM(RowsRejected) AS EtlRowsRejected
        FROM etl.ETLExecutions
        WHERE CompanyId = @CompanyId AND StartedAtUtc >= @FromUtc AND StartedAtUtc < @ToUtc
    )
    SELECT
        @DateFrom                                                         AS DateFrom,
        @DateTo                                                           AS DateTo,
        ISNULL(t.OperationCount, 0)                                       AS OperationCount,
        ISNULL(t.PendingCount, 0)                                         AS PendingCount,
        ISNULL(t.ProcessedCount, 0)                                       AS ProcessedCount,
        ISNULL(t.ReconciledCount, 0)                                      AS ReconciledCount,
        ISNULL(t.RejectedCount, 0)                                        AS RejectedCount,
        ISNULL(t.ReversedCount, 0)                                        AS ReversedCount,
        CAST(ISNULL(t.TotalIncome, 0) AS DECIMAL(18,2))                   AS TotalIncome,
        CAST(ISNULL(t.TotalExpense, 0) AS DECIMAL(18,2))                  AS TotalExpense,
        CAST(ISNULL(t.TotalIncome, 0) - ISNULL(t.TotalExpense, 0) AS DECIMAL(18,2)) AS NetAmount,
        CAST(CASE WHEN ISNULL(t.EffectiveCount, 0) = 0 THEN 0
                  ELSE 100.0 * t.EffectiveReconciledCount / t.EffectiveCount END AS DECIMAL(5,2)) AS ReconciliationCoveragePct,
        ISNULL(d.OpenDifferenceCount, 0)                                  AS OpenDifferenceCount,
        CAST(ISNULL(d.OpenDifferenceAmount, 0) AS DECIMAL(18,2))          AS OpenDifferenceAmount,
        ISNULL(l.EtlExecutionCount, 0)                                    AS EtlExecutionCount,
        ISNULL(l.EtlRowsRead, 0)                                          AS EtlRowsRead,
        ISNULL(l.EtlRowsRejected, 0)                                      AS EtlRowsRejected,
        CAST(CASE WHEN ISNULL(l.EtlRowsRead, 0) = 0 THEN 0
                  ELSE 100.0 * l.EtlRowsRejected / l.EtlRowsRead END AS DECIMAL(5,2)) AS EtlRejectionPct
    FROM Totals t CROSS JOIN Differences d CROSS JOIN Loads l;
END;
