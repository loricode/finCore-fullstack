/* Etapa 6 · Identidad en Reporting Services: cuenta de Windows → usuario de FinCore (rpt.usp_ReportUsers). */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;

DECLARE @Admin INT;
INSERT sec.Users (CompanyId, UserName, Email, FullName, PasswordHash)
VALUES (@CompanyId, N'test.admin.rpt', N'test.admin.rpt@fincore.test', N'Admin de prueba', N'x');
SET @Admin = CAST(SCOPE_IDENTITY() AS INT);
INSERT sec.UserRoles (UserId, RoleId) VALUES (@Admin, 1);

INSERT sec.UserWindowsAccounts (UserId, WindowsAccount) VALUES
    (@Sup, N'TESTPC\supervisora'), (@Admin, N'TESTPC\admin'), (@Op, N'TESTPC\operador');

DECLARE @U TABLE (UserId INT, Label NVARCHAR(300));
DECLARE @n INT, @first INT;

-- Sin reports.viewas: solo el propio usuario (sin distinguir mayúsculas).
INSERT @U EXEC rpt.usp_ReportUsers @WindowsAccount = N'testpc\SUPERVISORA';
SELECT @n = COUNT(*), @first = MIN(UserId) FROM @U;
EXEC test.usp_AssertEquals 1, @n, N'la supervisora solo se ve a sí misma';
EXEC test.usp_AssertEquals @Sup, @first, N'resuelve la supervisora';
EXEC test.usp_Pass N'cuenta asociada sin reports.viewas';

-- Con reports.viewas (ADMIN): él primero y luego los demás con reports.read de su empresa (no el operador).
DELETE @U;
INSERT @U EXEC rpt.usp_ReportUsers @WindowsAccount = N'TESTPC\admin';
SELECT @n = COUNT(*) FROM @U;
SELECT TOP (1) @first = UserId FROM @U;
EXEC test.usp_AssertEquals 2, @n, N'admin + supervisora';
EXEC test.usp_AssertEquals @Admin, @first, N'el propio usuario va primero (valor por defecto)';
SELECT @n = COUNT(*) FROM @U WHERE UserId = @Op;
EXEC test.usp_AssertEquals 0, @n, N'el operador no tiene reports.read';
EXEC test.usp_Pass N'reports.viewas lista los usuarios de la empresa';

ROLLBACK TRANSACTION;

-- Formato inválido: una cuenta sin dominio la rechaza el CHECK (en su propia transacción: XACT_ABORT la condena).
DECLARE @Err INT = 0;
BEGIN TRANSACTION;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
BEGIN TRY INSERT sec.UserWindowsAccounts (UserId, WindowsAccount) VALUES (@None, N'sin-dominio'); END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
EXEC test.usp_AssertEquals 547, @Err, N'CHECK de formato DOMINIO\usuario';
EXEC test.usp_Pass N'formato de la cuenta';

-- Cuenta no asociada: 50305.
SET @Err = 0;
BEGIN TRY EXEC rpt.usp_ReportUsers @WindowsAccount = N'OTRO\desconocido'; END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
EXEC test.usp_AssertEquals 50305, @Err, N'cuenta sin asociar';
EXEC test.usp_Pass N'50305 cuenta de Windows sin asociar';

-- Usuario asociado sin reports.read: 50300.
BEGIN TRANSACTION;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
INSERT sec.UserWindowsAccounts (UserId, WindowsAccount) VALUES (@Op, N'TESTPC\operador');
SET @Err = 0;
BEGIN TRY EXEC rpt.usp_ReportUsers @WindowsAccount = N'TESTPC\operador'; END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
EXEC test.usp_AssertEquals 50300, @Err, N'operador sin reports.read';
EXEC test.usp_Pass N'50300 cuenta asociada sin permiso';
