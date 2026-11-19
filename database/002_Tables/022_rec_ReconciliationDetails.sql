/*
    rec.ReconciliationDetails · Resultado de comparar cada operación interna y cada movimiento bancario.
    DifferenceAmount = BankAmount - InternalAmount (ausente = 0).
    Las diferencias se resuelven a mano con nota y usuario; el resultado original no se modifica.
*/
SET NOCOUNT ON;

CREATE TABLE rec.ReconciliationDetails
(
    Id                     BIGINT IDENTITY(1,1) NOT NULL,
    ReconciliationId       INT                  NOT NULL,
    ResultTypeId           TINYINT              NOT NULL,
    TransactionId          BIGINT               NULL,
    BankStatementDetailId  BIGINT               NULL,
    InternalAmount         DECIMAL(18,2)        NULL,
    BankAmount             DECIMAL(18,2)        NULL,
    DifferenceAmount       DECIMAL(18,2)        NOT NULL CONSTRAINT DF_ReconciliationDetails_Difference DEFAULT (0),
    Notes                  NVARCHAR(250)        NULL,
    ResolvedAtUtc          DATETIME2(3)         NULL,
    ResolvedByUserId       INT                  NULL,
    ResolutionNote         NVARCHAR(500)        NULL,
    CONSTRAINT PK_ReconciliationDetails PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ReconciliationDetails_Reconciliations FOREIGN KEY (ReconciliationId) REFERENCES rec.Reconciliations (Id),
    CONSTRAINT FK_ReconciliationDetails_ResultTypes FOREIGN KEY (ResultTypeId) REFERENCES rec.ReconciliationResultTypes (Id),
    CONSTRAINT FK_ReconciliationDetails_Transactions FOREIGN KEY (TransactionId) REFERENCES fin.Transactions (Id),
    CONSTRAINT FK_ReconciliationDetails_BankDetails FOREIGN KEY (BankStatementDetailId) REFERENCES rec.BankStatementDetails (Id),
    CONSTRAINT FK_ReconciliationDetails_ResolvedBy FOREIGN KEY (ResolvedByUserId) REFERENCES sec.Users (Id),
    CONSTRAINT CK_ReconciliationDetails_Side CHECK (TransactionId IS NOT NULL OR BankStatementDetailId IS NOT NULL),
    CONSTRAINT CK_ReconciliationDetails_Resolution CHECK (
        (ResolvedAtUtc IS NULL AND ResolvedByUserId IS NULL AND ResolutionNote IS NULL)
        OR (ResolvedAtUtc IS NOT NULL AND ResolvedByUserId IS NOT NULL AND ResolutionNote IS NOT NULL))
);
