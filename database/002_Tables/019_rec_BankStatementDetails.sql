/*
    rec.BankStatementDetails · Movimientos bancarios ya validados.
    - Amount tiene signo: positivo = abono a la cuenta, negativo = cargo.
    - (AccountId, ExternalMovementId) es único: cargar dos veces el mismo extracto no duplica movimientos.
    - BankReference NO es única: si el banco repite una referencia, la conciliación la reporta como duplicada.
*/
SET NOCOUNT ON;

CREATE TABLE rec.BankStatementDetails
(
    Id                    BIGINT IDENTITY(1,1) NOT NULL,
    BankStatementId       INT                  NOT NULL,
    AccountId             INT                  NOT NULL,
    ExternalMovementId    NVARCHAR(60)         NOT NULL,
    BankReference         NVARCHAR(40)         NULL,
    MovementDate          DATE                 NOT NULL,
    Amount                DECIMAL(18,2)        NOT NULL,
    Description           NVARCHAR(250)        NULL,
    MatchStatus           VARCHAR(10)          NOT NULL CONSTRAINT DF_BankStatementDetails_MatchStatus DEFAULT ('UNMATCHED'),
    MatchedTransactionId  BIGINT               NULL,
    MatchedAtUtc          DATETIME2(3)         NULL,
    CreatedAtUtc          DATETIME2(3)         NOT NULL CONSTRAINT DF_BankStatementDetails_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_BankStatementDetails PRIMARY KEY CLUSTERED (Id),
    -- Compuesta: el movimiento pertenece a la misma cuenta que su extracto.
    CONSTRAINT FK_BankStatementDetails_Statements FOREIGN KEY (BankStatementId, AccountId) REFERENCES rec.BankStatements (Id, AccountId),
    CONSTRAINT FK_BankStatementDetails_Transactions FOREIGN KEY (MatchedTransactionId) REFERENCES fin.Transactions (Id),
    CONSTRAINT UQ_BankStatementDetails_Account_External UNIQUE (AccountId, ExternalMovementId),
    CONSTRAINT CK_BankStatementDetails_Amount CHECK (Amount <> 0),
    CONSTRAINT CK_BankStatementDetails_MatchStatus CHECK (MatchStatus IN ('UNMATCHED', 'MATCHED', 'IGNORED')),
    CONSTRAINT CK_BankStatementDetails_Matched CHECK (
        (MatchStatus = 'MATCHED' AND MatchedTransactionId IS NOT NULL AND MatchedAtUtc IS NOT NULL)
        OR (MatchStatus <> 'MATCHED' AND MatchedTransactionId IS NULL))
);
