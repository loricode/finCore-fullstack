/*
    stg.BankMovements · Movimientos bancarios crudos, tal como llegan de Oracle o de un CSV.
    Todas las columnas de datos son texto a propósito: la validación y conversión la hace
    stg.usp_PromoteBankMovements, que registra cada rechazo en etl.ETLExecutionDetails.
    Formatos esperados: MovementDate yyyy-MM-dd · Amount con punto decimal y signo (-500000.00).
*/
SET NOCOUNT ON;

CREATE TABLE stg.BankMovements
(
    EtlExecutionId      BIGINT         NOT NULL,
    RowNumber           INT            NOT NULL,
    AccountNumber       NVARCHAR(60)   NULL,
    ExternalMovementId  NVARCHAR(100)  NULL,
    BankReference       NVARCHAR(100)  NULL,
    MovementDate        NVARCHAR(30)   NULL,
    Amount              NVARCHAR(40)   NULL,
    Description         NVARCHAR(400)  NULL,
    LoadedAtUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_BankMovements_LoadedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_BankMovements PRIMARY KEY CLUSTERED (EtlExecutionId, RowNumber),
    CONSTRAINT FK_BankMovements_Executions FOREIGN KEY (EtlExecutionId) REFERENCES etl.ETLExecutions (Id)
);
