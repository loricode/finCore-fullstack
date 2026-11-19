/* rec.ReconciliationResultTypes · Resultados posibles de comparar una operación con el banco. Ids fijos. */
SET NOCOUNT ON;

CREATE TABLE rec.ReconciliationResultTypes
(
    Id              TINYINT       NOT NULL,
    Code            VARCHAR(30)   NOT NULL,
    Name            NVARCHAR(80)  NOT NULL,
    Description     NVARCHAR(250) NOT NULL,
    RequiresAction  BIT           NOT NULL,   -- 1 = alguien debe revisarlo
    CONSTRAINT PK_ReconciliationResultTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ReconciliationResultTypes_Code UNIQUE (Code)
);
