/*
    rpt.usp_BranchSummary · Totales por sucursal en un período (solo sucursales autorizadas al usuario).

    Ingresos y egresos son BRUTOS: solo operaciones efectivas (procesadas o conciliadas, sin pares de
    reversión). El neto también es Ingresos - Egresos, así que una operación revertida no infla ningún total.
    Los conteos por estado incluyen todas las operaciones del período.
*/
CREATE OR ALTER PROCEDURE rpt.usp_BranchSummary
    @UserId    INT,
    @DateFrom  DATE,
    @DateTo    DATE
AS
BEGIN
    SET NOCOUNT ON;

    EXEC rpt.usp_AssertCanReadReports @UserId = @UserId, @DateFrom = @DateFrom, @DateTo = @DateTo;

    SELECT
        ab.BranchId,
        ab.Code                                                                         AS BranchCode,
        ab.Name                                                                         AS BranchName,
        ab.City,
        COUNT(t.Id)                                                                     AS TotalOperations,
        CAST(SUM(CASE WHEN eff.IsEffective = 1 AND t.SignedAmount > 0 THEN t.SignedAmount ELSE 0 END) AS DECIMAL(18,2))  AS TotalIncome,
        CAST(SUM(CASE WHEN eff.IsEffective = 1 AND t.SignedAmount < 0 THEN -t.SignedAmount ELSE 0 END) AS DECIMAL(18,2)) AS TotalExpense,
        CAST(SUM(CASE WHEN eff.IsEffective = 1 THEN t.SignedAmount ELSE 0 END) AS DECIMAL(18,2))                          AS NetAmount,
        SUM(CASE WHEN t.StatusId = 1 THEN 1 ELSE 0 END)                                 AS PendingCount,
        SUM(CASE WHEN t.StatusId = 2 THEN 1 ELSE 0 END)                                 AS ProcessedCount,
        SUM(CASE WHEN t.StatusId = 3 THEN 1 ELSE 0 END)                                 AS ReconciledCount,
        SUM(CASE WHEN t.StatusId = 4 THEN 1 ELSE 0 END)                                 AS RejectedCount,
        SUM(CASE WHEN t.StatusId = 5 THEN 1 ELSE 0 END)                                 AS ReversedCount
    FROM sec.tvf_AuthorizedBranches(@UserId) ab
    LEFT JOIN fin.Transactions t
           ON t.BranchId = ab.BranchId
          AND t.OperationDate BETWEEN @DateFrom AND @DateTo
    OUTER APPLY (SELECT CASE WHEN t.StatusId IN (2, 3) AND t.TransactionTypeId <> 5 THEN 1 ELSE 0 END AS IsEffective) eff
    GROUP BY ab.BranchId, ab.Code, ab.Name, ab.City
    ORDER BY ab.Name;
END;
