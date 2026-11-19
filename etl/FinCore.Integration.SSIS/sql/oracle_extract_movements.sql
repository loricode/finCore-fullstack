-- Extracción incremental de movimientos del sistema legado (Oracle · LEGACY_BANK).
-- La usan, sin cambios, el FinCore.Worker (Docker) y el paquete SSIS (origen ADO.NET / OLE DB del Data Flow).
--
-- Transformación ("T" del ETL) hecha en el origen, al formato que espera stg.BankMovements:
--   BOOKING_DATE  DATE        ->  'YYYY-MM-DD'
--   AMOUNT        NUMBER      ->  texto con punto decimal, signo y 2 decimales (independiente del NLS de la sesión)
-- La validación de cada fila NO se hace aquí: la hace stg.usp_PromoteBankMovements, igual que para los CSV.
--
-- Parámetros: :last_sequence (marca de agua de etl.Watermarks) · :max_rows (tamaño del lote)
SELECT MOVEMENT_SEQ,
       ACCOUNT_NO,
       MOVEMENT_ID,
       REFERENCE,
       TO_CHAR(BOOKING_DATE, 'YYYY-MM-DD')                                   AS BOOKING_DATE,
       TO_CHAR(AMOUNT, 'FM9999999999990.00', 'NLS_NUMERIC_CHARACTERS=''.,''') AS AMOUNT,
       NARRATIVE,
       ROW_NUMBER() OVER (ORDER BY MOVEMENT_SEQ)                            AS ROW_NUMBER  -- stg.BankMovements.RowNumber en SSIS
FROM LEGACY_BANK.BANK_MOVEMENTS
WHERE MOVEMENT_SEQ > :last_sequence
ORDER BY MOVEMENT_SEQ
FETCH FIRST :max_rows ROWS ONLY
