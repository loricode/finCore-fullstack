/* sec.Companies · Empresas (tenant). El modelo es multiempresa aunque el MVP usa una sola. */
SET NOCOUNT ON;

CREATE TABLE sec.Companies
(
    Id            INT IDENTITY(1,1) NOT NULL,
    TaxId         NVARCHAR(20)      NOT NULL,   -- NIT
    Name          NVARCHAR(150)     NOT NULL,
    IsActive      BIT               NOT NULL CONSTRAINT DF_Companies_IsActive DEFAULT (1),
    CreatedAtUtc  DATETIME2(3)      NOT NULL CONSTRAINT DF_Companies_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Companies PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Companies_TaxId UNIQUE (TaxId)
);
