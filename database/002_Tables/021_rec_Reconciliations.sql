/*
    rec.Reconciliations · Una ejecución del motor de conciliación para una cuenta y un período.
    La fila se crea en RUNNING antes de empezar, de modo que una falla queda registrada (FAILED + ErrorMessage).
*/
SET NOCOUNT ON;

CREATE TABLE rec.Reconciliations
(
    Id                 INT IDENTITY(1,1) NOT NULL,
    AccountId          INT               NOT NULL,
    PeriodFrom         DATE              NOT NULL,
    PeriodTo           DATE              NOT NULL,
    DateToleranceDays  TINYINT           NOT NULL,
    Status             VARCHAR(10)       NOT NULL,
    StartedAtUtc       DATETIME2(3)      NOT NULL,
    FinishedAtUtc      DATETIME2(3)      NULL,
    ExecutedByUserId   INT               NULL,
    ExecutedBy         NVARCHAR(100)     NULL,   -- 'api', 'fincore-worker'
    CorrelationId      NVARCHAR(64)      NULL,
    InternalCount      INT               NOT NULL CONSTRAINT DF_Reconciliations_InternalCount DEFAULT (0),
    BankCount          INT               NOT NULL CONSTRAINT DF_Reconciliations_BankCount DEFAULT (0),
    MatchedCount       INT               NOT NULL CONSTRAINT DF_Reconciliations_MatchedCount DEFAULT (0),
    DifferenceCount    INT               NOT NULL CONSTRAINT DF_Reconciliations_DifferenceCount DEFAULT (0),
    ErrorMessage       NVARCHAR(2000)    NULL,
    CONSTRAINT PK_Reconciliations PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Reconciliations_Accounts FOREIGN KEY (AccountId) REFERENCES fin.Accounts (Id),
    CONSTRAINT FK_Reconciliations_Users FOREIGN KEY (ExecutedByUserId) REFERENCES sec.Users (Id),
    CONSTRAINT CK_Reconciliations_Period CHECK (PeriodTo >= PeriodFrom),
    CONSTRAINT CK_Reconciliations_Status CHECK (Status IN ('RUNNING', 'COMPLETED', 'FAILED')),
    CONSTRAINT CK_Reconciliations_Finished CHECK (Status = 'RUNNING' OR FinishedAtUtc IS NOT NULL)
);
