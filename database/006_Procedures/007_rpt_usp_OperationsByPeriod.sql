/*
    rpt.usp_OperationsByPeriod · Operaciones agrupadas por día o por mes y por tipo.
    @Granularity: 'DAY' | 'MONTH'. @BranchId opcional (debe estar autorizada; si no, no devuelve filas).
*/
CREATE OR ALTER PROCEDURE rpt.usp_OperationsByPeriod
    @UserId       INT,
    @DateFrom     DATE,
    @DateTo       DATE,
    @Granularity  VARCHAR(5) = 'DAY',
    @BranchId     INT        = NULL
AS
BEGIN
    SET NOCOUNT ON;

    EXEC rpt.usp_AssertCanReadReports @UserId = @UserId, @DateFrom = @DateFrom, @DateTo = @DateTo;

    IF @Granularity NOT IN ('DAY', 'MONTH')
        THROW 50302, N'La granularidad debe ser DAY o MONTH.', 1;

    SELECT
        p.PeriodStart,
        tt.Code                                                          AS TransactionTypeCode,
        tt.Name                                                          AS TransactionTypeName,
        COUNT(*)                                                         AS OperationCount,
        SUM(CASE WHEN t.StatusId IN (2, 3) THEN 1 ELSE 0 END)            AS EffectiveCount,
        CAST(SUM(CASE WHEN t.StatusId IN (2, 3) THEN t.Amount ELSE 0 END) AS DECIMAL(18,2)) AS EffectiveAmount
    FROM fin.Transactions t
    JOIN sec.tvf_AuthorizedBranches(@UserId) ab ON ab.BranchId = t.BranchId
    JOIN fin.TransactionTypes tt                ON tt.Id = t.TransactionTypeId
    CROSS APPLY (SELECT CASE WHEN @Granularity = 'MONTH'
                             THEN DATEFROMPARTS(YEAR(t.OperationDate), MONTH(t.OperationDate), 1)
                             ELSE t.OperationDate END AS PeriodStart) p
    WHERE t.OperationDate BETWEEN @DateFrom AND @DateTo
      AND (@BranchId IS NULL OR t.BranchId = @BranchId)
    GROUP BY p.PeriodStart, tt.Id, tt.Code, tt.Name
    ORDER BY p.PeriodStart, tt.Id;
END;
