/*
    stg.usp_PurgeStaging · Borra filas de staging de ejecuciones ETL ya terminadas hace más de N días.
    La ejecuta el FinCore.Worker (Windows Service) una vez al día. Borra por lotes para no bloquear
    la tabla ni crecer el log de transacciones. El historial en etl.* se conserva.
*/
CREATE OR ALTER PROCEDURE stg.usp_PurgeStaging
    @RetentionDays  INT = 30,
    @BatchSize      INT = 5000
AS
BEGIN
    SET NOCOUNT ON;

    IF @RetentionDays < 1 OR @BatchSize < 1
        THROW 50204, N'La retención y el tamaño de lote deben ser mayores que cero.', 1;

    DECLARE @Cutoff DATETIME2(3) = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
    DECLARE @Deleted INT = 1, @Total INT = 0;

    WHILE @Deleted > 0
    BEGIN
        DELETE TOP (@BatchSize) s
        FROM stg.BankMovements s
        JOIN etl.ETLExecutions e ON e.Id = s.EtlExecutionId
        WHERE e.Status <> 'RUNNING'
          AND e.FinishedAtUtc < @Cutoff;

        SET @Deleted = @@ROWCOUNT;
        SET @Total += @Deleted;
    END;

    SELECT @Total AS RowsDeleted;
END;
