/* Operaciones: protecciones irreversibles del trigger y el historial de estados. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;
DECLARE @Tx BIGINT, @Err INT, @n INT;

-- Cada caso de error necesita su propia transacción (el THROW del trigger la deja no confirmable).
DECLARE @Case INT = 1, @Expected INT, @Name NVARCHAR(120);
WHILE @Case <= 5
BEGIN
    BEGIN TRANSACTION;
    EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                                @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
    EXEC test.usp_AddTransaction @AccountId, N'G-0001', 'ING', '2026-11-03', 100000, @Op, 1, @Tx OUTPUT;

    BEGIN TRY
        IF @Case = 1 BEGIN SET @Expected = 50011; SET @Name = N'50011 una operación no se elimina';
            DELETE fin.Transactions WHERE Id = @Tx; END;
        IF @Case = 2 BEGIN SET @Expected = 50010; SET @Name = N'50010 no cambia el valor de una procesada';
            UPDATE fin.Transactions SET Amount = 1 WHERE Id = @Tx; END;
        IF @Case = 3 BEGIN SET @Expected = 50010; SET @Name = N'50010 no cambia la referencia de una procesada';
            UPDATE fin.Transactions SET Reference = N'G-0002' WHERE Id = @Tx; END;
        IF @Case = 4 BEGIN SET @Expected = 50012; SET @Name = N'50012 una conciliada no vuelve a procesada';
            UPDATE fin.Transactions SET StatusId = 3, ReconciledAtUtc = SYSUTCDATETIME() WHERE Id = @Tx;
            UPDATE fin.Transactions SET StatusId = 2 WHERE Id = @Tx; END;
        IF @Case = 5 BEGIN SET @Expected = 50012; SET @Name = N'50012 una rechazada es final';
            UPDATE fin.Transactions SET StatusId = 4, RejectionReason = N'prueba' WHERE Id = @Tx;
            UPDATE fin.Transactions SET StatusId = 2 WHERE Id = @Tx; END;
        SET @Err = 0;
    END TRY
    BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;

    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    EXEC test.usp_AssertEquals @Expected, @Err, @Name;
    EXEC test.usp_Pass @Name;
    SET @Case += 1;
END;

-- Lo permitido: editar una pendiente, conciliar, revertir una conciliada; y el historial lo registra todo.
BEGIN TRANSACTION;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'G-0003', 'PAG', '2026-11-03', 100000, @Op, 0, @Tx OUTPUT;

UPDATE fin.Transactions SET Amount = 120000 WHERE Id = @Tx;   -- pendiente: se puede corregir

EXEC sys.sp_set_session_context @key = N'UserId', @value = @Sup;
EXEC sys.sp_set_session_context @key = N'CorrelationId', @value = N'corr-test-04';
UPDATE fin.Transactions SET StatusId = 2, ProcessedAtUtc = SYSUTCDATETIME() WHERE Id = @Tx;
UPDATE fin.Transactions SET StatusId = 3, ReconciledAtUtc = SYSUTCDATETIME() WHERE Id = @Tx;
UPDATE fin.Transactions SET StatusId = 5 WHERE Id = @Tx;       -- conciliada -> revertida: permitido
EXEC sys.sp_set_session_context @key = N'UserId', @value = NULL;
EXEC sys.sp_set_session_context @key = N'CorrelationId', @value = NULL;

DECLARE @Path NVARCHAR(100) = (SELECT STRING_AGG(CONCAT(ISNULL(CAST(FromStatusId AS NVARCHAR(2)), N'-'), N'>', ToStatusId), N' ')
                                      WITHIN GROUP (ORDER BY Id)
                               FROM fin.TransactionStatusHistory WHERE TransactionId = @Tx);
SELECT @n = COUNT(*) FROM fin.TransactionStatusHistory
WHERE TransactionId = @Tx AND FromStatusId IS NOT NULL AND ChangedByUserId = @Sup AND CorrelationId = N'corr-test-04';
DECLARE @Creator INT = (SELECT ChangedByUserId FROM fin.TransactionStatusHistory WHERE TransactionId = @Tx AND FromStatusId IS NULL);
DECLARE @Final TINYINT = (SELECT StatusId FROM fin.Transactions WHERE Id = @Tx);
ROLLBACK TRANSACTION;

EXEC test.usp_AssertEquals N'->1 1>2 2>3 3>5', @Path, N'recorrido de estados';
EXEC test.usp_AssertEquals 3, @n, N'cambios con usuario y correlación de la sesión';
EXEC test.usp_AssertEquals @Op, @Creator, N'el alta la registra el creador';
EXEC test.usp_AssertEquals 5, @Final, N'estado final';
EXEC test.usp_Pass N'transiciones permitidas e historial completo';

-- Restricciones de la tabla
DECLARE @Checks TABLE (Id INT, Name NVARCHAR(120));
INSERT @Checks VALUES (1, N'CK valor positivo'), (2, N'CK rechazo exige motivo'), (3, N'CK reversión exige original'),
                      (4, N'UX una sola reversión por operación'), (5, N'UQ referencia única por empresa');
SET @Case = 1;
WHILE @Case <= 5
BEGIN
    SELECT @Name = Name FROM @Checks WHERE Id = @Case;
    BEGIN TRANSACTION;
    EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                                @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
    EXEC test.usp_AddTransaction @AccountId, N'C-0001', 'EGR', '2026-11-03', 100000, @Op, 1, @Tx OUTPUT;
    BEGIN TRY
        IF @Case = 1 EXEC test.usp_AddTransaction @AccountId, N'C-0002', 'ING', '2026-11-03', 0, @Op, 0;
        IF @Case = 2 UPDATE fin.Transactions SET StatusId = 4 WHERE Id = @Tx;
        IF @Case = 3
            INSERT fin.Transactions (CompanyId, BranchId, AccountId, TransactionTypeId, StatusId, Reference, OperationDate,
                                     Amount, Direction, CreatedByUserId)
            SELECT CompanyId, BranchId, Id, 5, 2, N'C-0003', '2026-11-03', 100, 1, @Op
            FROM fin.Accounts WHERE Id = @AccountId;
        IF @Case = 4
        BEGIN
            INSERT fin.Transactions (CompanyId, BranchId, AccountId, TransactionTypeId, StatusId, Reference, OperationDate,
                                     Amount, Direction, OriginalTransactionId, CreatedByUserId)
            SELECT CompanyId, BranchId, AccountId, 5, 2, Ref, OperationDate, Amount, -Direction, Id, CreatedByUserId
            FROM fin.Transactions CROSS JOIN (VALUES (N'REV-A'), (N'REV-B')) r (Ref)
            WHERE Id = @Tx;
        END;
        IF @Case = 5 EXEC test.usp_AddTransaction @AccountId, N'C-0001', 'ING', '2026-11-04', 5000, @Op, 0;
        SET @Err = 0;
    END TRY
    BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    -- 547 = CHECK/FK, 2601 = índice único, 2627 = restricción UNIQUE
    IF @Err NOT IN (547, 2601, 2627)
    BEGIN
        DECLARE @Msg NVARCHAR(300) = CONCAT(N'FALLA: ', @Name, N' | error obtenido: ', @Err);
        THROW 50999, @Msg, 1;
    END;
    EXEC test.usp_Pass @Name;
    SET @Case += 1;
END;
