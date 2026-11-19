-- Execute SQL Task "Validar y cargar" (OLE DB). Parámetro 0 Input User::EtlExecutionId (LARGE_INTEGER).
-- Valida cada fila de staging, registra los rechazos y carga los movimientos válidos en una transacción propia.
EXEC stg.usp_PromoteBankMovements @EtlExecutionId = ?;
