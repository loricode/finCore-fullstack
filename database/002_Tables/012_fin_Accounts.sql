/*
    fin.Accounts · Cuentas financieras de una sucursal. Cada una está asociada a una cuenta
    bancaria real (BankAccountNumber), que es la que se concilia contra los extractos.
*/
SET NOCOUNT ON;

CREATE TABLE fin.Accounts
(
    Id                 INT IDENTITY(1,1) NOT NULL,
    CompanyId          INT               NOT NULL,
    BranchId           INT               NOT NULL,
    Code               NVARCHAR(20)      NOT NULL,
    Name               NVARCHAR(100)     NOT NULL,
    BankName           NVARCHAR(100)     NOT NULL,
    BankAccountNumber  NVARCHAR(40)      NOT NULL,
    Currency           CHAR(3)           NOT NULL CONSTRAINT DF_Accounts_Currency DEFAULT ('COP'),
    IsActive           BIT               NOT NULL CONSTRAINT DF_Accounts_IsActive DEFAULT (1),
    CreatedAtUtc       DATETIME2(3)      NOT NULL CONSTRAINT DF_Accounts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId    INT               NULL,
    RowVersion         ROWVERSION        NOT NULL,
    CONSTRAINT PK_Accounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Accounts_Branches FOREIGN KEY (BranchId, CompanyId) REFERENCES sec.Branches (Id, CompanyId),
    CONSTRAINT FK_Accounts_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES sec.Users (Id),
    CONSTRAINT UQ_Accounts_Company_Code UNIQUE (CompanyId, Code),
    CONSTRAINT UQ_Accounts_Company_BankAccount UNIQUE (CompanyId, BankAccountNumber),
    CONSTRAINT UQ_Accounts_Id_Branch UNIQUE (Id, BranchId),
    CONSTRAINT CK_Accounts_Currency CHECK (Currency = 'COP')   -- MVP monomoneda
);
