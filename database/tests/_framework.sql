/*
    Mini framework de pruebas T-SQL. Lo aplica run-tests.ps1 antes de las pruebas; NO se despliega
    en producción (vive en el esquema test y deploy.ps1 no lo conoce).

    Convención de cada prueba (tests/test_*.sql):
      - BEGIN TRANSACTION -> preparar datos con los fixtures -> ejecutar -> afirmar -> ROLLBACK.
      - Una afirmación fallida lanza el error 50999; sqlcmd -b termina con código 1 y la conexión
        cerrada revierte la transacción abierta. La base queda como estaba.
      - Para esperar un error de negocio: capturarlo en TRY/CATCH, hacer ROLLBACK y afirmar el número
        (con XACT_ABORT un THROW deja la transacción no confirmable, así que cada caso va en su propia transacción).
*/
SET NOCOUNT ON;
IF SCHEMA_ID(N'test') IS NULL EXEC (N'CREATE SCHEMA test AUTHORIZATION dbo;');
GO

CREATE OR ALTER PROCEDURE test.usp_AssertEquals
    @Expected  SQL_VARIANT,
    @Actual    SQL_VARIANT,
    @Message   NVARCHAR(300)
AS
BEGIN
    IF (@Expected IS NULL AND @Actual IS NULL) RETURN;

    -- Se compara como texto. Las fechas se llevan a ISO 8601 (yyyy-MM-ddTHH:mm:ss) para no depender del idioma
    -- de la sesión: así una prueba puede escribir el valor esperado como '2026-11-03'.
    DECLARE @E NVARCHAR(4000), @A NVARCHAR(4000);
    IF CONVERT(SYSNAME, SQL_VARIANT_PROPERTY(@Actual, 'BaseType')) IN (N'date', N'datetime', N'datetime2', N'smalldatetime')
    BEGIN
        SET @A = CONVERT(NVARCHAR(4000), CONVERT(DATETIME2(3), @Actual), 126);
        SET @E = CONVERT(NVARCHAR(4000), TRY_CONVERT(DATETIME2(3), CONVERT(NVARCHAR(4000), @Expected)), 126);
    END
    ELSE
    BEGIN
        SET @A = CONVERT(NVARCHAR(4000), @Actual);
        SET @E = CONVERT(NVARCHAR(4000), @Expected);
    END;

    IF (@E IS NULL OR @A IS NULL OR @E <> @A)
    BEGIN
        DECLARE @Text NVARCHAR(2048) = CONCAT(N'FALLA: ', @Message, N' | esperado: ', ISNULL(@E, N'NULL'),
                                              N' | obtenido: ', ISNULL(@A, N'NULL'));
        THROW 50999, @Text, 1;
    END;
END;
GO

CREATE OR ALTER PROCEDURE test.usp_Pass
    @TestName NVARCHAR(200)
AS
BEGIN
    PRINT CONCAT(N'  ok  ', @TestName);
END;
GO

/*  Crea una empresa aislada con dos sucursales, una cuenta en la primera y tres usuarios:
    operador (solo sucursal 1), supervisor (todas) y un usuario sin roles.  */
