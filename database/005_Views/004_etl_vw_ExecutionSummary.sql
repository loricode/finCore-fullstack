/* etl.vw_ExecutionSummary · Ejecuciones ETL con duración y los códigos de error más frecuentes. */
CREATE OR ALTER VIEW etl.vw_ExecutionSummary
AS
SELECT
    e.Id               AS EtlExecutionId,
    e.CompanyId,
    e.PackageName,
    e.SourceSystem,
    e.SourceName,
    e.Status,
    e.StartedAtUtc,
    e.FinishedAtUtc,
    DATEDIFF(MILLISECOND, e.StartedAtUtc, e.FinishedAtUtc) AS DurationMs,
    e.RowsRead,
    e.RowsLoaded,
    e.RowsRejected,
    CAST(CASE WHEN e.RowsRead = 0 THEN 0 ELSE 100.0 * e.RowsRejected / e.RowsRead END AS DECIMAL(5,2)) AS RejectionRatePct,
    errs.ErrorCodes,
    e.ErrorMessage,
    e.TriggeredBy,
    e.CorrelationId
FROM etl.ETLExecutions e
OUTER APPLY
(
    SELECT STRING_AGG(CONCAT(x.ErrorCode, N' (', x.Total, N')'), N', ') WITHIN GROUP (ORDER BY x.Total DESC) AS ErrorCodes
    FROM (
        SELECT d.ErrorCode, COUNT(*) AS Total
        FROM etl.ETLExecutionDetails d
        WHERE d.EtlExecutionId = e.Id
        GROUP BY d.ErrorCode
    ) x
) errs;
