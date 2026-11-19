/*
    sec.UserWindowsAccounts · Cuenta de Windows (DOMINIO\usuario) con la que un usuario de FinCore abre los informes de SSRS.
    SSRS autentica con Windows y entrega la cuenta en User!UserID; rpt.usp_ReportUsers la traduce al usuario de FinCore.
    Una cuenta de Windows corresponde a un solo usuario y cada usuario tiene como máximo una cuenta.
    La intercalación de la base no distingue mayúsculas: "laptop\ana" y "LAPTOP\ana" son la misma cuenta.
*/
SET NOCOUNT ON;

CREATE TABLE sec.UserWindowsAccounts
(
    UserId            INT           NOT NULL,
    WindowsAccount    NVARCHAR(256) NOT NULL,
    AssignedAtUtc     DATETIME2(3)  NOT NULL CONSTRAINT DF_UserWindowsAccounts_AssignedAtUtc DEFAULT (SYSUTCDATETIME()),
    AssignedByUserId  INT           NULL,
    CONSTRAINT PK_UserWindowsAccounts PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT UQ_UserWindowsAccounts_Account UNIQUE (WindowsAccount),
    CONSTRAINT FK_UserWindowsAccounts_Users FOREIGN KEY (UserId) REFERENCES sec.Users (Id),
    CONSTRAINT FK_UserWindowsAccounts_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES sec.Users (Id),
    -- DOMINIO\usuario: una sola barra, con texto a ambos lados y sin espacios en los extremos.
    CONSTRAINT CK_UserWindowsAccounts_Format CHECK (
        WindowsAccount LIKE N'_%\_%'
        AND LEN(WindowsAccount) - LEN(REPLACE(WindowsAccount, N'\', N'')) = 1
        AND WindowsAccount = LTRIM(RTRIM(WindowsAccount)))
);
