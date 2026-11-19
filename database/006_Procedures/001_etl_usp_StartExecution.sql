/*
    etl.usp_StartExecution · Abre una ejecución ETL y devuelve su Id.
    Es lo primero que hace el paquete SSIS (Execute SQL Task); el Id viaja como variable del paquete
    y se usa para marcar las filas que se cargan en stg.BankMovements.

    Errores: 50201 empresa inexistente o inactiva · 50203 origen inválido.
*/
CREATE OR ALTER PROCEDURE etl.usp_StartExecution
    @CompanyId       INT,
    @PackageName     NVARCHAR(100),
    @SourceSystem    VARCHAR(10),
    @SourceName      NVARCHAR(260) = NULL,
    @TriggeredBy     NVARCHAR(100),
    @CorrelationId   NVARCHAR(64)  = NULL,
    @EtlExecutionId  BIGINT        OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM sec.Companies WHERE Id = @CompanyId AND IsActive = 1)
        THROW 50201, N'La empresa no existe o está inactiva.', 1;

    IF @SourceSystem NOT IN ('ORACLE', 'CSV')
        THROW 50203, N'El origen debe ser ORACLE o CSV.', 1;

    INSERT etl.ETLExecutions (CompanyId, PackageName, SourceSystem, SourceName, Status, TriggeredBy, CorrelationId)
    VALUES (@CompanyId, @PackageName, @SourceSystem, @SourceName, 'RUNNING', @TriggeredBy, @CorrelationId);

    SET @EtlExecutionId = CAST(SCOPE_IDENTITY() AS BIGINT);
END;
