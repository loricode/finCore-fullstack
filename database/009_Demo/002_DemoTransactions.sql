/*
    DEMO · Operaciones de noviembre de 2026.
    Se insertan como Pendientes y luego se procesan, rechazan o revierten con UPDATE, igual que lo
    haría la API: así el trigger deja un historial de estados realista.

    Montería (MON-001) está preparada para que la conciliación del 1 al 15 de noviembre
    produzca todos los tipos de resultado (ver 003_DemoBankStatement.sql).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @CompanyId  INT = (SELECT Id FROM sec.Companies WHERE TaxId = N'900123456-7');
DECLARE @Supervisor INT = (SELECT Id FROM sec.Users WHERE UserName = N'supervisor');

DECLARE @Ops TABLE
(
    Reference NVARCHAR(40), AccountCode NVARCHAR(20), TypeCode VARCHAR(10), OperationDate DATE,
    Amount DECIMAL(18,2), Description NVARCHAR(250), Counterparty NVARCHAR(150), CreatedBy NVARCHAR(50),
    FinalStatus VARCHAR(10)   -- PENDING | PROCESSED | REJECTED
);

INSERT @Ops VALUES
-- Montería: escenario de conciliación
(N'TRF-1001', N'MON-001', 'TRF', '2026-11-03',   500000, N'Transferencia a proveedor',         N'Distribuidora del Sinú', N'operador.monteria', 'PROCESSED'),
(N'ING-1002', N'MON-001', 'ING', '2026-11-04',  1200000, N'Recaudo cartera',                    NULL,                      N'operador.monteria', 'PROCESSED'),
(N'PAG-1003', N'MON-001', 'PAG', '2026-11-05',   350000, N'Pago servicios públicos',            N'Electricaribe',          N'operador.monteria', 'PROCESSED'),
(N'EGR-1004', N'MON-001', 'EGR', '2026-11-06',    80000, N'Caja menor',                         NULL,                      N'operador.monteria', 'PROCESSED'),
(N'ING-1005', N'MON-001', 'ING', '2026-11-07',  2000000, N'Consignación cliente corporativo',   NULL,                      N'operador.monteria', 'PROCESSED'),
(N'ING-1006', N'MON-001', 'ING', '2026-11-10',   450000, N'Recaudo pendiente de soporte',       NULL,                      N'operador.monteria', 'PENDING'),
(N'ING-1007', N'MON-001', 'ING', '2026-11-05',   300000, N'Recaudo convenio',                   NULL,                      N'operador.monteria', 'PROCESSED'),
(N'EGR-1008', N'MON-001', 'EGR', '2026-11-08',    60000, N'Egreso sin soporte',                 NULL,                      N'operador.monteria', 'REJECTED'),
(N'PAG-1009', N'MON-001', 'PAG', '2026-11-11',   150000, N'Pago duplicado a proveedor',         N'Ferretería Córdoba',     N'operador.monteria', 'PROCESSED'),
-- Bogotá
(N'ING-2001', N'BOG-001', 'ING', '2026-11-03',  3500000, N'Recaudo cartera',                    NULL,                      N'operador.bogota',   'PROCESSED'),
(N'ING-2002', N'BOG-001', 'ING', '2026-11-04',   800000, N'Recaudo convenio',                   NULL,                      N'operador.bogota',   'PROCESSED'),
(N'PAG-2003', N'BOG-001', 'PAG', '2026-11-05',  1250000, N'Pago nómina contratistas',           N'Servicios Andinos',      N'operador.bogota',   'PROCESSED'),
(N'TRF-2004', N'BOG-001', 'TRF', '2026-11-06',   600000, N'Traslado a cuenta de Medellín',      N'FinCore Medellín',       N'operador.bogota',   'PROCESSED'),
(N'ING-2005', N'BOG-001', 'ING', '2026-11-12',   950000, N'Recaudo en revisión',                NULL,                      N'operador.bogota',   'PENDING'),
-- Medellín y Barranquilla (las registra el supervisor, que ve todas las sucursales)
(N'ING-3001', N'MED-001', 'ING', '2026-11-04',  2200000, N'Recaudo cartera',                    NULL,                      N'supervisor',        'PROCESSED'),
(N'EGR-3002', N'MED-001', 'EGR', '2026-11-06',   400000, N'Gastos de sucursal',                 NULL,                      N'supervisor',        'PROCESSED'),
(N'ING-4001', N'BAQ-001', 'ING', '2026-11-05',  1750000, N'Recaudo cartera',                    NULL,                      N'supervisor',        'PROCESSED'),
(N'PAG-4002', N'BAQ-001', 'PAG', '2026-11-07',   300000, N'Pago arriendo',                      N'Inmobiliaria Caribe',    N'supervisor',        'PENDING');

-- 1. Alta: todas pendientes (09:00 hora de Colombia = 14:00 UTC del día de la operación)
INSERT fin.Transactions (CompanyId, BranchId, AccountId, TransactionTypeId, StatusId, Reference, OperationDate,
                         Amount, Direction, Description, CounterpartyName, CreatedByUserId, CreatedAtUtc)
SELECT @CompanyId, a.BranchId, a.Id, tt.Id, 1, o.Reference, o.OperationDate,
       o.Amount, tt.DefaultDirection, o.Description, o.Counterparty, u.Id,
       DATEADD(HOUR, 14, CAST(o.OperationDate AS DATETIME2(3)))
FROM @Ops o
JOIN fin.Accounts a          ON a.Code = o.AccountCode AND a.CompanyId = @CompanyId
JOIN fin.TransactionTypes tt ON tt.Code = o.TypeCode
JOIN sec.Users u             ON u.UserName = o.CreatedBy;

-- 2. Procesadas por el supervisor (doble control)
UPDATE t
SET StatusId = 2, ProcessedAtUtc = DATEADD(HOUR, 2, t.CreatedAtUtc),
    UpdatedByUserId = @Supervisor, UpdatedAtUtc = DATEADD(HOUR, 2, t.CreatedAtUtc)
FROM fin.Transactions t
JOIN @Ops o ON o.Reference = t.Reference
WHERE t.CompanyId = @CompanyId AND o.FinalStatus = 'PROCESSED';

-- 3. Rechazada
UPDATE fin.Transactions
SET StatusId = 4, RejectionReason = N'No se adjuntó el soporte del egreso.',
    UpdatedByUserId = @Supervisor, UpdatedAtUtc = DATEADD(HOUR, 3, CreatedAtUtc)
WHERE CompanyId = @CompanyId AND Reference = N'EGR-1008';

-- 4. Reversión de PAG-1009: nueva operación tipo REV con sentido contrario; la original queda Revertida
DECLARE @OriginalId BIGINT = (SELECT Id FROM fin.Transactions WHERE CompanyId = @CompanyId AND Reference = N'PAG-1009');

INSERT fin.Transactions (CompanyId, BranchId, AccountId, TransactionTypeId, StatusId, Reference, OperationDate,
                         Amount, Direction, Description, OriginalTransactionId, CreatedByUserId, CreatedAtUtc,
                         ProcessedAtUtc)
SELECT CompanyId, BranchId, AccountId, 5, 2, N'REV-1009', '2026-11-12',
       Amount, -Direction, N'Reversión: pago registrado dos veces', Id, @Supervisor,
       '2026-11-12T15:00:00', '2026-11-12T15:00:00'
FROM fin.Transactions
WHERE Id = @OriginalId;

UPDATE fin.Transactions
SET StatusId = 5, UpdatedByUserId = @Supervisor, UpdatedAtUtc = '2026-11-12T15:00:00'
WHERE Id = @OriginalId;

COMMIT TRANSACTION;