CREATE OR ALTER PROCEDURE test.usp_CreateFixture
    @CompanyId      INT OUTPUT,
    @BranchId       INT OUTPUT,
    @OtherBranchId  INT OUTPUT,
    @AccountId      INT OUTPUT,
    @OperatorId     INT OUTPUT,
    @SupervisorId   INT OUTPUT,
    @NoRoleUserId   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @s NVARCHAR(8) = LEFT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N''), 8);

    INSERT sec.Companies (TaxId, Name) VALUES (CONCAT(N'T-', @s), CONCAT(N'Empresa de prueba ', @s));
    SET @CompanyId = CAST(SCOPE_IDENTITY() AS INT);

    INSERT sec.Branches (CompanyId, Code, Name, City) VALUES (@CompanyId, N'T1', N'Sucursal prueba 1', N'Montería');
    SET @BranchId = CAST(SCOPE_IDENTITY() AS INT);
    INSERT sec.Branches (CompanyId, Code, Name, City) VALUES (@CompanyId, N'T2', N'Sucursal prueba 2', N'Bogotá');
    SET @OtherBranchId = CAST(SCOPE_IDENTITY() AS INT);

    INSERT sec.Users (CompanyId, UserName, Email, FullName, PasswordHash)
    VALUES (@CompanyId, CONCAT(N'op.', @s), CONCAT(N'op.', @s, N'@test.local'), N'Operador prueba', N'x');
    SET @OperatorId = CAST(SCOPE_IDENTITY() AS INT);
    INSERT sec.Users (CompanyId, UserName, Email, FullName, PasswordHash)
    VALUES (@CompanyId, CONCAT(N'sup.', @s), CONCAT(N'sup.', @s, N'@test.local'), N'Supervisor prueba', N'x');
    SET @SupervisorId = CAST(SCOPE_IDENTITY() AS INT);
    INSERT sec.Users (CompanyId, UserName, Email, FullName, PasswordHash)
    VALUES (@CompanyId, CONCAT(N'none.', @s), CONCAT(N'none.', @s, N'@test.local'), N'Sin roles', N'x');
    SET @NoRoleUserId = CAST(SCOPE_IDENTITY() AS INT);

    INSERT sec.UserRoles (UserId, RoleId) VALUES (@OperatorId, 3), (@SupervisorId, 2);
    INSERT sec.UserBranches (UserId, BranchId, CompanyId) VALUES (@OperatorId, @BranchId, @CompanyId);

    INSERT fin.Accounts (CompanyId, BranchId, Code, Name, BankName, BankAccountNumber)
    VALUES (@CompanyId, @BranchId, N'T1-001', N'Cuenta prueba', N'Banco prueba', CONCAT(N'TEST-', @s));
    SET @AccountId = CAST(SCOPE_IDENTITY() AS INT);
END;
GO

/*  Registra una operación como lo haría la API: la crea Pendiente y, si se pide, la procesa.
    @TypeCode ING | EGR | TRF | PAG. @Process = 1 la deja Procesada.  */
CREATE OR ALTER PROCEDURE test.usp_AddTransaction
    @AccountId      INT,
    @Reference      NVARCHAR(40),
    @TypeCode       VARCHAR(10),
    @OperationDate  DATE,
    @Amount         DECIMAL(18,2),
    @CreatedBy      INT,
    @Process        BIT = 1,
    @TransactionId  BIGINT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT fin.Transactions (CompanyId, BranchId, AccountId, TransactionTypeId, StatusId, Reference, OperationDate,
                             Amount, Direction, CreatedByUserId)
    SELECT a.CompanyId, a.BranchId, a.Id, tt.Id, 1, @Reference, @OperationDate, @Amount, tt.DefaultDirection, @CreatedBy
    FROM fin.Accounts a
    CROSS JOIN fin.TransactionTypes tt
    WHERE a.Id = @AccountId AND tt.Code = @TypeCode;

    SET @TransactionId = CAST(SCOPE_IDENTITY() AS BIGINT);

    IF @Process = 1
        UPDATE fin.Transactions
        SET StatusId = 2, ProcessedAtUtc = SYSUTCDATETIME(), UpdatedByUserId = @CreatedBy, UpdatedAtUtc = SYSUTCDATETIME()
        WHERE Id = @TransactionId;
END;
GO

/*  Crea un extracto vacío para la cuenta y devuelve su Id.  */
CREATE OR ALTER PROCEDURE test.usp_AddStatement
    @AccountId    INT,
    @StatementId  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT rec.BankStatements (AccountId, Source, PeriodFrom, PeriodTo) VALUES (@AccountId, 'CSV', '2026-11-01', '2026-11-30');
    SET @StatementId = CAST(SCOPE_IDENTITY() AS INT);
END;
GO
