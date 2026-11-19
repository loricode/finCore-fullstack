/* rec.BankStatements · Extracto bancario: un lote de movimientos de una cuenta y su origen. */
SET NOCOUNT ON;

CREATE TABLE rec.BankStatements
(
    Id                INT IDENTITY(1,1) NOT NULL,
    AccountId         INT               NOT NULL,
    Source            VARCHAR(10)       NOT NULL,   -- ORACLE | CSV | WEBHOOK
    PeriodFrom        DATE              NOT NULL,
    PeriodTo          DATE              NOT NULL,
    FileName          NVARCHAR(260)     NULL,
    EtlExecutionId    BIGINT            NULL,
    LineCount         INT               NOT NULL CONSTRAINT DF_BankStatements_LineCount DEFAULT (0),
    ImportedAtUtc     DATETIME2(3)      NOT NULL CONSTRAINT DF_BankStatements_ImportedAtUtc DEFAULT (SYSUTCDATETIME()),
    ImportedByUserId  INT               NULL,
    CONSTRAINT PK_BankStatements PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_BankStatements_Accounts FOREIGN KEY (AccountId) REFERENCES fin.Accounts (Id),
    CONSTRAINT FK_BankStatements_Executions FOREIGN KEY (EtlExecutionId) REFERENCES etl.ETLExecutions (Id),
    CONSTRAINT FK_BankStatements_Users FOREIGN KEY (ImportedByUserId) REFERENCES sec.Users (Id),
    CONSTRAINT UQ_BankStatements_Id_Account UNIQUE (Id, AccountId),
    CONSTRAINT CK_BankStatements_Source CHECK (Source IN ('ORACLE', 'CSV', 'WEBHOOK')),
    CONSTRAINT CK_BankStatements_Period CHECK (PeriodTo >= PeriodFrom),
    CONSTRAINT CK_BankStatements_LineCount CHECK (LineCount >= 0)
);
