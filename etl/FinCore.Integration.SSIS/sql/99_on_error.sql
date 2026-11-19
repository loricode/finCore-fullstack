-- Manejador OnError del paquete (Execute SQL Task, OLE DB). Parámetros:
--   0 Input User::EtlExecutionId     (LARGE_INTEGER)
--   1 Input System::ErrorDescription (VARCHAR)
-- Si la falla ocurre antes de promover (p. ej. Oracle no responde), la ejecución queda FAILED con el error.
EXEC etl.usp_FailExecution @EtlExecutionId = ?, @ErrorMessage = ?;
