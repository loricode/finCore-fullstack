# Tareas · Etapa 2 · Operaciones financieras

## 1. Base de datos
- [x] 1.1 Triggers: historial de estados (`fin.trg_Transactions_StatusHistory`) y protección de cerradas (`Guard`, `PreventDelete`)
- [x] 1.2 Saldo de una cuenta a una fecha (`fin.tvf_AccountBalance`) y vista `fin.vw_Transactions`

## 2. Dominio y API
- [x] 2.1 HU-06 · Registrar una operación con validación por campo
- [x] 2.2 HU-07 · Idempotencia con `Idempotency-Key` (mismo cuerpo → misma respuesta; distinto → 409)
- [x] 2.3 HU-08 · Consultar con filtros, paginación del servidor y resumen
- [x] 2.4 HU-09 · Procesar o rechazar; doble control desde 10 millones (quien registra no procesa)
- [x] 2.5 HU-10 · Revertir con operación espejo y motivo
- [x] 2.6 HU-11 · Operaciones cerradas no se editan ni se borran (dominio + trigger)
- [x] 2.7 HU-12 · Historial de estados con usuario, motivo y CorrelationId en el detalle
- [x] 2.8 Concurrencia optimista con RowVersion (409 si otra persona cambió la operación)

## 3. Angular
- [x] 3.1 Página de operaciones: filtros, tabla con filas intercaladas y paginación en español
- [x] 3.2 Formulario de alta con fechas dd/MM/yyyy (`EsDateAdapter`) y errores por campo de la API
- [x] 3.3 Detalle con historial y acciones según permiso; diálogo de motivo para rechazar o revertir

## 4. Pruebas
- [x] 4.1 Pruebas de dominio de la máquina de estados
- [x] 4.2 `TransactionsApiTests` (alta, idempotencia, doble control, rechazo, reversión, 409)
- [x] 4.3 `test_04_transaction_guards.sql`
