/*
    fin.vw_Transactions · Operación con sus descripciones (sucursal, cuenta, tipo, estado, usuario),
    la operación que revierte y la reversión que la anuló.
    Base de las consultas de lectura de la API y de los reportes. No aplica filtros de seguridad:
    quien la consulta debe filtrar por las sucursales autorizadas (sec.tvf_AuthorizedBranches).
*/
CREATE OR ALTER VIEW fin.vw_Transactions
AS
SELECT
    t.Id,
    t.PublicId,
    t.CompanyId,
    t.BranchId,
    b.Code               AS BranchCode,
    b.Name               AS BranchName,
    t.AccountId,
    a.Code               AS AccountCode,
    a.BankAccountNumber,
    t.TransactionTypeId,
    tt.Code              AS TransactionTypeCode,
    tt.Name              AS TransactionTypeName,
    t.StatusId,
    ts.Code              AS StatusCode,
    ts.Name              AS StatusName,
    t.Reference,
    t.OperationDate,
    t.Amount,
    t.Direction,
    t.SignedAmount,
    t.Currency,
    t.Description,
    t.CounterpartyName,
    t.CounterpartyAccount,
    t.OriginalTransactionId,
    o.PublicId           AS OriginalPublicId,
    r.PublicId           AS ReversalPublicId,
    t.RejectionReason,
    t.Source,
    t.CreatedByUserId,
    cu.UserName          AS CreatedByUserName,
    t.CreatedAtUtc,
    t.ProcessedAtUtc,
    t.ReconciledAtUtc,
    t.RowVersion,
    -- Movimiento real de dinero para totales brutos: excluye pendientes, rechazadas y pares de reversión.
    CAST(CASE WHEN t.StatusId IN (2, 3) AND t.TransactionTypeId <> 5 THEN 1 ELSE 0 END AS BIT) AS IsEffective
FROM fin.Transactions t
JOIN sec.Branches b           ON b.Id = t.BranchId
JOIN fin.Accounts a           ON a.Id = t.AccountId
JOIN fin.TransactionTypes tt  ON tt.Id = t.TransactionTypeId
JOIN fin.TransactionStatus ts ON ts.Id = t.StatusId
JOIN sec.Users cu             ON cu.Id = t.CreatedByUserId
LEFT JOIN fin.Transactions o  ON o.Id = t.OriginalTransactionId
LEFT JOIN fin.Transactions r  ON r.OriginalTransactionId = t.Id;
