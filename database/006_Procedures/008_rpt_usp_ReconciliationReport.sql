/*
    rpt.usp_ReconciliationReport · Detalle de una conciliación, una fila por resultado.
    Las columnas de cabecera se repiten en cada fila para que SSRS las use en el encabezado del informe.
    Si la cuenta no pertenece a una sucursal autorizada al usuario, falla con 50303.
*/
CREATE OR ALTER PROCEDURE rpt.usp_ReconciliationReport
    @UserId            INT,
    @ReconciliationId  INT
AS
BEGIN
    SET NOCOUNT ON;

    EXEC rpt.usp_AssertCanReadReports @UserId = @UserId;

    IF NOT EXISTS (
        SELECT 1
        FROM rec.Reconciliations r
        JOIN fin.Accounts a ON a.Id = r.AccountId
        JOIN sec.tvf_AuthorizedBranches(@UserId) ab ON ab.BranchId = a.BranchId
        WHERE r.Id = @ReconciliationId)
        THROW 50303, N'La conciliación no existe o pertenece a una sucursal no autorizada.', 1;

    SELECT
        s.ReconciliationId,
        s.AccountCode,
        a.BankName,
        a.BankAccountNumber,
        b.Name                 AS BranchName,
        s.PeriodFrom,
        s.PeriodTo,
        s.Status,
        s.StartedAtUtc,
        s.MatchedCount,
        s.DifferenceCount,
        s.NetDifferenceAmount,
        rt.Code                AS ResultTypeCode,
        rt.Name                AS ResultTypeName,
        rt.RequiresAction,
        t.Reference            AS InternalReference,
        t.OperationDate,
        bd.BankReference,
        bd.MovementDate,
        d.InternalAmount,
        d.BankAmount,
        d.DifferenceAmount,
        d.Notes,
        d.ResolvedAtUtc,
        d.ResolutionNote
    FROM rec.vw_ReconciliationSummary s
    JOIN fin.Accounts a                    ON a.Id = s.AccountId
    JOIN sec.Branches b                    ON b.Id = a.BranchId
    JOIN rec.ReconciliationDetails d       ON d.ReconciliationId = s.ReconciliationId
    JOIN rec.ReconciliationResultTypes rt  ON rt.Id = d.ResultTypeId
    LEFT JOIN fin.Transactions t           ON t.Id = d.TransactionId
    LEFT JOIN rec.BankStatementDetails bd  ON bd.Id = d.BankStatementDetailId
    WHERE s.ReconciliationId = @ReconciliationId
    ORDER BY rt.RequiresAction DESC, rt.Id, COALESCE(t.OperationDate, bd.MovementDate);
END;
