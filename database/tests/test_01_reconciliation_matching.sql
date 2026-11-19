/* Motor de conciliación: cada regla de emparejamiento y sus efectos. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;

DECLARE @Matched BIGINT, @NextDay BIGINT, @Amount BIGINT, @Date BIGINT, @Missing BIGINT, @Dup BIGINT,
        @Pending BIGINT, @Edge BIGINT, @OutOfPeriod BIGINT;

EXEC test.usp_AddTransaction @AccountId, N'R-MATCH',   'TRF', '2026-11-03', 500000,  @Op, 1, @Matched OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'R-NEXTDAY', 'ING', '2026-11-04', 1200000, @Op, 1, @NextDay OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'R-AMOUNT',  'PAG', '2026-11-05', 350000,  @Op, 1, @Amount OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'R-DATE',    'ING', '2026-11-05', 300000,  @Op, 1, @Date OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'R-MISSING', 'EGR', '2026-11-06', 80000,   @Op, 1, @Missing OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'R-DUP',     'ING', '2026-11-07', 2000000, @Op, 1, @Dup OUTPUT;
EXEC test.usp_AddTransaction @AccountId, N'R-PENDING', 'ING', '2026-11-10', 450000,  @Op, 0, @Pending OUTPUT;
-- Último día del período; el banco lo registra al día siguiente (fuera del período, dentro de la tolerancia).
EXEC test.usp_AddTransaction @AccountId, N'R-EDGE',    'ING', '2026-11-15', 90000,   @Op, 1, @Edge OUTPUT;
-- Fuera del período: no debe participar.
EXEC test.usp_AddTransaction @AccountId, N'R-OUT',     'ING', '2026-11-20', 70000,   @Op, 1, @OutOfPeriod OUTPUT;

DECLARE @St INT;
EXEC test.usp_AddStatement @AccountId, @St OUTPUT;

INSERT rec.BankStatementDetails (BankStatementId, AccountId, ExternalMovementId, BankReference, MovementDate, Amount)
VALUES (@St, @AccountId, N'B1',  N'R-MATCH',   '2026-11-03',  -500000),
       (@St, @AccountId, N'B2',  N'R-NEXTDAY', '2026-11-05',  1200000),
       (@St, @AccountId, N'B3',  N'R-AMOUNT',  '2026-11-05',  -355000),
       (@St, @AccountId, N'B4',  N'R-DATE',    '2026-11-12',   300000),
       (@St, @AccountId, N'B5',  N'R-DUP',     '2026-11-07',  2000000),
       (@St, @AccountId, N'B6',  N'R-DUP',     '2026-11-07',  2000000),
       (@St, @AccountId, N'B7',  N'R-PENDING', '2026-11-10',   450000),
       (@St, @AccountId, N'B8',  N'COMISION',  '2026-11-10',   -12500),
       (@St, @AccountId, N'B9',  NULL,         '2026-11-11',    -3000),
       (@St, @AccountId, N'B10', N'R-EDGE',    '2026-11-16',    90000),
       (@St, @AccountId, N'B11', N'AJENO',     '2026-11-16',    -1000);   -- fuera del período y sin pareja: se ignora

DECLARE @Rid INT;
EXEC rec.usp_RunReconciliation @AccountId = @AccountId, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15',
     @DateToleranceDays = 1, @ExecutedByUserId = @Sup, @ExecutedBy = N'test', @CorrelationId = N'corr-test-01',
     @ReconciliationId = @Rid OUTPUT;

DECLARE @n INT;
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 1;
EXEC test.usp_AssertEquals 3, @n, N'MATCHED (exacta, día siguiente y borde del período)';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 2 AND TransactionId = @Amount;
EXEC test.usp_AssertEquals 1, @n, N'AMOUNT_MISMATCH';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 3 AND TransactionId = @Date;
EXEC test.usp_AssertEquals 1, @n, N'DATE_MISMATCH';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 4 AND TransactionId = @Missing;
EXEC test.usp_AssertEquals 1, @n, N'MISSING_IN_BANK';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 5;
EXEC test.usp_AssertEquals 2, @n, N'MISSING_INTERNAL (comisión y movimiento sin referencia; el de fuera del período no)';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 6 AND TransactionId = @Dup;
EXEC test.usp_AssertEquals 2, @n, N'DUPLICATE_REFERENCE (uno por movimiento repetido)';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 7 AND TransactionId = @Pending AND BankStatementDetailId IS NOT NULL;
EXEC test.usp_AssertEquals 1, @n, N'PENDING_REVIEW con su movimiento bancario';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND TransactionId = @OutOfPeriod;
EXEC test.usp_AssertEquals 0, @n, N'una operación fuera del período no participa';
EXEC test.usp_Pass N'clasificación de cada resultado';

DECLARE @Diff DECIMAL(18,2) = (SELECT DifferenceAmount FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid AND ResultTypeId = 2);
EXEC test.usp_AssertEquals -5000.00, @Diff, N'diferencia = banco - interno';
EXEC test.usp_Pass N'valor de la diferencia';

-- Efectos: conciliadas pasan a estado 3 y los movimientos quedan MATCHED apuntando a su operación.
SELECT @n = COUNT(*) FROM fin.Transactions WHERE Id IN (@Matched, @NextDay, @Edge) AND StatusId = 3 AND ReconciledAtUtc IS NOT NULL;
EXEC test.usp_AssertEquals 3, @n, N'operaciones conciliadas en estado 3';
SELECT @n = COUNT(*) FROM fin.Transactions WHERE Id IN (@Amount, @Date, @Missing, @Dup) AND StatusId = 2;
EXEC test.usp_AssertEquals 4, @n, N'las diferencias siguen procesadas';
SELECT @n = COUNT(*) FROM rec.BankStatementDetails WHERE BankStatementId = @St AND MatchStatus = 'MATCHED' AND MatchedTransactionId IS NOT NULL;
EXEC test.usp_AssertEquals 3, @n, N'movimientos MATCHED';
SELECT @n = COUNT(*) FROM fin.TransactionStatusHistory WHERE TransactionId = @Matched AND ToStatusId = 3
                                                          AND CorrelationId = N'corr-test-01' AND ChangedByUserId = @Sup;
EXEC test.usp_AssertEquals 1, @n, N'historial con usuario y CorrelationId de la conciliación';
EXEC test.usp_Pass N'efectos sobre operaciones, movimientos e historial';

-- Cabecera
DECLARE @Status VARCHAR(10), @MatchedCount INT, @DiffCount INT;
SELECT @Status = Status, @MatchedCount = MatchedCount, @DiffCount = DifferenceCount FROM rec.Reconciliations WHERE Id = @Rid;
EXEC test.usp_AssertEquals 'COMPLETED', @Status, N'estado de la conciliación';
EXEC test.usp_AssertEquals 3, @MatchedCount, N'MatchedCount';
EXEC test.usp_AssertEquals 8, @DiffCount, N'DifferenceCount';
EXEC test.usp_Pass N'cabecera con conteos';

-- Repetir la conciliación no vuelve a conciliar ni duplica efectos: solo reevalúa lo pendiente.
DECLARE @Rid2 INT;
EXEC rec.usp_RunReconciliation @AccountId = @AccountId, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15',
     @DateToleranceDays = 1, @ReconciliationId = @Rid2 OUTPUT;
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid2 AND ResultTypeId = 1;
EXEC test.usp_AssertEquals 0, @n, N'segunda ejecución sin nuevas conciliaciones';
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails WHERE ReconciliationId = @Rid2;
EXEC test.usp_AssertEquals 8, @n, N'segunda ejecución reporta las mismas diferencias';
SELECT @n = COUNT(*) FROM rec.vw_OpenDifferences WHERE AccountId = @AccountId;
EXEC test.usp_AssertEquals 8, @n, N'la bandeja muestra solo la última ejecución';
EXEC test.usp_Pass N'conciliación repetible';

-- Un segundo cargo con una referencia ya conciliada se detecta como duplicado.
INSERT rec.BankStatementDetails (BankStatementId, AccountId, ExternalMovementId, BankReference, MovementDate, Amount)
VALUES (@St, @AccountId, N'B12', N'R-MATCH', '2026-11-14', -500000);
DECLARE @Rid3 INT;
EXEC rec.usp_RunReconciliation @AccountId = @AccountId, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15',
     @DateToleranceDays = 1, @ReconciliationId = @Rid3 OUTPUT;
SELECT @n = COUNT(*) FROM rec.ReconciliationDetails d JOIN rec.BankStatementDetails b ON b.Id = d.BankStatementDetailId
WHERE d.ReconciliationId = @Rid3 AND d.ResultTypeId = 6 AND b.ExternalMovementId = N'B12';
EXEC test.usp_AssertEquals 1, @n, N'cargo repetido de una referencia ya conciliada';
EXEC test.usp_Pass N'duplicado contra un movimiento ya conciliado';

ROLLBACK TRANSACTION;
