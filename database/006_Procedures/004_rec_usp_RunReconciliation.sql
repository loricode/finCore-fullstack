/*
    rec.usp_RunReconciliation · Motor de conciliación bancaria de una cuenta en un período.

    Compara las operaciones internas con los movimientos del banco por REFERENCIA, VALOR y FECHA.

    Entradas evaluadas
      Internas  : operaciones de la cuenta con OperationDate en [PeriodFrom, PeriodTo] que están
                  Procesadas (2), o Revertidas (5) sin conciliar todavía.
      Pendientes: operaciones Pendientes (1) del período: no se concilian, se reportan para revisión.
      Banco     : movimientos UNMATCHED de la cuenta con fecha en [PeriodFrom - tolerancia, PeriodTo + tolerancia].
                  Los que caen fuera del período solo sirven para emparejar; si no emparejan se ignoran
                  (se evaluarán en la conciliación de su propio período).

    Reglas, en orden (cada elemento recibe un solo resultado)
      6 DUPLICATE_REFERENCE  la referencia bancaria aparece en más de un movimiento de la cuenta.
      1 MATCHED              misma referencia, mismo valor con signo y fecha dentro de la tolerancia.
      2 AMOUNT_MISMATCH      misma referencia, valor distinto.
      3 DATE_MISMATCH        misma referencia y valor, fecha fuera de la tolerancia.
      7 PENDING_REVIEW       operación pendiente (con o sin movimiento bancario que la respalde).
      4 MISSING_IN_BANK      operación interna sin movimiento bancario.
      5 MISSING_INTERNAL     movimiento bancario del período sin operación interna.

    Efectos
      - MATCHED: la operación pasa a Conciliada (3), o conserva Revertida (5) con ReconciledAtUtc;
        el movimiento queda MATCHED y apunta a la operación. Es la única transición de estado que hace SQL.
      - Todos los resultados quedan en rec.ReconciliationDetails.
      - La cabecera se crea en RUNNING antes de empezar: si algo falla queda FAILED con el error.

    Concurrencia: una sola conciliación en curso por cuenta (validación + índice único filtrado),
    y UPDLOCK sobre las filas evaluadas para que nadie las cambie mientras se concilian.

    Errores: 50101 cuenta inexistente o inactiva · 50102 período inválido · 50103 período > 93 días ·
             50104 ya hay una conciliación en curso · 50105 tolerancia > 5 días.
*/
CREATE OR ALTER PROCEDURE rec.usp_RunReconciliation
    @AccountId          INT,
    @PeriodFrom         DATE,
    @PeriodTo           DATE,
    @DateToleranceDays  TINYINT       = 1,
    @ExecutedByUserId   INT           = NULL,
    @ExecutedBy         NVARCHAR(100) = NULL,
    @CorrelationId      NVARCHAR(64)  = NULL,
    @ReconciliationId   INT           = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    ------------------------------------------------------------------ validaciones
    IF NOT EXISTS (SELECT 1 FROM fin.Accounts WHERE Id = @AccountId AND IsActive = 1)
        THROW 50101, N'La cuenta no existe o está inactiva.', 1;

    IF @PeriodFrom IS NULL OR @PeriodTo IS NULL OR @PeriodFrom > @PeriodTo
        THROW 50102, N'El período es inválido: la fecha inicial debe ser menor o igual a la final.', 1;

    IF DATEDIFF(DAY, @PeriodFrom, @PeriodTo) > 92
        THROW 50103, N'El período de conciliación no puede superar 93 días.', 1;

    IF @DateToleranceDays IS NULL OR @DateToleranceDays > 5
        THROW 50105, N'La tolerancia de fechas debe estar entre 0 y 5 días.', 1;

    -- Una ejecución que lleva más de 30 minutos en curso se considera abandonada (proceso caído).
    UPDATE rec.Reconciliations
    SET Status = 'FAILED', FinishedAtUtc = SYSUTCDATETIME(),
        ErrorMessage = N'Ejecución abandonada: superó 30 minutos en curso.'
    WHERE AccountId = @AccountId AND Status = 'RUNNING'
      AND StartedAtUtc < DATEADD(MINUTE, -30, SYSUTCDATETIME());

    IF EXISTS (SELECT 1 FROM rec.Reconciliations WHERE AccountId = @AccountId AND Status = 'RUNNING')
        THROW 50104, N'Ya hay una conciliación en curso para esta cuenta.', 1;

    IF @CorrelationId IS NOT NULL
        EXEC sys.sp_set_session_context @key = N'CorrelationId', @value = @CorrelationId;

    ------------------------------------------------------------------ cabecera
    INSERT rec.Reconciliations (AccountId, PeriodFrom, PeriodTo, DateToleranceDays, Status, StartedAtUtc,
                                ExecutedByUserId, ExecutedBy, CorrelationId)
    VALUES (@AccountId, @PeriodFrom, @PeriodTo, @DateToleranceDays, 'RUNNING', SYSUTCDATETIME(),
            @ExecutedByUserId, @ExecutedBy, @CorrelationId);

    SET @ReconciliationId = CAST(SCOPE_IDENTITY() AS INT);

    DECLARE @Tolerance INT = @DateToleranceDays;   -- TINYINT no admite el signo negativo
    DECLARE @BankFrom DATE = DATEADD(DAY, -@Tolerance, @PeriodFrom);
    DECLARE @BankTo   DATE = DATEADD(DAY,  @Tolerance, @PeriodTo);

    CREATE TABLE #Internal
    (
        TransactionId  BIGINT        NOT NULL PRIMARY KEY,
        Reference      NVARCHAR(40)  COLLATE DATABASE_DEFAULT NOT NULL,
        OperationDate  DATE          NOT NULL,
        SignedAmount   DECIMAL(18,2) NOT NULL,
        Assigned       BIT           NOT NULL DEFAULT (0)
    );

    CREATE TABLE #Bank
    (
        BankDetailId  BIGINT        NOT NULL PRIMARY KEY,
        Reference     NVARCHAR(40)  COLLATE DATABASE_DEFAULT NULL,
        MovementDate  DATE          NOT NULL,
        Amount        DECIMAL(18,2) NOT NULL,
        InPeriod      BIT           NOT NULL,
        IsDuplicate   BIT           NOT NULL DEFAULT (0),
        Assigned      BIT           NOT NULL DEFAULT (0)
    );

    CREATE TABLE #Results
    (
        ResultTypeId    TINYINT       NOT NULL,
        TransactionId   BIGINT        NULL,
        BankDetailId    BIGINT        NULL,
        InternalAmount  DECIMAL(18,2) NULL,
        BankAmount      DECIMAL(18,2) NULL,
        Notes           NVARCHAR(250) COLLATE DATABASE_DEFAULT NULL
    );

    BEGIN TRY
        BEGIN TRANSACTION;

        ------------------------------------------------------------------ insumos
        INSERT #Internal (TransactionId, Reference, OperationDate, SignedAmount)
        SELECT t.Id, t.Reference, t.OperationDate, t.SignedAmount
        FROM fin.Transactions t WITH (UPDLOCK)
        WHERE t.AccountId = @AccountId
          AND t.OperationDate BETWEEN @PeriodFrom AND @PeriodTo
          AND (t.StatusId = 2 OR (t.StatusId = 5 AND t.ReconciledAtUtc IS NULL));

        INSERT #Bank (BankDetailId, Reference, MovementDate, Amount, InPeriod)
        SELECT d.Id, d.BankReference, d.MovementDate, d.Amount,
               CASE WHEN d.MovementDate BETWEEN @PeriodFrom AND @PeriodTo THEN 1 ELSE 0 END
        FROM rec.BankStatementDetails d WITH (UPDLOCK)
        WHERE d.AccountId = @AccountId
          AND d.MatchStatus = 'UNMATCHED'
          AND d.MovementDate BETWEEN @BankFrom AND @BankTo;

        ------------------------------------------------------------------ 6 DUPLICATE_REFERENCE
        -- Se compara contra todos los movimientos de la cuenta (incluso ya conciliados):
        -- un segundo cargo con la misma referencia es justamente lo que hay que detectar.
        UPDATE b SET IsDuplicate = 1
        FROM #Bank b
        WHERE b.Reference IS NOT NULL
          AND EXISTS (SELECT 1 FROM rec.BankStatementDetails o
                      WHERE o.AccountId = @AccountId
                        AND o.BankReference = b.Reference
                        AND o.Id <> b.BankDetailId
                        AND o.MatchStatus <> 'IGNORED');

        INSERT #Results (ResultTypeId, TransactionId, BankDetailId, InternalAmount, BankAmount, Notes)
        SELECT 6, t.Id, b.BankDetailId, t.SignedAmount, b.Amount,
               N'La referencia aparece en más de un movimiento bancario de la cuenta.'
        FROM #Bank b
        LEFT JOIN fin.Transactions t ON t.AccountId = @AccountId AND t.Reference = b.Reference
        WHERE b.IsDuplicate = 1;

        UPDATE i SET Assigned = 1
        FROM #Internal i
        WHERE EXISTS (SELECT 1 FROM #Bank b WHERE b.IsDuplicate = 1 AND b.Reference = i.Reference);

        UPDATE #Bank SET Assigned = 1 WHERE IsDuplicate = 1;

        ------------------------------------------------------------------ 1, 2, 3 por referencia
        -- Sin duplicados, cada referencia empareja como máximo un movimiento con una operación.
        INSERT #Results (ResultTypeId, TransactionId, BankDetailId, InternalAmount, BankAmount, Notes)
        SELECT
            CASE WHEN i.SignedAmount <> b.Amount THEN 2
                 WHEN ABS(DATEDIFF(DAY, i.OperationDate, b.MovementDate)) > @DateToleranceDays THEN 3
                 ELSE 1 END,
            i.TransactionId, b.BankDetailId, i.SignedAmount, b.Amount,
            CASE WHEN i.SignedAmount <> b.Amount
                     THEN N'El valor del banco no coincide con el de la operación.'
                 WHEN ABS(DATEDIFF(DAY, i.OperationDate, b.MovementDate)) > @DateToleranceDays
                     THEN CONCAT(N'La fecha difiere en ', ABS(DATEDIFF(DAY, i.OperationDate, b.MovementDate)),
                                 N' días (tolerancia: ', @DateToleranceDays, N').')
                 ELSE NULL END
        FROM #Internal i
        JOIN #Bank b ON b.Reference = i.Reference
        WHERE i.Assigned = 0 AND b.Assigned = 0;

        UPDATE i SET Assigned = 1 FROM #Internal i JOIN #Results r ON r.TransactionId = i.TransactionId;
        UPDATE b SET Assigned = 1 FROM #Bank b     JOIN #Results r ON r.BankDetailId  = b.BankDetailId;

        ------------------------------------------------------------------ 7 PENDING_REVIEW
        INSERT #Results (ResultTypeId, TransactionId, BankDetailId, InternalAmount, BankAmount, Notes)
        SELECT 7, t.Id, b.BankDetailId, t.SignedAmount, b.Amount,
               CASE WHEN b.BankDetailId IS NULL
                    THEN N'Operación pendiente de procesar.'
                    ELSE N'Operación pendiente de procesar; el banco ya registra el movimiento.' END
        FROM fin.Transactions t
        LEFT JOIN #Bank b ON b.Reference = t.Reference AND b.Assigned = 0
        WHERE t.AccountId = @AccountId
          AND t.StatusId = 1
          AND t.OperationDate BETWEEN @PeriodFrom AND @PeriodTo;

        UPDATE b SET Assigned = 1
        FROM #Bank b JOIN #Results r ON r.BankDetailId = b.BankDetailId AND r.ResultTypeId = 7;

        ------------------------------------------------------------------ 4 MISSING_IN_BANK
        INSERT #Results (ResultTypeId, TransactionId, InternalAmount, Notes)
        SELECT 4, i.TransactionId, i.SignedAmount, N'No hay un movimiento bancario con esta referencia.'
        FROM #Internal i
        WHERE i.Assigned = 0;

        ------------------------------------------------------------------ 5 MISSING_INTERNAL
        INSERT #Results (ResultTypeId, BankDetailId, BankAmount, Notes)
        SELECT 5, b.BankDetailId, b.Amount,
               CASE WHEN b.Reference IS NULL
                    THEN N'Movimiento bancario sin referencia y sin operación interna.'
                    ELSE N'No hay una operación interna con esta referencia.' END
        FROM #Bank b
        WHERE b.Assigned = 0 AND b.InPeriod = 1;

        ------------------------------------------------------------------ efectos
        UPDATE t
        SET StatusId        = CASE WHEN t.StatusId = 2 THEN 3 ELSE t.StatusId END,
            ReconciledAtUtc = SYSUTCDATETIME(),
            UpdatedAtUtc    = SYSUTCDATETIME(),
            UpdatedByUserId = COALESCE(@ExecutedByUserId, t.UpdatedByUserId)
        FROM fin.Transactions t
        JOIN #Results r ON r.TransactionId = t.Id AND r.ResultTypeId = 1;

        UPDATE d
        SET MatchStatus = 'MATCHED', MatchedTransactionId = r.TransactionId, MatchedAtUtc = SYSUTCDATETIME()
        FROM rec.BankStatementDetails d
        JOIN #Results r ON r.BankDetailId = d.Id AND r.ResultTypeId = 1;

        INSERT rec.ReconciliationDetails (ReconciliationId, ResultTypeId, TransactionId, BankStatementDetailId,
                                          InternalAmount, BankAmount, DifferenceAmount, Notes)
        SELECT @ReconciliationId, r.ResultTypeId, r.TransactionId, r.BankDetailId,
               r.InternalAmount, r.BankAmount, ISNULL(r.BankAmount, 0) - ISNULL(r.InternalAmount, 0), r.Notes
        FROM #Results r;

        UPDATE rec.Reconciliations
        SET Status          = 'COMPLETED',
            FinishedAtUtc   = SYSUTCDATETIME(),
            InternalCount   = (SELECT COUNT(*) FROM #Internal),
            BankCount       = (SELECT COUNT(*) FROM #Bank WHERE InPeriod = 1 OR Assigned = 1),
            MatchedCount    = (SELECT COUNT(*) FROM #Results WHERE ResultTypeId = 1),
            DifferenceCount = (SELECT COUNT(*) FROM #Results WHERE ResultTypeId <> 1)
        WHERE Id = @ReconciliationId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

        DECLARE @Error NVARCHAR(2000) =
            LEFT(CONCAT(N'Error ', ERROR_NUMBER(), N' en ', ERROR_PROCEDURE(), N' línea ', ERROR_LINE(), N': ', ERROR_MESSAGE()), 2000);

        -- Solo tiene efecto si la cabecera sobrevivió al ROLLBACK (es decir, no había transacción externa).
        UPDATE rec.Reconciliations
        SET Status = 'FAILED', FinishedAtUtc = SYSUTCDATETIME(), ErrorMessage = @Error
        WHERE Id = @ReconciliationId;

        THROW;
    END CATCH;

    SELECT ReconciliationId, Status, InternalCount, BankCount, MatchedCount, DifferenceCount,
           AmountMismatchCount, DateMismatchCount, MissingInBankCount, MissingInternalCount,
           DuplicateReferenceCount, PendingReviewCount, NetDifferenceAmount
    FROM rec.vw_ReconciliationSummary
    WHERE ReconciliationId = @ReconciliationId;
END;
