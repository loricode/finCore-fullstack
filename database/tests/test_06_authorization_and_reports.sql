/* Alcance por sucursal, permisos de reportes y totales financieros. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @CompanyId INT, @BranchId INT, @OtherBranchId INT, @AccountId INT, @Op INT, @Sup INT, @None INT;
EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                            @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;

DECLARE @n INT;
SELECT @n = COUNT(*) FROM sec.tvf_AuthorizedBranches(@Op);
EXEC test.usp_AssertEquals 1, @n, N'el operador ve solo su sucursal';
SELECT @n = COUNT(*) FROM sec.tvf_AuthorizedBranches(@Op) WHERE BranchId = @OtherBranchId;
EXEC test.usp_AssertEquals 0, @n, N'el operador no ve la otra sucursal';
SELECT @n = COUNT(*) FROM sec.tvf_AuthorizedBranches(@Sup);
EXEC test.usp_AssertEquals 2, @n, N'el supervisor (branches.all) ve todas las de su empresa';
SELECT @n = COUNT(*) FROM sec.tvf_AuthorizedBranches(@Sup) ab JOIN sec.Branches b ON b.Id = ab.BranchId WHERE b.CompanyId <> @CompanyId;
EXEC test.usp_AssertEquals 0, @n, N'branches.all no cruza empresas';
SELECT @n = COUNT(*) FROM sec.tvf_AuthorizedBranches(@None);
EXEC test.usp_AssertEquals 0, @n, N'sin roles no ve sucursales';
UPDATE sec.Users SET IsActive = 0 WHERE Id = @Sup;
SELECT @n = COUNT(*) FROM sec.tvf_AuthorizedBranches(@Sup);
EXEC test.usp_AssertEquals 0, @n, N'un usuario inactivo no ve nada';
UPDATE sec.Users SET IsActive = 1 WHERE Id = @Sup;
EXEC test.usp_Pass N'alcance por sucursal';

-- Totales: 1.000.000 de ingreso + 300.000 de egreso efectivos; pendiente, rechazada y par de reversión no cuentan.
DECLARE @Rev BIGINT, @Rej BIGINT;
EXEC test.usp_AddTransaction @AccountId, N'T-ING', 'ING', '2026-11-03', 1000000, @Op, 1;
EXEC test.usp_AddTransaction @AccountId, N'T-EGR', 'EGR', '2026-11-04', 300000,  @Op, 1;
EXEC test.usp_AddTransaction @AccountId, N'T-PEN', 'ING', '2026-11-05', 777000,  @Op, 0;
EXEC test.usp_AddTransaction @AccountId, N'T-REJ', 'EGR', '2026-11-05', 50000,   @Op, 0, @Rej OUTPUT;
UPDATE fin.Transactions SET StatusId = 4, RejectionReason = N'prueba' WHERE Id = @Rej;
EXEC test.usp_AddTransaction @AccountId, N'T-PAG', 'PAG', '2026-11-06', 200000,  @Op, 1, @Rev OUTPUT;
INSERT fin.Transactions (CompanyId, BranchId, AccountId, TransactionTypeId, StatusId, Reference, OperationDate,
                         Amount, Direction, OriginalTransactionId, CreatedByUserId)
SELECT CompanyId, BranchId, AccountId, 5, 2, N'T-REV', '2026-11-07', Amount, -Direction, Id, @Sup
FROM fin.Transactions WHERE Id = @Rev;
UPDATE fin.Transactions SET StatusId = 5 WHERE Id = @Rev;

DECLARE @Summary TABLE (BranchId INT, BranchCode NVARCHAR(10), BranchName NVARCHAR(100), City NVARCHAR(80),
    TotalOperations INT, TotalIncome DECIMAL(18,2), TotalExpense DECIMAL(18,2), NetAmount DECIMAL(18,2),
    PendingCount INT, ProcessedCount INT, ReconciledCount INT, RejectedCount INT, ReversedCount INT);
INSERT @Summary EXEC rpt.usp_BranchSummary @UserId = @Sup, @DateFrom = '2026-11-01', @DateTo = '2026-11-30';

DECLARE @Income DECIMAL(18,2), @Expense DECIMAL(18,2), @Net DECIMAL(18,2), @Total INT, @Pend INT, @Revd INT, @Rows INT;
SELECT @Income = TotalIncome, @Expense = TotalExpense, @Net = NetAmount, @Total = TotalOperations,
       @Pend = PendingCount, @Revd = ReversedCount
FROM @Summary WHERE BranchId = @BranchId;
SELECT @Rows = COUNT(*) FROM @Summary;
EXEC test.usp_AssertEquals 2, @Rows, N'una fila por sucursal autorizada (incluida la que no tiene operaciones)';
EXEC test.usp_AssertEquals 1000000.00, @Income, N'ingresos brutos';
EXEC test.usp_AssertEquals 300000.00, @Expense, N'egresos brutos';
EXEC test.usp_AssertEquals 700000.00, @Net, N'neto';
EXEC test.usp_AssertEquals 6, @Total, N'operaciones del período';
EXEC test.usp_AssertEquals 1, @Pend, N'pendientes';
EXEC test.usp_AssertEquals 1, @Revd, N'revertidas';
EXEC test.usp_Pass N'totales por sucursal';

DECLARE @Balance DECIMAL(18,2) = (SELECT Balance FROM fin.tvf_AccountBalance(@AccountId, '2026-11-30'));
EXEC test.usp_AssertEquals 700000.00, @Balance, N'saldo: el par de reversión suma cero';
SET @Balance = (SELECT Balance FROM fin.tvf_AccountBalance(@AccountId, '2026-11-06'));
EXEC test.usp_AssertEquals 500000.00, @Balance, N'saldo antes de la reversión';
EXEC test.usp_Pass N'saldo de la cuenta';

ROLLBACK TRANSACTION;

-- Permisos: el operador no tiene reports.read; el supervisor no tiene audit.read.
DECLARE @Err INT, @Case INT = 1, @Expected INT, @Name NVARCHAR(120);
WHILE @Case <= 3
BEGIN
    BEGIN TRANSACTION;
    EXEC test.usp_CreateFixture @CompanyId OUTPUT, @BranchId OUTPUT, @OtherBranchId OUTPUT, @AccountId OUTPUT,
                                @Op OUTPUT, @Sup OUTPUT, @None OUTPUT;
    BEGIN TRY
        IF @Case = 1 BEGIN SET @Expected = 50300; SET @Name = N'50300 operador sin reports.read';
            EXEC rpt.usp_BranchSummary @UserId = @Op, @DateFrom = '2026-11-01', @DateTo = '2026-11-30'; END;
        IF @Case = 2 BEGIN SET @Expected = 50304; SET @Name = N'50304 supervisor sin audit.read';
            EXEC rpt.usp_UserActivity @UserId = @Sup, @DateFrom = '2026-11-01', @DateTo = '2026-11-30'; END;
        IF @Case = 3 BEGIN SET @Expected = 50301; SET @Name = N'50301 período de reporte invertido';
            EXEC rpt.usp_BranchSummary @UserId = @Sup, @DateFrom = '2026-11-30', @DateTo = '2026-11-01'; END;
        SET @Err = 0;
    END TRY
    BEGIN CATCH SET @Err = ERROR_NUMBER(); END CATCH;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    EXEC test.usp_AssertEquals @Expected, @Err, @Name;
    EXEC test.usp_Pass @Name;
    SET @Case += 1;
END;
