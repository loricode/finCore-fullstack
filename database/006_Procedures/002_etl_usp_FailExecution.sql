/*
    etl.usp_FailExecution · Marca una ejecución ETL como fallida.
    La llama el manejador de errores (OnError) del paquete SSIS cuando la falla ocurre antes
    de promover los datos (por ejemplo, Oracle no responde).
*/
CREATE OR ALTER PROCEDURE etl.usp_FailExecution
    @EtlExecutionId  BIGINT,
    @ErrorMessage    NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE etl.ETLExecutions
    SET Status        = 'FAILED',
        FinishedAtUtc = SYSUTCDATETIME(),
        ErrorMessage  = LEFT(@ErrorMessage, 2000)
    WHERE Id = @EtlExecutionId
      AND Status = 'RUNNING';
END;
