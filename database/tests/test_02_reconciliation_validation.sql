/* Motor de conciliación: validaciones de entrada, concurrencia y registro de fallas. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Err INT;

-- 50101 cuenta inexistente (no necesita datos)
BEGIN TRY
    EXEC rec.usp_RunReconciliation @AccountId = -1, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15';
    SET @Err = 0;
END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
EXEC test.usp_AssertEquals 50101, @Err, N'cuenta inexistente';
EXEC test.usp_Pass N'50101 cuenta inexistente';

-- Casos que necesitan una cuenta: cada uno en su propia transacción.
DECLARE @Cases TABLE (Id INT, PeriodFrom DATE, PeriodTo DATE, Tolerance TINYINT, Expected INT, Name NVARCHAR(100));
INSERT @Cases VALUES
    (1, '2026-11-15', '2026-11-01', 1, 50102, N'50102 período invertido'),
    (2, '2026-01-01', '2026-06-30', 1, 50103, N'50103 período mayor a 93 días'),
    (3, '2026-11-01', '2026-11-15', 6, 50105, N'50105 tolerancia mayor a 5 días'),
    (4, '2026-11-01', '2026-11-15', 1, 50104, N'50104 conciliación en curso para la cuenta');

DECLARE @i INT = 1, @From DATE, @To DATE, @Tol TINYINT, @Expected INT, @Name NVARCHAR(100);
DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;

WHILE @i <= 4
BEGIN
    SELECT @From = PeriodFrom, @To = PeriodTo, @Tol = Tolerance, @Expected = Expected, @Name = Name FROM @Cases WHERE Id = @i;

    BEGIN TRANSACTION;
    EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                                @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
    IF @i = 4
        INSERT rec.Reconciliations (AccountId, PeriodFrom, PeriodTo, DateToleranceDays, Status, StartedAtUtc)
        VALUES (@AccountId, '2026-11-01', '2026-11-15', 1, 'RUNNING', SYSUTCDATETIME());

    BEGIN TRY
        EXEC rec.usp_RunReconciliation @AccountId = @AccountId, @PeriodFrom = @From, @PeriodTo = @To, @DateToleranceDays = @Tol;
        SET @Err = 0;
    END TRY
    BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;

    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    EXEC test.usp_AssertEquals @Expected, @Err, @Name;
    EXEC test.usp_Pass @Name;
    SET @i += 1;
END;

-- Una ejecución RUNNING de hace más de 30 minutos se marca abandonada y no bloquea la nueva.
BEGIN TRANSACTION;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
INSERT rec.Reconciliations (AccountId, PeriodFrom, PeriodTo, DateToleranceDays, Status, StartedAtUtc)
VALUES (@AccountId, '2026-11-01', '2026-11-15', 1, 'RUNNING', DATEADD(MINUTE, -45, SYSUTCDATETIME()));
DECLARE @Stale INT = CAST(SCOPE_IDENTITY() AS INT), @Rid INT;

EXEC rec.usp_RunReconciliation @AccountId = @AccountId, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15',
     @ReconciliationId = @Rid OUTPUT;

DECLARE @StaleStatus VARCHAR(10) = (SELECT Status FROM rec.Reconciliations WHERE Id = @Stale);
DECLARE @NewStatus VARCHAR(10)   = (SELECT Status FROM rec.Reconciliations WHERE Id = @Rid);
ROLLBACK TRANSACTION;

EXEC test.usp_AssertEquals 'FAILED', @StaleStatus, N'ejecución abandonada';
EXEC test.usp_AssertEquals 'COMPLETED', @NewStatus, N'nueva ejecución';
EXEC test.usp_Pass N'ejecución abandonada no bloquea la cuenta';
