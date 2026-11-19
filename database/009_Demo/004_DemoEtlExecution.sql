/*
    DEMO · Simula lo que hace el paquete SSIS con un CSV de Banco de Bogotá:
      1. etl.usp_StartExecution   abre la ejecución
      2. carga de filas crudas en stg.BankMovements (en SSIS: OLE DB Destination)
      3. stg.usp_PromoteBankMovements valida, rechaza y carga

    De 9 filas, 4 son válidas y 5 se rechazan: INVALID_DATE, INVALID_AMOUNT (formato con puntos de miles),
    ACCOUNT_NOT_FOUND, MISSING_MOVEMENT_ID y DUPLICATE_IN_FILE.
    Resultado: ejecución COMPLETED_WITH_ERRORS con RowsRead 9 = RowsLoaded 4 + RowsRejected 5.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId INT = (SELECT Id FROM sec.Companies WHERE TaxId = N'900123456-7');
DECLARE @EtlExecutionId BIGINT;

EXEC etl.usp_StartExecution
    @CompanyId      = @CompanyId,
    @PackageName    = N'ImportBankStatementCsv.dtsx',
    @SourceSystem   = 'CSV',
    @SourceName     = N'bancodebogota_987-654321-02_20261115.csv',
    @TriggeredBy    = N'demo-seed',
    @EtlExecutionId = @EtlExecutionId OUTPUT;

INSERT stg.BankMovements (EtlExecutionId, RowNumber, AccountNumber, ExternalMovementId, BankReference, MovementDate, Amount, Description)
VALUES
    (@EtlExecutionId, 1, N'987-654321-02', N'BOG-M-001', N'ing-2001 ',  N'2026-11-03', N'3500000.00',  N'CONSIGNACION RECAUDO'),
    (@EtlExecutionId, 2, N'987-654321-02', N'BOG-M-002', N'ING-2002',   N'2026-11-04', N'800000',      N'ABONO CONVENIO'),
    (@EtlExecutionId, 3, N'987-654321-02', N'BOG-M-003', N'PAG-2003',   N'2026-11-05', N'-1250000.00', N'PAGO CONTRATISTAS'),
    (@EtlExecutionId, 4, N'987-654321-02', N'BOG-M-004', N'TRF-2004',   N'2026-11-06', N'-600000.00',  N'TRASLADO MEDELLIN'),
    (@EtlExecutionId, 5, N'987-654321-02', N'BOG-M-005', N'GMF-1106',   N'06/11/2026', N'-2400.00',    N'GMF 4X1000'),
    (@EtlExecutionId, 6, N'987-654321-02', N'BOG-M-006', N'ING-2005',   N'2026-11-12', N'950.000,00',  N'CONSIGNACION'),
    (@EtlExecutionId, 7, N'000-000000-00', N'BOG-M-007', N'XXX-0001',   N'2026-11-07', N'10000.00',    N'CUENTA DESCONOCIDA'),
    (@EtlExecutionId, 8, N'987-654321-02', NULL,         N'ING-2006',   N'2026-11-08', N'50000.00',    N'SIN IDENTIFICADOR'),
    (@EtlExecutionId, 9, N'987-654321-02', N'BOG-M-004', N'TRF-2004',   N'2026-11-06', N'-600000.00',  N'TRASLADO MEDELLIN');

EXEC stg.usp_PromoteBankMovements @EtlExecutionId = @EtlExecutionId;
