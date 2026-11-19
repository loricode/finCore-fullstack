/*
    fin.TransactionTypes · Tipos de operación. Ids fijos (enum TransactionType en el dominio).
    DefaultDirection: 1 entra dinero a la cuenta, -1 sale.
    Es NULL en la reversión, que toma el sentido contrario al de la operación original.
*/
SET NOCOUNT ON;

CREATE TABLE fin.TransactionTypes
(
    Id                    TINYINT      NOT NULL,
    Code                  VARCHAR(10)  NOT NULL,
    Name                  NVARCHAR(50) NOT NULL,
    DefaultDirection      SMALLINT     NULL,
    RequiresCounterparty  BIT          NOT NULL,
    CONSTRAINT PK_TransactionTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_TransactionTypes_Code UNIQUE (Code),
    CONSTRAINT CK_TransactionTypes_Direction CHECK (DefaultDirection IN (1, -1))
);
