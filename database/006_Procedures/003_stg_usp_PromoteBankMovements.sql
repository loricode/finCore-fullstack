/*
    stg.usp_PromoteBankMovements · Valida los movimientos crudos de una ejecución ETL y carga los válidos.

    Para cada fila de stg.BankMovements de la ejecución:
      1. Convierte y normaliza (fecha ISO, monto con punto decimal, referencia en mayúsculas).
      2. Valida, en este orden, y se queda con el primer error:
           MISSING_MOVEMENT_ID · INVALID_MOVEMENT_ID · ACCOUNT_NOT_FOUND · INVALID_REFERENCE ·
           INVALID_DATE · INVALID_AMOUNT · DUPLICATE_IN_FILE · ALREADY_LOADED
      3. Registra cada rechazo en etl.ETLExecutionDetails.
      4. Crea un rec.BankStatements por cuenta y carga los movimientos válidos en rec.BankStatementDetails.
      5. Cierra la ejecución con los conteos: RowsRead = RowsLoaded + RowsRejected.

    Es idempotente frente a recargas: un movimiento ya cargado se rechaza como ALREADY_LOADED.
    Todo ocurre en una transacción; si algo falla, la ejecución queda FAILED con el error.

    Errores: 50202 la ejecución no existe o ya terminó.
*/
CREATE OR ALTER PROCEDURE stg.usp_PromoteBankMovements
    @EtlExecutionId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CompanyId INT, @SourceSystem VARCHAR(10), @SourceName NVARCHAR(260);

    SELECT @CompanyId = CompanyId, @SourceSystem = SourceSystem, @SourceName = SourceName
    FROM etl.ETLExecutions
    WHERE Id = @EtlExecutionId AND Status = 'RUNNING';

    IF @CompanyId IS NULL
        THROW 50202, N'La ejecución ETL no existe o ya terminó.', 1;

    CREATE TABLE #Rows
    (
        RowNumber           INT            NOT NULL PRIMARY KEY,
        AccountId           INT            NULL,
        ExternalMovementId  NVARCHAR(100)  COLLATE DATABASE_DEFAULT NULL,
        BankReference       NVARCHAR(40)   COLLATE DATABASE_DEFAULT NULL,
        BankReferenceLength INT            NULL,
        MovementDate       DATE           NULL,
        AmountRaw           DECIMAL(38,10) NULL,
        Amount              DECIMAL(18,2)  NULL,
        Description         NVARCHAR(250)  COLLATE DATABASE_DEFAULT NULL,
        ErrorCode           VARCHAR(30)    NULL,
        ErrorMessage        NVARCHAR(500)  COLLATE DATABASE_DEFAULT NULL
    );

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Conversión y normalización
        INSERT #Rows (RowNumber, AccountId, ExternalMovementId, BankReference, BankReferenceLength, MovementDate, AmountRaw, Amount, Description)
        SELECT
            s.RowNumber,
            a.Id,
            NULLIF(TRIM(s.ExternalMovementId), N''),
            rec.fn_NormalizeReference(s.BankReference),
            LEN(TRIM(s.BankReference)),
            TRY_CONVERT(DATE, TRIM(s.MovementDate), 23),
            TRY_CONVERT(DECIMAL(38,10), TRIM(s.Amount)),
            TRY_CONVERT(DECIMAL(18,2), TRY_CONVERT(DECIMAL(38,10), TRIM(s.Amount))),
            LEFT(NULLIF(TRIM(s.Description), N''), 250)
        FROM stg.BankMovements s
        LEFT JOIN fin.Accounts a
               ON a.CompanyId = @CompanyId
              AND a.BankAccountNumber = TRIM(s.AccountNumber)
              AND a.IsActive = 1
        WHERE s.EtlExecutionId = @EtlExecutionId;

        -- 2. Validaciones (cada UPDATE solo toca filas que aún no tienen error)
        UPDATE #Rows SET ErrorCode = 'MISSING_MOVEMENT_ID', ErrorMessage = N'El movimiento no tiene identificador del banco.'
        WHERE ErrorCode IS NULL AND ExternalMovementId IS NULL;

        UPDATE #Rows SET ErrorCode = 'INVALID_MOVEMENT_ID', ErrorMessage = N'El identificador del banco supera 60 caracteres.'
        WHERE ErrorCode IS NULL AND LEN(ExternalMovementId) > 60;

        UPDATE #Rows SET ErrorCode = 'ACCOUNT_NOT_FOUND', ErrorMessage = N'La cuenta bancaria no existe o está inactiva en la empresa.'
        WHERE ErrorCode IS NULL AND AccountId IS NULL;

        UPDATE #Rows SET ErrorCode = 'INVALID_REFERENCE', ErrorMessage = N'La referencia bancaria supera 40 caracteres.'
        WHERE ErrorCode IS NULL AND BankReferenceLength > 40;

        UPDATE #Rows SET ErrorCode = 'INVALID_DATE', ErrorMessage = N'La fecha no tiene el formato yyyy-MM-dd.'
        WHERE ErrorCode IS NULL AND MovementDate IS NULL;

        UPDATE #Rows SET ErrorCode = 'INVALID_AMOUNT',
                         ErrorMessage = N'El monto debe ser numérico, distinto de cero y con máximo 2 decimales.'
        WHERE ErrorCode IS NULL
          AND (AmountRaw IS NULL OR Amount IS NULL OR Amount = 0 OR AmountRaw <> Amount);

        WITH Ranked AS
        (
            SELECT ErrorCode, ErrorMessage,
                   ROW_NUMBER() OVER (PARTITION BY AccountId, ExternalMovementId ORDER BY RowNumber) AS rn
            FROM #Rows
            WHERE ErrorCode IS NULL
        )
        UPDATE Ranked SET ErrorCode = 'DUPLICATE_IN_FILE', ErrorMessage = N'El movimiento aparece más de una vez en el origen.'
        WHERE rn > 1;

        UPDATE r SET ErrorCode = 'ALREADY_LOADED', ErrorMessage = N'El movimiento ya fue cargado en una ejecución anterior.'
        FROM #Rows r
        WHERE r.ErrorCode IS NULL
          AND EXISTS (SELECT 1 FROM rec.BankStatementDetails d
                      WHERE d.AccountId = r.AccountId AND d.ExternalMovementId = r.ExternalMovementId);

        -- 3. Rechazos
        INSERT etl.ETLExecutionDetails (EtlExecutionId, RowNumber, SourceKey, Severity, ErrorCode, ErrorMessage)
        SELECT @EtlExecutionId, RowNumber, LEFT(ExternalMovementId, 100), 'ERROR', ErrorCode, ErrorMessage
        FROM #Rows
        WHERE ErrorCode IS NOT NULL;

        -- 4. Un extracto por cuenta con sus movimientos válidos
        DECLARE @Statements TABLE (BankStatementId INT NOT NULL, AccountId INT NOT NULL PRIMARY KEY);

        INSERT rec.BankStatements (AccountId, Source, PeriodFrom, PeriodTo, FileName, EtlExecutionId, LineCount)
        OUTPUT inserted.Id, inserted.AccountId INTO @Statements (BankStatementId, AccountId)
        SELECT AccountId, @SourceSystem, MIN(MovementDate), MAX(MovementDate), @SourceName, @EtlExecutionId, COUNT(*)
        FROM #Rows
        WHERE ErrorCode IS NULL
        GROUP BY AccountId;

        INSERT rec.BankStatementDetails (BankStatementId, AccountId, ExternalMovementId, BankReference, MovementDate, Amount, Description)
        SELECT st.BankStatementId, r.AccountId, r.ExternalMovementId, r.BankReference, r.MovementDate, r.Amount, r.Description
        FROM #Rows r
        JOIN @Statements st ON st.AccountId = r.AccountId
        WHERE r.ErrorCode IS NULL;

        -- 5. Cierre de la ejecución
        DECLARE @Read INT     = (SELECT COUNT(*) FROM #Rows);
        DECLARE @Rejected INT = (SELECT COUNT(*) FROM #Rows WHERE ErrorCode IS NOT NULL);

        UPDATE etl.ETLExecutions
        SET Status        = CASE WHEN @Rejected > 0 THEN 'COMPLETED_WITH_ERRORS' ELSE 'COMPLETED' END,
            FinishedAtUtc = SYSUTCDATETIME(),
            RowsRead      = @Read,
            RowsLoaded    = @Read - @Rejected,
            RowsRejected  = @Rejected
        WHERE Id = @EtlExecutionId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

        DECLARE @Error NVARCHAR(2000) =
            LEFT(CONCAT(N'Error ', ERROR_NUMBER(), N' en ', ERROR_PROCEDURE(), N' línea ', ERROR_LINE(), N': ', ERROR_MESSAGE()), 2000);
        EXEC etl.usp_FailExecution @EtlExecutionId = @EtlExecutionId, @ErrorMessage = @Error;

        THROW;
    END CATCH;

    SELECT Id AS EtlExecutionId, Status, RowsRead, RowsLoaded, RowsRejected
    FROM etl.ETLExecutions
    WHERE Id = @EtlExecutionId;
END;
