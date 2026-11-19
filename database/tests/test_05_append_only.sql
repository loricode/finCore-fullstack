/* Auditoría e historial de estados son de solo inserción. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Err INT, @Case INT = 1, @Expected INT, @Name NVARCHAR(120);
DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT, @Tx BIGINT;

WHILE @Case <= 4
BEGIN
    BEGIN TRANSACTION;
    INSERT audit.AuditLogs (UserName, Action, EntityName, EntityId, NewValues, CorrelationId)
    VALUES (N'test', 'CREATE', N'Transaction', N'1', N'{"amount":100}', N'corr-test-05');

    BEGIN TRY
        IF @Case = 1 BEGIN SET @Expected = 50090; SET @Name = N'50090 la auditoría no se modifica';
            UPDATE audit.AuditLogs SET Action = 'UPDATE' WHERE CorrelationId = N'corr-test-05'; END;
        IF @Case = 2 BEGIN SET @Expected = 50090; SET @Name = N'50090 la auditoría no se elimina';
            DELETE audit.AuditLogs WHERE CorrelationId = N'corr-test-05'; END;
        IF @Case IN (3, 4)
        BEGIN
            EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                                        @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
            EXEC test.usp_AddTransaction @AccountId, N'A-0001', 'ING', '2026-11-03', 1000, @Op, 1, @Tx OUTPUT;
            SET @Expected = 50091;
            IF @Case = 3 BEGIN SET @Name = N'50091 el historial no se modifica';
                UPDATE fin.TransactionStatusHistory SET ToStatusId = 3 WHERE TransactionId = @Tx; END;
            IF @Case = 4 BEGIN SET @Name = N'50091 el historial no se elimina';
                DELETE fin.TransactionStatusHistory WHERE TransactionId = @Tx; END;
        END;
        SET @Err = 0;
    END TRY
    BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;

    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    EXEC test.usp_AssertEquals @Expected, @Err, @Name;
    EXEC test.usp_Pass @Name;
    SET @Case += 1;
END;

-- Los valores de auditoría deben ser JSON válido.
BEGIN TRANSACTION;
BEGIN TRY
    INSERT audit.AuditLogs (UserName, Action, EntityName, NewValues) VALUES (N'test', 'CREATE', N'X', N'no es json');
    SET @Err = 0;
END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
EXEC test.usp_AssertEquals 547, @Err, N'CK JSON en auditoría';
EXEC test.usp_Pass N'auditoría exige JSON válido';
