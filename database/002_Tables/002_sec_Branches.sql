/* sec.Branches · Sucursales de una empresa. */
SET NOCOUNT ON;

CREATE TABLE sec.Branches
(
    Id            INT IDENTITY(1,1) NOT NULL,
    CompanyId     INT               NOT NULL,
    Code          NVARCHAR(10)      NOT NULL,
    Name          NVARCHAR(100)     NOT NULL,
    City          NVARCHAR(80)      NOT NULL,
    IsActive      BIT               NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT (1),
    CreatedAtUtc  DATETIME2(3)      NOT NULL CONSTRAINT DF_Branches_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Branches PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Branches_Companies FOREIGN KEY (CompanyId) REFERENCES sec.Companies (Id),
    CONSTRAINT UQ_Branches_Company_Code UNIQUE (CompanyId, Code),
    -- Clave alterna: permite que las tablas hijas garanticen que la sucursal es de la misma empresa.
    CONSTRAINT UQ_Branches_Id_Company UNIQUE (Id, CompanyId)
);
