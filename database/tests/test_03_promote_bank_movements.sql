/* ETL: validación y carga desde staging, conteos de la ejecución e idempotencia ante recargas. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
DECLARE @Bank NVARCHAR(40) = (SELECT BankAccountNumber FROM fin.Accounts WHERE Id = @AccountId);

DECLARE @Exec BIGINT;
EXEC etl.usp_StartExecution @CompanyId = @CompanyId, @PackageName = N'test.dtsx', @SourceSystem = 'ORACLE',
     @TriggeredBy = N'test', @EtlExecutionId = @Exec OUTPUT;

INSERT stg.BankMovements (EtlExecutionId, RowNumber, AccountNumber, ExternalMovementId, BankReference, MovementDate, Amount)
VALUES (@Exec, 1,  @Bank,        N'M1',  N'  ref-1 ',  N'2026-11-03', N'-500000.00'),  -- válida; referencia normalizada
       (@Exec, 2,  CONCAT(N' ', @Bank, N' '), N'M2', N'REF-2', N'2026-11-04', N'1200000'), -- válida; cuenta con espacios
       (@Exec, 3,  @Bank,        NULL,   N'REF-3',     N'2026-11-05', N'100.00'),      -- MISSING_MOVEMENT_ID
       (@Exec, 4,  N'NO-EXISTE', N'M4',  N'REF-4',     N'2026-11-05', N'100.00'),      -- ACCOUNT_NOT_FOUND
       (@Exec, 5,  @Bank,        N'M5',  REPLICATE(N'X', 41), N'2026-11-05', N'100.00'), -- INVALID_REFERENCE
       (@Exec, 6,  @Bank,        N'M6',  N'REF-6',     N'05/11/2026', N'100.00'),      -- INVALID_DATE
       (@Exec, 7,  @Bank,        N'M7',  N'REF-7',     N'2026-11-05', N'1.200.000'),   -- INVALID_AMOUNT (miles con punto)
       (@Exec, 8,  @Bank,        N'M8',  N'REF-8',     N'2026-11-05', N'0'),           -- INVALID_AMOUNT (cero)
       (@Exec, 9,  @Bank,        N'M9',  N'REF-9',     N'2026-11-05', N'10.555'),      -- INVALID_AMOUNT (3 decimales)
       (@Exec, 10, @Bank,        N'M1',  N'REF-1',     N'2026-11-03', N'-500000.00'),  -- DUPLICATE_IN_FILE
       (@Exec, 11, @Bank,        N'M11', NULL,         N'2026-11-09', N'-12500.50');   -- válida sin referencia

EXEC stg.usp_PromoteBankMovements @EtlExecutionId = @Exec;

DECLARE @Status VARCHAR(25), @Read INT, @Loaded INT, @Rejected INT;
SELECT @Status = Status, @Read = RowsRead, @Loaded = RowsLoaded, @Rejected = RowsRejected FROM etl.ETLExecutions WHERE Id = @Exec;
EXEC test.usp_AssertEquals 'COMPLETED_WITH_ERRORS', @Status, N'estado';
EXEC test.usp_AssertEquals 11, @Read, N'leídas';
EXEC test.usp_AssertEquals 3, @Loaded, N'cargadas';
EXEC test.usp_AssertEquals 8, @Rejected, N'rechazadas';
EXEC test.usp_Pass N'conteos: leídas = cargadas + rechazadas';

DECLARE @Codes NVARCHAR(400) = (SELECT STRING_AGG(CONCAT(RowNumber, N':', ErrorCode), N',') WITHIN GROUP (ORDER BY RowNumber)
                                FROM etl.ETLExecutionDetails WHERE EtlExecutionId = @Exec);
EXEC test.usp_AssertEquals N'3:MISSING_MOVEMENT_ID,4:ACCOUNT_NOT_FOUND,5:INVALID_REFERENCE,6:INVALID_DATE,7:INVALID_AMOUNT,8:INVALID_AMOUNT,9:INVALID_AMOUNT,10:DUPLICATE_IN_FILE',
     @Codes, N'código de error por fila';
EXEC test.usp_Pass N'cada rechazo con su código';

DECLARE @Ref NVARCHAR(40) = (SELECT BankReference FROM rec.BankStatementDetails WHERE AccountId = @AccountId AND ExternalMovementId = N'M1');
EXEC test.usp_AssertEquals N'REF-1', @Ref, N'referencia normalizada';
DECLARE @PFrom DATE, @PTo DATE, @Lines INT;
SELECT @PFrom = PeriodFrom, @PTo = PeriodTo, @Lines = LineCount FROM rec.BankStatements WHERE EtlExecutionId = @Exec;
EXEC test.usp_AssertEquals '2026-11-03', @PFrom, N'inicio del extracto';
EXEC test.usp_AssertEquals '2026-11-09', @PTo, N'fin del extracto';
EXEC test.usp_AssertEquals 3, @Lines, N'líneas del extracto';
EXEC test.usp_Pass N'extracto creado con período y movimientos normalizados';

-- Recargar el mismo origen no duplica movimientos.
DECLARE @Exec2 BIGINT;
EXEC etl.usp_StartExecution @CompanyId = @CompanyId, @PackageName = N'test.dtsx', @SourceSystem = 'ORACLE',
     @TriggeredBy = N'test', @EtlExecutionId = @Exec2 OUTPUT;
INSERT stg.BankMovements (EtlExecutionId, RowNumber, AccountNumber, ExternalMovementId, BankReference, MovementDate, Amount)
VALUES (@Exec2, 1, @Bank, N'M1', N'REF-1', N'2026-11-03', N'-500000.00'),
       (@Exec2, 2, @Bank, N'M2', N'REF-2', N'2026-11-04', N'1200000');
EXEC stg.usp_PromoteBankMovements @EtlExecutionId = @Exec2;

DECLARE @Already INT = (SELECT COUNT(*) FROM etl.ETLExecutionDetails WHERE EtlExecutionId = @Exec2 AND ErrorCode = 'ALREADY_LOADED');
DECLARE @Movs INT = (SELECT COUNT(*) FROM rec.BankStatementDetails WHERE AccountId = @AccountId);
DECLARE @Stmts INT = (SELECT COUNT(*) FROM rec.BankStatements WHERE EtlExecutionId = @Exec2);
EXEC test.usp_AssertEquals 2, @Already, N'ALREADY_LOADED';
EXEC test.usp_AssertEquals 3, @Movs, N'movimientos sin duplicar';
EXEC test.usp_AssertEquals 0, @Stmts, N'sin filas válidas no se crea extracto';
EXEC test.usp_Pass N'recarga idempotente';

ROLLBACK TRANSACTION;

-- Promover una ejecución ya terminada falla con 50202.
DECLARE @Err INT;
BEGIN TRANSACTION;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
EXEC etl.usp_StartExecution @CompanyId = @CompanyId, @PackageName = N'test.dtsx', @SourceSystem = 'CSV',
     @TriggeredBy = N'test', @EtlExecutionId = @Exec OUTPUT;
EXEC etl.usp_FailExecution @EtlExecutionId = @Exec, @ErrorMessage = N'Oracle no responde';
BEGIN TRY
    EXEC stg.usp_PromoteBankMovements @EtlExecutionId = @Exec;
    SET @Err = 0;
END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
EXEC test.usp_AssertEquals 50202, @Err, N'promover una ejecución terminada';
EXEC test.usp_Pass N'50202 ejecución ya terminada';
