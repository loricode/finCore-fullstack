/*
    fin.Transactions · Operaciones financieras registradas por las sucursales.

    - Amount siempre es positivo; Direction indica el sentido (1 entra, -1 sale) y SignedAmount
      es el valor con signo, comparable directamente con el extracto bancario.
    - Las llaves foráneas compuestas garantizan que la cuenta es de la sucursal y la sucursal de la empresa.
    - Una operación nunca se elimina ni cambia sus datos después de procesada: fin.trg_Transactions_Guard.
    - Las transiciones de estado son una regla del dominio (.NET); la base solo protege lo irreversible.
*/
SET NOCOUNT ON;

CREATE TABLE fin.Transactions
(
    Id                     BIGINT IDENTITY(1,1) NOT NULL,
    PublicId               UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_Transactions_PublicId DEFAULT (NEWSEQUENTIALID()),
    CompanyId              INT                  NOT NULL,
    BranchId               INT                  NOT NULL,
    AccountId              INT                  NOT NULL,
    TransactionTypeId      TINYINT              NOT NULL,
    StatusId               TINYINT              NOT NULL,
    Reference              NVARCHAR(40)         NOT NULL,
    OperationDate          DATE                 NOT NULL,   -- fecha de negocio (hora de Colombia)
    Amount                 DECIMAL(18,2)        NOT NULL,
    Direction              SMALLINT             NOT NULL,
    SignedAmount           AS CAST(Amount * Direction AS DECIMAL(18,2)) PERSISTED,
    Currency               CHAR(3)              NOT NULL CONSTRAINT DF_Transactions_Currency DEFAULT ('COP'),
    Description            NVARCHAR(250)        NULL,
    CounterpartyName       NVARCHAR(150)        NULL,
    CounterpartyAccount    NVARCHAR(40)         NULL,
    OriginalTransactionId  BIGINT               NULL,       -- solo en reversiones
    RejectionReason        NVARCHAR(250)        NULL,
    Source                 VARCHAR(10)          NOT NULL CONSTRAINT DF_Transactions_Source DEFAULT ('MANUAL'),
    IdempotencyKey         NVARCHAR(64)         NULL,
    CreatedByUserId        INT                  NOT NULL,
    CreatedAtUtc           DATETIME2(3)         NOT NULL CONSTRAINT DF_Transactions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedByUserId        INT                  NULL,
    UpdatedAtUtc           DATETIME2(3)         NULL,
    ProcessedAtUtc         DATETIME2(3)         NULL,
    ReconciledAtUtc        DATETIME2(3)         NULL,
    RowVersion             ROWVERSION           NOT NULL,
    CONSTRAINT PK_Transactions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Transactions_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Transactions_Company_Reference UNIQUE (CompanyId, Reference),
    CONSTRAINT FK_Transactions_Branches FOREIGN KEY (BranchId, CompanyId) REFERENCES sec.Branches (Id, CompanyId),
    CONSTRAINT FK_Transactions_Accounts FOREIGN KEY (AccountId, BranchId) REFERENCES fin.Accounts (Id, BranchId),
    CONSTRAINT FK_Transactions_Types FOREIGN KEY (TransactionTypeId) REFERENCES fin.TransactionTypes (Id),
    CONSTRAINT FK_Transactions_Status FOREIGN KEY (StatusId) REFERENCES fin.TransactionStatus (Id),
    CONSTRAINT FK_Transactions_Original FOREIGN KEY (OriginalTransactionId) REFERENCES fin.Transactions (Id),
    CONSTRAINT FK_Transactions_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES sec.Users (Id),
    CONSTRAINT FK_Transactions_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES sec.Users (Id),
    CONSTRAINT CK_Transactions_Amount CHECK (Amount > 0 AND Amount <= 5000000000),
    CONSTRAINT CK_Transactions_Direction CHECK (Direction IN (1, -1)),
    CONSTRAINT CK_Transactions_Currency CHECK (Currency = 'COP'),
    CONSTRAINT CK_Transactions_Reference CHECK (LEN(Reference) >= 4 AND Reference = TRIM(Reference)),
    CONSTRAINT CK_Transactions_Source CHECK (Source IN ('MANUAL', 'WEBHOOK', 'IMPORT')),
    -- Tipo 5 = reversión: es la única que referencia a una operación original.
    CONSTRAINT CK_Transactions_Reversal CHECK (
        (TransactionTypeId = 5 AND OriginalTransactionId IS NOT NULL)
        OR (TransactionTypeId <> 5 AND OriginalTransactionId IS NULL)),
    -- Estado 4 = rechazada: exige motivo.
    CONSTRAINT CK_Transactions_Rejection CHECK (StatusId <> 4 OR RejectionReason IS NOT NULL),
    -- Estado 3 = conciliada: exige fecha de conciliación.
    CONSTRAINT CK_Transactions_Reconciled CHECK (StatusId <> 3 OR ReconciledAtUtc IS NOT NULL)
);
