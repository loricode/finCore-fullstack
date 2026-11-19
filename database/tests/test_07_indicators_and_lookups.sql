/* Etapa 5 · Indicadores financieros y listas de parámetros de los informes. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;

-- Ingreso 1.000.000 conciliado, egreso 400.000 procesado, ingreso pendiente y egreso rechazado.
DECLARE @Ing BIGINT, @Rej BIGINT;
EXEC test.usp_AddTransaction @AccountId, N'K-ING', 'ING', '2026-11-03', 1000000, @Op, 1, @Ing OUTPUT;
UPDATE fin.Transactions SET StatusId = 3, ReconciledAtUtc = SYSUTCDATETIME() WHERE Id = @Ing;
EXEC test.usp_AddTransaction @AccountId, N'K-EGR', 'EGR', '2026-11-04', 400000, @Op, 1;
EXEC test.usp_AddTransaction @AccountId, N'K-PEN', 'ING', '2026-11-05', 50000, @Op, 0;
EXEC test.usp_AddTransaction @AccountId, N'K-REJ', 'EGR', '2026-11-05', 10000, @Op, 0, @Rej OUTPUT;
UPDATE fin.Transactions SET StatusId = 4, RejectionReason = N'prueba' WHERE Id = @Rej;

DECLARE @I TABLE (DateFrom DATE, DateTo DATE, OperationCount INT, PendingCount INT, ProcessedCount INT, ReconciledCount INT,
    RejectedCount INT, ReversedCount INT, TotalIncome DECIMAL(18,2), TotalExpense DECIMAL(18,2), NetAmount DECIMAL(18,2),
    ReconciliationCoveragePct DECIMAL(5,2), OpenDifferenceCount INT, OpenDifferenceAmount DECIMAL(18,2),
    EtlExecutionCount INT, EtlRowsRead INT, EtlRowsRejected INT, EtlRejectionPct DECIMAL(5,2));
INSERT @I EXEC rpt.usp_FinancialIndicators @UserId = @Sup, @DateFrom = '2026-11-01', @DateTo = '2026-11-30';

DECLARE @Ops INT, @Income DECIMAL(18,2), @Expense DECIMAL(18,2), @Net DECIMAL(18,2), @Coverage DECIMAL(5,2), @Pending INT;
SELECT @Ops = OperationCount, @Income = TotalIncome, @Expense = TotalExpense, @Net = NetAmount,
       @Coverage = ReconciliationCoveragePct, @Pending = PendingCount FROM @I;
EXEC test.usp_AssertEquals 4, @Ops, N'operaciones del período';
EXEC test.usp_AssertEquals 1, @Pending, N'pendientes';
EXEC test.usp_AssertEquals 1000000.00, @Income, N'ingresos efectivos';
EXEC test.usp_AssertEquals 400000.00, @Expense, N'egresos efectivos';
EXEC test.usp_AssertEquals 600000.00, @Net, N'neto';
EXEC test.usp_AssertEquals 50.00, @Coverage, N'cobertura: 1 de 2 efectivas conciliada';
EXEC test.usp_Pass N'indicadores financieros';

-- Listas de parámetros: sucursales del operador (con "Todas") y conciliaciones solo de cuentas autorizadas.
DECLARE @B TABLE (BranchId INT NULL, Label NVARCHAR(100), SortOrder INT);
INSERT @B EXEC rpt.usp_ReportBranches @UserId = @Op;
DECLARE @n INT = (SELECT COUNT(*) FROM @B);
EXEC test.usp_AssertEquals 2, @n, N'"Todas" + la sucursal del operador';

DECLARE @Rid INT;
EXEC rec.usp_RunReconciliation @AccountId = @AccountId, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15', @ReconciliationId = @Rid OUTPUT;
DECLARE @R TABLE (ReconciliationId INT, Label NVARCHAR(200));
INSERT @R EXEC rpt.usp_ReportReconciliations @UserId = @Sup;
SELECT @n = COUNT(*) FROM @R WHERE ReconciliationId = @Rid;
EXEC test.usp_AssertEquals 1, @n, N'la conciliación aparece en la lista del supervisor';
EXEC test.usp_Pass N'listas de parámetros';

ROLLBACK TRANSACTION;

-- Sin reports.read: 50300.
DECLARE @Err INT;
BEGIN TRANSACTION;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
BEGIN TRY
    EXEC rpt.usp_FinancialIndicators @UserId = @Op, @DateFrom = '2026-11-01', @DateTo = '2026-11-30';
    SET @Err = 0;
END TRY
BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
EXEC test.usp_AssertEquals 50300, @Err, N'operador sin reports.read';
EXEC test.usp_Pass N'50300 indicadores sin permiso';
