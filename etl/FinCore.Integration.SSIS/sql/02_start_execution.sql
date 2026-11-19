-- Execute SQL Task "Abrir ejecución ETL" (OLE DB). Parámetros (Parameter Mapping):
--   0 Input  User::CompanyId      (LONG)
--   1 Input  System::PackageName  (VARCHAR)
--   2 Input  User::TriggeredBy    (VARCHAR)  p. ej. 'sql-agent'
--   3 Output User::EtlExecutionId (LARGE_INTEGER)
EXEC etl.usp_StartExecution
     @CompanyId      = ?,
     @PackageName    = ?,
     @SourceSystem   = 'ORACLE',
     @SourceName     = N'ORACLE:LEGACY_BANK.BANK_MOVEMENTS',
     @TriggeredBy    = ?,
     @EtlExecutionId = ? OUTPUT;
