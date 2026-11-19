/*
    etl.ETLExecutions · Una fila por ejecución de un proceso de carga (paquete SSIS o importación CSV).
    Responde a soporte: cuándo corrió, de dónde leyó, cuántos registros leyó, cargó y rechazó, y por qué falló.
*/
SET NOCOUNT ON;

CREATE TABLE etl.ETLExecutions
(
    Id             BIGINT IDENTITY(1,1) NOT NULL,
    CompanyId      INT                  NOT NULL,
    PackageName    NVARCHAR(100)        NOT NULL,   -- p. ej. ImportOracleBankMovements.dtsx
    SourceSystem   VARCHAR(10)          NOT NULL,   -- ORACLE | CSV
    SourceName     NVARCHAR(260)        NULL,       -- archivo o tabla de origen
    Status         VARCHAR(25)          NOT NULL CONSTRAINT DF_ETLExecutions_Status DEFAULT ('RUNNING'),
    StartedAtUtc   DATETIME2(3)         NOT NULL CONSTRAINT DF_ETLExecutions_StartedAtUtc DEFAULT (SYSUTCDATETIME()),
    FinishedAtUtc  DATETIME2(3)         NULL,
    RowsRead       INT                  NOT NULL CONSTRAINT DF_ETLExecutions_RowsRead DEFAULT (0),
    RowsLoaded     INT                  NOT NULL CONSTRAINT DF_ETLExecutions_RowsLoaded DEFAULT (0),
    RowsRejected   INT                  NOT NULL CONSTRAINT DF_ETLExecutions_RowsRejected DEFAULT (0),
    ErrorMessage   NVARCHAR(2000)       NULL,
    TriggeredBy    NVARCHAR(100)        NOT NULL,   -- usuario, 'sql-agent', 'fincore-worker'
    CorrelationId  NVARCHAR(64)         NULL,
    CONSTRAINT PK_ETLExecutions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ETLExecutions_Companies FOREIGN KEY (CompanyId) REFERENCES sec.Companies (Id),
    CONSTRAINT CK_ETLExecutions_SourceSystem CHECK (SourceSystem IN ('ORACLE', 'CSV')),
    CONSTRAINT CK_ETLExecutions_Status CHECK (Status IN ('RUNNING', 'COMPLETED', 'COMPLETED_WITH_ERRORS', 'FAILED')),
    CONSTRAINT CK_ETLExecutions_Rows CHECK (RowsRead >= 0 AND RowsLoaded >= 0 AND RowsRejected >= 0),
    -- Al terminar bien, cada registro leído quedó cargado o rechazado: ninguno se pierde.
    CONSTRAINT CK_ETLExecutions_Balance CHECK (
        Status NOT IN ('COMPLETED', 'COMPLETED_WITH_ERRORS') OR RowsRead = RowsLoaded + RowsRejected),
    CONSTRAINT CK_ETLExecutions_Finished CHECK (Status = 'RUNNING' OR FinishedAtUtc IS NOT NULL)
);
