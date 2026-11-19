/*
    DEMO · Empresa, sucursales, usuarios y cuentas. Solo se aplica con deploy.ps1 -IncludeDemo.
    Todos los usuarios tienen la contraseña de demostración  FinCore*2026  (hash PBKDF2-SHA512 de
    ASP.NET Core Identity v3, 100.000 iteraciones). Nunca usar estos datos fuera de desarrollo.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @DemoHash NVARCHAR(256) = N'AQAAAAIAAYagAAAAEJWxUVQ55mS6NySdQMgoNUQedJKQuabs8zg4cumT1g5oROeNffZ+v5AVbqUD/nd5yA==';

INSERT sec.Companies (TaxId, Name) VALUES (N'900123456-7', N'FinCore Demo S.A.S.');
DECLARE @CompanyId INT = CAST(SCOPE_IDENTITY() AS INT);

INSERT sec.Branches (CompanyId, Code, Name, City)
VALUES (@CompanyId, N'MON', N'Sucursal Montería',     N'Montería'),
       (@CompanyId, N'BOG', N'Sucursal Bogotá',       N'Bogotá'),
       (@CompanyId, N'MED', N'Sucursal Medellín',     N'Medellín'),
       (@CompanyId, N'BAQ', N'Sucursal Barranquilla', N'Barranquilla');

INSERT sec.Users (CompanyId, UserName, Email, FullName, PasswordHash)
VALUES (@CompanyId, N'admin',             N'admin@fincore.demo',             N'Administrador FinCore',   @DemoHash),
       (@CompanyId, N'supervisor',        N'supervisor@fincore.demo',        N'Laura Supervisora',       @DemoHash),
       (@CompanyId, N'operador.monteria', N'operador.monteria@fincore.demo', N'Carlos Operador Montería', @DemoHash),
       (@CompanyId, N'operador.bogota',   N'operador.bogota@fincore.demo',   N'Ana Operadora Bogotá',    @DemoHash),
       (@CompanyId, N'auditor',           N'auditor@fincore.demo',           N'Marta Auditora',          @DemoHash),
       (@CompanyId, N'svc.integration',   N'svc.integration@fincore.demo',   N'Cuenta de integración',   @DemoHash);

DECLARE @AdminId INT = (SELECT Id FROM sec.Users WHERE UserName = N'admin');

INSERT sec.UserRoles (UserId, RoleId, AssignedByUserId)
SELECT u.Id, r.Id, @AdminId
FROM (VALUES (N'admin', 'ADMIN'), (N'supervisor', 'SUPERVISOR'), (N'operador.monteria', 'OPERATOR'),
             (N'operador.bogota', 'OPERATOR'), (N'auditor', 'AUDITOR'), (N'svc.integration', 'INTEGRATION')) x (UserName, RoleCode)
JOIN sec.Users u ON u.UserName = x.UserName
JOIN sec.Roles r ON r.Code = x.RoleCode;

-- Los operadores solo ven su sucursal. Admin, supervisor, auditor e integración ven todas (branches.all).
INSERT sec.UserBranches (UserId, BranchId, CompanyId, AssignedByUserId)
SELECT u.Id, b.Id, @CompanyId, @AdminId
FROM (VALUES (N'operador.monteria', N'MON'), (N'operador.bogota', N'BOG')) x (UserName, BranchCode)
JOIN sec.Users u    ON u.UserName = x.UserName
JOIN sec.Branches b ON b.Code = x.BranchCode AND b.CompanyId = @CompanyId;

INSERT fin.Accounts (CompanyId, BranchId, Code, Name, BankName, BankAccountNumber, CreatedByUserId)
SELECT @CompanyId, b.Id, x.Code, x.Name, x.BankName, x.BankAccountNumber, @AdminId
FROM (VALUES
    (N'MON', N'MON-001', N'Recaudo Montería',     N'Bancolombia',     N'123-456789-01'),
    (N'BOG', N'BOG-001', N'Recaudo Bogotá',       N'Banco de Bogotá', N'987-654321-02'),
    (N'MED', N'MED-001', N'Recaudo Medellín',     N'Davivienda',      N'555-000111-03'),
    (N'BAQ', N'BAQ-001', N'Recaudo Barranquilla', N'BBVA',            N'444-222333-04')
) x (BranchCode, Code, Name, BankName, BankAccountNumber)
JOIN sec.Branches b ON b.Code = x.BranchCode AND b.CompanyId = @CompanyId;

COMMIT TRANSACTION;
