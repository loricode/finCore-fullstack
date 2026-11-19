/* Índices del módulo de conciliación. */
SET NOCOUNT ON;

-- Motor de conciliación: movimientos sin conciliar de una cuenta en un rango de fechas.
CREATE NONCLUSTERED INDEX IX_BankStatementDetails_Account_Match_Date
    ON rec.BankStatementDetails (AccountId, MatchStatus, MovementDate)
    INCLUDE (BankReference, Amount);

-- Detección de referencias duplicadas en el banco.
CREATE NONCLUSTERED INDEX IX_BankStatementDetails_Account_Reference
    ON rec.BankStatementDetails (AccountId, BankReference)
    WHERE BankReference IS NOT NULL;

-- "¿Con qué movimiento bancario se concilió esta operación?"
CREATE NONCLUSTERED INDEX IX_BankStatementDetails_MatchedTransaction
    ON rec.BankStatementDetails (MatchedTransactionId)
    WHERE MatchedTransactionId IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_BankStatementDetails_Statement
    ON rec.BankStatementDetails (BankStatementId);

-- Una sola conciliación en curso por cuenta (respaldo de la validación del procedimiento ante carreras).
CREATE UNIQUE NONCLUSTERED INDEX UX_Reconciliations_Account_Running
    ON rec.Reconciliations (AccountId)
    WHERE Status = 'RUNNING';

-- Historial de conciliaciones de una cuenta, la más reciente primero.
CREATE NONCLUSTERED INDEX IX_Reconciliations_Account_StartedAtUtc
    ON rec.Reconciliations (AccountId, StartedAtUtc DESC)
    INCLUDE (Status, PeriodFrom, PeriodTo);

-- Resultados de una conciliación agrupados por tipo (resumen y reporte).
CREATE NONCLUSTERED INDEX IX_ReconciliationDetails_Reconciliation_Type
    ON rec.ReconciliationDetails (ReconciliationId, ResultTypeId)
    INCLUDE (DifferenceAmount, ResolvedAtUtc);

-- Trazabilidad de una operación o un movimiento a través de todas las conciliaciones.
CREATE NONCLUSTERED INDEX IX_ReconciliationDetails_Transaction
    ON rec.ReconciliationDetails (TransactionId)
    WHERE TransactionId IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_ReconciliationDetails_BankDetail
    ON rec.ReconciliationDetails (BankStatementDetailId)
    WHERE BankStatementDetailId IS NOT NULL;
