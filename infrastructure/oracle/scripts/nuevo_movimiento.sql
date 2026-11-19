-- Agrega un movimiento al sistema legado para ver la extracción incremental del Worker.
-- Uso:
--   docker exec -i fincore-oracle sqlplus -s legacy_bank/Legacy_Bank_2026@//localhost/FREEPDB1 < infrastructure/oracle/scripts/nuevo_movimiento.sql
-- En el siguiente ciclo del trabajo LegacyBankImport (60 s en Docker) aparece una carga ORACLE nueva con 1 movimiento.
INSERT INTO bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('555-000111-03', 'MED-ORA-' || TO_CHAR(SYSTIMESTAMP, 'HH24MISSFF3'), 'ING-' || TO_CHAR(SYSDATE, 'DDHH24MI'),
        TRUNC(SYSDATE), 125000.00, 'CONSIGNACION DESDE EL CORE LEGADO');
COMMIT;
SELECT MAX(movement_seq) AS ultimo_movimiento FROM bank_movements;
EXIT;
