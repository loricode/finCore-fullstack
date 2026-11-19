/* fin.TransactionStatus · Estados de una operación. Ids fijos (enum TransactionStatus en el dominio). */
SET NOCOUNT ON;

CREATE TABLE fin.TransactionStatus
(
    Id       TINYINT      NOT NULL,
    Code     VARCHAR(20)  NOT NULL,
    Name     NVARCHAR(50) NOT NULL,
    IsFinal  BIT          NOT NULL,
    CONSTRAINT PK_TransactionStatus PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_TransactionStatus_Code UNIQUE (Code)
);
