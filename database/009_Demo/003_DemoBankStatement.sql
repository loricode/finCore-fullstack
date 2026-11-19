/*
    DEMO · Extracto de Bancolombia para MON-001, del 1 al 15 de noviembre de 2026.

    Resultado esperado de  EXEC rec.usp_RunReconciliation @AccountId = <MON-001>,
    @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15', @DateToleranceDays = 1:

      MATCHED              TRF-1001, ING-1002 (un día después), PAG-1009 y REV-1009 (par de reversión)
      AMOUNT_MISMATCH      PAG-1003: el banco debitó 355.000, no 350.000
      DATE_MISMATCH        ING-1007: el banco lo abonó 7 días después
      MISSING_IN_BANK      EGR-1004: no aparece en el banco
      MISSING_INTERNAL     BNK-COM-1110: comisión bancaria no registrada
      DUPLICATE_REFERENCE  ING-1005: el banco lo registró dos veces (2 resultados)
      PENDING_REVIEW       ING-1006: sigue pendiente aunque el banco ya lo abonó
      (EGR-1008 está rechazada: no participa)
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @AccountId INT = (SELECT Id FROM fin.Accounts WHERE Code = N'MON-001');
DECLARE @ImportedBy INT = (SELECT Id FROM sec.Users WHERE UserName = N'supervisor');

INSERT rec.BankStatements (AccountId, Source, PeriodFrom, PeriodTo, FileName, LineCount, ImportedAtUtc, ImportedByUserId)
VALUES (@AccountId, 'CSV', '2026-11-01', '2026-11-15', N'bancolombia_123-456789-01_20261115.csv', 10,
        '2026-11-16T13:00:00', @ImportedBy);

DECLARE @StatementId INT = CAST(SCOPE_IDENTITY() AS INT);

INSERT rec.BankStatementDetails (BankStatementId, AccountId, ExternalMovementId, BankReference, MovementDate, Amount, Description)
VALUES
    (@StatementId, @AccountId, N'BCO-0001', N'TRF-1001',     '2026-11-03',  -500000.00, N'TRANSF A DISTRIBUIDORA DEL SINU'),
    (@StatementId, @AccountId, N'BCO-0002', N'ING-1002',     '2026-11-05', 1200000.00, N'CONSIGNACION RECAUDO'),
    (@StatementId, @AccountId, N'BCO-0003', N'PAG-1003',     '2026-11-05',  -355000.00, N'PAGO PSE ELECTRICARIBE'),
    (@StatementId, @AccountId, N'BCO-0004', N'BNK-COM-1110', '2026-11-10',   -12500.00, N'COMISION MANEJO CUENTA'),
    (@StatementId, @AccountId, N'BCO-0005', N'ING-1005',     '2026-11-07', 2000000.00, N'CONSIGNACION CORPORATIVA'),
    (@StatementId, @AccountId, N'BCO-0006', N'ING-1005',     '2026-11-07', 2000000.00, N'CONSIGNACION CORPORATIVA'),
    (@StatementId, @AccountId, N'BCO-0007', N'ING-1007',     '2026-11-12',  300000.00, N'ABONO CONVENIO'),
    (@StatementId, @AccountId, N'BCO-0008', N'PAG-1009',     '2026-11-11',  -150000.00, N'PAGO FERRETERIA CORDOBA'),
    (@StatementId, @AccountId, N'BCO-0009', N'REV-1009',     '2026-11-12',  150000.00, N'DEVOLUCION FERRETERIA CORDOBA'),
    (@StatementId, @AccountId, N'BCO-0010', N'ING-1006',     '2026-11-10',  450000.00, N'CONSIGNACION');

COMMIT TRANSACTION;
