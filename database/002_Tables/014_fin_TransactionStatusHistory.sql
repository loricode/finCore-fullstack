/*
    fin.TransactionStatusHistory · Cada cambio de estado de una operación.
    La llena el trigger fin.trg_Transactions_StatusHistory: la base registra el historial
    aunque el cambio no venga de la API (por ejemplo, del motor de conciliación).
*/
SET NOCOUNT ON;

CREATE TABLE fin.TransactionStatusHistory
(
    Id               BIGINT IDENTITY(1,1) NOT NULL,
    TransactionId    BIGINT               NOT NULL,
    FromStatusId     TINYINT              NULL,       -- NULL en la creación
    ToStatusId       TINYINT              NOT NULL,
    ChangedAtUtc     DATETIME2(3)         NOT NULL,
    ChangedByUserId  INT                  NULL,
    CorrelationId    NVARCHAR(64)         NULL,
    Reason           NVARCHAR(250)        NULL,
    CONSTRAINT PK_TransactionStatusHistory PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TransactionStatusHistory_Transactions FOREIGN KEY (TransactionId) REFERENCES fin.Transactions (Id),
    CONSTRAINT FK_TransactionStatusHistory_From FOREIGN KEY (FromStatusId) REFERENCES fin.TransactionStatus (Id),
    CONSTRAINT FK_TransactionStatusHistory_To FOREIGN KEY (ToStatusId) REFERENCES fin.TransactionStatus (Id),
    CONSTRAINT FK_TransactionStatusHistory_Users FOREIGN KEY (ChangedByUserId) REFERENCES sec.Users (Id)
);
