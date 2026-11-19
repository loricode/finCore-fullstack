/* etl.ETLExecutionDetails · Registros rechazados o con advertencias en una ejecución ETL. */
SET NOCOUNT ON;

CREATE TABLE etl.ETLExecutionDetails
(
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    EtlExecutionId  BIGINT               NOT NULL,
    RowNumber       INT                  NOT NULL,   -- número de fila en el origen
    SourceKey       NVARCHAR(100)        NULL,       -- id del movimiento en el origen, si se pudo leer
    Severity        VARCHAR(10)          NOT NULL,
    ErrorCode       VARCHAR(30)          NOT NULL,
    ErrorMessage    NVARCHAR(500)        NOT NULL,
    CreatedAtUtc    DATETIME2(3)         NOT NULL CONSTRAINT DF_ETLExecutionDetails_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ETLExecutionDetails PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ETLExecutionDetails_Executions FOREIGN KEY (EtlExecutionId) REFERENCES etl.ETLExecutions (Id),
    CONSTRAINT CK_ETLExecutionDetails_Severity CHECK (Severity IN ('ERROR', 'WARNING'))
);
