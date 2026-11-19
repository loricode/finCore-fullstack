# Tareas · Etapa 4 · Integraciones

## 1. Webhooks (Node.js)
- [x] 1.1 HU-19 · Adaptador Express 5 en Node 24 con TypeScript sin compilación
- [x] 1.2 Firma `X-Bank-Signature` (HMAC) con anti-replay de 5 minutos
- [x] 1.3 Cola con reintentos 1/2/4/8/16 s y archivo dead letter; sin reintento ante errores de negocio (4xx)
- [x] 1.4 Endpoint de integración en la API con idempotencia por evento
- [x] 1.5 Simulador bancario (perfil `demo`) y `node-tests`

## 2. Oracle
- [x] 2.1 Oracle en Docker (gvenzl) con el esquema del banco legado
- [x] 2.2 HU-20 · Extracción incremental por marca de agua (`etl.Watermarks`) en el Worker
- [x] 2.3 Consulta de extracción compartida entre el Worker y el diseño SSIS (`sql/oracle_extract_movements.sql`)

## 3. Worker (Windows Service)
- [x] 3.1 `UseWindowsService`; el mismo binario corre en Docker
- [x] 3.2 Trabajos programados con CorrelationId por ejecución e identidad `svc.integration`
- [x] 3.3 HU-21 · Conciliación nocturna
- [x] 3.4 HU-22 · Depuración de staging por lotes (`stg.usp_PurgeStaging`)
- [x] 3.5 Recuperación de eventos de entrada pendientes
- [x] 3.6 Guía de instalación como servicio (`docs/Deployment/windows-service.md`)

## 4. SSIS
- [x] 4.1 Diseño del paquete y SQL de cada tarea (marca de agua, inicio, promoción, cierre, manejo de error), validados
- [ ] 4.2 Construir el `.dtsx` en Visual Studio (SSIS Projects) a partir del diseño de `etl/FinCore.Integration.SSIS`
- [ ] 4.3 Al activar SSIS, desactivar el trabajo Oracle del Worker (`Worker__Jobs__LegacyBankImport__Enabled=false`) para no extraer dos veces
