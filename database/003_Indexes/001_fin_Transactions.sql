/*
    Índices de fin.Transactions. Cada uno indica la consulta que atiende.
    (La unicidad de Reference por empresa ya está en la tabla: UQ_Transactions_Company_Reference.)
*/
SET NOCOUNT ON;

-- Consulta de operaciones por sucursal y período (pantalla principal y rpt.usp_BranchSummary).
CREATE NONCLUSTERED INDEX IX_Transactions_Branch_OperationDate
    ON fin.Transactions (BranchId, OperationDate)
    INCLUDE (StatusId, TransactionTypeId, SignedAmount, CreatedByUserId);

-- Motor de conciliación: operaciones de una cuenta por estado en un rango de fechas.
CREATE NONCLUSTERED INDEX IX_Transactions_Account_Status_OperationDate
    ON fin.Transactions (AccountId, StatusId, OperationDate)
    INCLUDE (Reference, SignedAmount, ReconciledAtUtc);

-- Idempotencia de la API: la misma Idempotency-Key no crea dos operaciones en la empresa.
CREATE UNIQUE NONCLUSTERED INDEX UX_Transactions_Company_IdempotencyKey
    ON fin.Transactions (CompanyId, IdempotencyKey)
    WHERE IdempotencyKey IS NOT NULL;

-- Una operación solo puede revertirse una vez.
CREATE UNIQUE NONCLUSTERED INDEX UX_Transactions_OriginalTransactionId
    ON fin.Transactions (OriginalTransactionId)
    WHERE OriginalTransactionId IS NOT NULL;

-- Actividad por usuario (rpt.usp_UserActivity) y soporte: "¿qué registró este usuario?".
CREATE NONCLUSTERED INDEX IX_Transactions_CreatedBy_CreatedAtUtc
    ON fin.Transactions (CreatedByUserId, CreatedAtUtc);

-- Historial de estados de una operación, en orden.
CREATE NONCLUSTERED INDEX IX_TransactionStatusHistory_Transaction
    ON fin.TransactionStatusHistory (TransactionId, ChangedAtUtc);
