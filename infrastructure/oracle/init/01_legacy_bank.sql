-- ============================================================================
-- Sistema bancario LEGADO simulado (Oracle 23ai Free) · Etapa 4
-- Lo ejecuta la imagen gvenzl/oracle-free la primera vez que arranca el contenedor
-- (/container-entrypoint-initdb.d), como SYS. El script es autosuficiente: crea su usuario.
--
-- FinCore NO escribe aquí: solo extrae (Worker en Docker o paquete SSIS en Windows) con
-- etl/FinCore.Integration.SSIS/sql/oracle_extract_movements.sql
-- ============================================================================
ALTER SESSION SET CONTAINER = FREEPDB1;

DECLARE
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM dba_users WHERE username = 'LEGACY_BANK';
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE 'CREATE USER legacy_bank IDENTIFIED BY "Legacy_Bank_2026" QUOTA UNLIMITED ON USERS';
    EXECUTE IMMEDIATE 'GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE PROCEDURE TO legacy_bank';
  END IF;
END;
/

CREATE TABLE legacy_bank.bank_movements (
  movement_seq  NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,   -- marca de agua de la extracción
  account_no    VARCHAR2(40)   NOT NULL,
  movement_id   VARCHAR2(60)   NOT NULL,
  reference     VARCHAR2(60),
  booking_date  DATE           NOT NULL,
  amount        NUMBER(18, 2)  NOT NULL,
  narrative     VARCHAR2(200),
  created_at    TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
  CONSTRAINT uq_bank_movements_id UNIQUE (account_no, movement_id)
);

COMMENT ON TABLE legacy_bank.bank_movements IS 'Movimientos del core bancario legado. FinCore los extrae de forma incremental por MOVEMENT_SEQ.';

-- Medellín (cuenta 555-000111-03): coincide con las operaciones de la demo de FinCore
INSERT INTO legacy_bank.bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('555-000111-03', 'MED-ORA-0001', 'ING-3001', DATE '2026-11-04', 2200000.00, 'CONSIGNACION RECAUDO');
INSERT INTO legacy_bank.bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('555-000111-03', 'MED-ORA-0002', 'EGR-3002', DATE '2026-11-06', -400000.00, 'GASTOS SUCURSAL');
INSERT INTO legacy_bank.bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('555-000111-03', 'MED-ORA-0003', 'GMF-1106', DATE '2026-11-06', -1600.00, 'GMF 4X1000');

-- Barranquilla (cuenta 444-222333-04)
INSERT INTO legacy_bank.bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('444-222333-04', 'BAQ-ORA-0001', 'ING-4001', DATE '2026-11-05', 1750000.00, 'CONSIGNACION RECAUDO');
INSERT INTO legacy_bank.bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('444-222333-04', 'BAQ-ORA-0002', 'PAG-4002', DATE '2026-11-07', -300000.00, 'PAGO ARRIENDO INMOBILIARIA CARIBE');

-- Cuenta que no existe en FinCore: la promoción la rechaza con ACCOUNT_NOT_FOUND (se ve en el historial ETL)
INSERT INTO legacy_bank.bank_movements (account_no, movement_id, reference, booking_date, amount, narrative)
VALUES ('999-999999-99', 'XXX-ORA-0001', 'TRF-9999', DATE '2026-11-08', -50000.00, 'CUENTA DE OTRA ENTIDAD');

COMMIT;
