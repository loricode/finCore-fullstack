# database · FinCore

Objetos de SQL Server de FinCore versionados como scripts: tablas, índices, funciones, vistas, procedimientos,
triggers y datos semilla. **Es la única fuente de verdad del esquema**: EF Core mapea estas tablas pero no genera
migraciones ([ADR-002](../docs/Architecture/adr/ADR-002-scripts-sql-fuente-de-verdad.md)).

Requisitos: SQL Server 2019 o posterior (probado en 2025 Developer), `sqlcmd` en el PATH, PowerShell 5.1 o 7.

```powershell
cd database
.\deploy.ps1 -Recreate -IncludeDemo     # base FinCore desde cero con datos de demostración
.\run-tests.ps1                         # 9 archivos de prueba T-SQL; no dejan datos
.\deploy.ps1                            # despliegue incremental: solo lo nuevo o modificado
.\deploy.ps1 -DryRun                    # qué se ejecutaría
.\deploy.ps1 -Server "sql01,1433" -Database FinCore -User deploy -Password $env:SQL_PASSWORD -TrustServerCertificate
.\deploy.ps1 -AppLogin fincore_app -AppPassword $env:APP_PASSWORD   # además crea/actualiza el login de la API
.\deploy.ps1 -ReportsLogin fincore_reports -ReportsPassword $env:REPORTS_PASSWORD   # login de SSRS (solo EXECUTE en rpt)
```

En Docker lo ejecuta el servicio `db-migrator` (PowerShell 7 sobre Linux) con el mismo script: ver
[docs/Deployment/docker.md](../docs/Deployment/docker.md). `-AppLogin` ejecuta `_deploy/100_AppLogin.sql`, que no se registra
en la bitácora porque depende del entorno; la contraseña no puede contener el nombre del login (política de SQL Server).
`-ReportsLogin` hace lo mismo con `_deploy/101_ReportsLogin.sql`: usuario con esquema por defecto `rpt` en el rol `fincore_reports_role`.

## Estructura

| Carpeta | Tipo | Contenido |
|---|---|---|
| `_deploy/` | — | Bitácora `dbo.SchemaVersions` y funciones de PowerShell compartidas |
| `001_Schemas/` | Versionado | Esquemas por módulo: `sec`, `fin`, `rec`, `stg`, `etl`, `integration`, `audit`, `rpt` |
| `002_Tables/` | Versionado | Una tabla por archivo, con sus PK, FK, UNIQUE, CHECK y DEFAULT (`025_etl_Watermarks`: marca de agua de extracciones incrementales, Etapa 4) |
| `003_Indexes/` | Versionado | Índices no agrupados y filtrados; cada uno dice qué consulta atiende |
| `004_Functions/` | Repetible | Escalares y de tabla en línea |
| `005_Views/` | Repetible | Vistas de lectura |
| `006_Procedures/` | Repetible | ETL, conciliación, reportes y mantenimiento |
| `007_Triggers/` | Repetible | Historial de estados, protecciones y tablas de solo inserción |
| `008_Seed/` | Repetible | Catálogos, roles, permisos y matriz rol-permiso (`MERGE`) |
| `009_Demo/` | Versionado | Datos de demostración (solo con `-IncludeDemo`) |
| `010_Security/` | Repetible | Rol `fincore_app_role` de la aplicación: lectura/escritura sin `DELETE` (salvo en `sec.UserRoles`, `sec.UserBranches` y `sec.UserWindowsAccounts`), `EXECUTE` en `rec`, `stg`, `etl`, `rpt`, sin DDL. Rol `fincore_reports_role` de SSRS: solo `EXECUTE` sobre `rpt` |
| `tests/` | — | Framework y pruebas T-SQL (esquema `test`, nunca se despliega en producción) |

El orden cambia respecto a la propuesta inicial (funciones antes que vistas y procedimientos, y triggers antes que semillas)
porque las vistas dependen de las funciones y el historial debe existir antes de insertar datos.

### Versionado o repetible

- **Versionado** se ejecuta una sola vez. `deploy.ps1` guarda su checksum SHA-256 y **se detiene si un script ya aplicado cambió**.
- **Repetible** usa `CREATE OR ALTER` o `MERGE` y se vuelve a ejecutar cada vez que cambia su checksum.

### Cómo hacer un cambio

| Cambio | Qué hacer |
|---|---|
| Columna nueva en una tabla | Script **nuevo** en `002_Tables/`, p. ej. `025_fin_Transactions_AddChannel.sql` con `ALTER TABLE` idempotente |
| Índice nuevo | Script nuevo en `003_Indexes/` |
| Cambiar un procedimiento, vista, función o trigger | Editar su archivo: se redespliega solo |
| Permiso nuevo o cambio de matriz | Editar `008_Seed/002_sec_RolesPermissions.sql` |
| Objeto que cambia un comportamiento | Actualizar también la spec en `openspec/` y su prueba en `tests/` |

Nunca edites un script versionado que ya se aplicó en un entorno compartido.

## Convenciones

- Nombres en inglés y PascalCase. Restricciones: `PK_`, `FK_<Hija>_<Padre>`, `UQ_`, `UX_` (índice único), `IX_`, `CK_`, `DF_`.
- Procedimientos `usp_`, funciones escalares `fn_`, de tabla `tvf_`, vistas `vw_`, triggers `trg_<Tabla>_<Propósito>`.
- Dinero: `DECIMAL(18,2)`. En C# `decimal`, nunca `double`.
- Instantes en UTC: `DATETIME2(3)` con sufijo `Utc`. Fechas de negocio (`OperationDate`, `MovementDate`) en `DATE`, hora de Colombia (UTC-5).
- Catálogos con Id fijo (`TINYINT`/`SMALLINT` sin IDENTITY) que coinciden con los enums del dominio.
- Estados internos de procesos como `VARCHAR` con `CHECK` (`RUNNING`, `COMPLETED`…).
- Llaves foráneas compuestas para coherencia entre empresa, sucursal y cuenta.
- Procedimientos con `SET NOCOUNT ON; SET XACT_ABORT ON;` y `TRY/CATCH` que revierte y relanza con `THROW`.
- Los scripts se ejecutan con `QUOTED_IDENTIFIER ON` (`sqlcmd -I`): lo exigen los índices filtrados y las columnas calculadas persistidas.

## Objetos principales

| Objeto | Qué hace |
|---|---|
| `rec.usp_RunReconciliation` | Motor de conciliación set-based: 7 tipos de resultado, tolerancia de fechas, duplicados, una ejecución por cuenta, cabecera `FAILED` ante error |
| `stg.usp_PromoteBankMovements` | Valida el staging (8 códigos de error), carga extractos y cierra la ejecución con conteos balanceados |
| `etl.usp_StartExecution` / `etl.usp_FailExecution` | Ciclo de vida de una carga (los usa SSIS) |
| `stg.usp_PurgeStaging` | Depuración por lotes del staging (Worker) |
| `rpt.usp_BranchSummary`, `rpt.usp_OperationsByPeriod`, `rpt.usp_ReconciliationReport`, `rpt.usp_UserActivity`, `rpt.usp_FinancialIndicators` | Reportes para API y SSRS con validación de permisos y alcance |
| `rpt.usp_ReportUsers`, `rpt.usp_ReportBranches`, `rpt.usp_ReportReconciliations` | Listas de valores de los parámetros de SSRS. `usp_ReportUsers` traduce la cuenta de Windows (`sec.UserWindowsAccounts`) al usuario de FinCore |
| `sec.tvf_AuthorizedBranches`, `sec.tvf_UserPermissions` | Alcance por sucursal y permisos efectivos |
| `fin.tvf_AccountBalance` | Saldo de una cuenta a una fecha |
| `fin.vw_Transactions`, `rec.vw_ReconciliationSummary`, `rec.vw_OpenDifferences`, `etl.vw_ExecutionSummary` | Vistas de lectura |
| `fin.trg_Transactions_StatusHistory` | Historial de estados con usuario y CorrelationId de `SESSION_CONTEXT` |
| `fin.trg_Transactions_Guard`, `fin.trg_Transactions_PreventDelete` | Operaciones no se borran ni cambian después de procesadas |
| `audit.trg_AuditLogs_AppendOnly`, `fin.trg_TransactionStatusHistory_AppendOnly` | Evidencia de solo inserción |

El modelo completo y el diagrama entidad-relación están en [`docs/Database/modelo-de-datos.md`](../docs/Database/modelo-de-datos.md).

## Contrato con la API

- Al abrir cada conexión, la API ejecuta `sp_set_session_context 'UserId', @id` y `'CorrelationId', @cid` (interceptor de EF Core).
  El trigger de historial los usa; sin ellos toma `CreatedByUserId`/`UpdatedByUserId`.
- `rec.usp_RunReconciliation` y `stg.usp_PromoteBankMovements` manejan su propia transacción: llámalos **sin** transacción externa,
  así la cabecera `FAILED` sobrevive a un error.
- La API traduce los errores de negocio según este catálogo.

## Catálogo de errores

| Número | Origen | Significado | HTTP |
|---|---|---|---|
| 50010 | Trigger | Cambiar datos financieros de una operación no pendiente | 409 |
| 50011 | Trigger | Eliminar una operación | 409 |
| 50012 | Trigger | Transición desde un estado cerrado | 409 |
| 50090 / 50091 | Trigger | Modificar o borrar auditoría / historial | 409 |
| 50101 | Conciliación | Cuenta inexistente o inactiva | 404 |
| 50102 | Conciliación | Período invertido | 400 |
| 50103 | Conciliación | Período mayor a 93 días | 400 |
| 50104 | Conciliación | Ya hay una conciliación en curso para la cuenta | 409 |
| 50105 | Conciliación | Tolerancia fuera de 0–5 días | 400 |
| 50201 | ETL | Empresa inexistente o inactiva | 404 |
| 50202 | ETL | Ejecución inexistente o ya terminada | 409 |
| 50203 | ETL | Origen distinto de ORACLE o CSV | 400 |
| 50204 | ETL | Parámetros de depuración inválidos | 400 |
| 50300 | Reportes | Sin `reports.read` | 403 |
| 50301 | Reportes | Período inválido | 400 |
| 50302 | Reportes | Granularidad inválida | 400 |
| 50303 | Reportes | Conciliación inexistente o no autorizada | 404 |
| 50304 | Reportes | Actividad de usuarios sin `audit.read` | 403 |
| 50305 | Reportes (SSRS) | Cuenta de Windows sin asociar a un usuario activo (`rpt.usp_ReportUsers`) | — |
| 50999 | Pruebas | Afirmación fallida (solo en `tests/`) | — |

## Recorrido de demostración

Después de `.\deploy.ps1 -Recreate -IncludeDemo` (usuarios con contraseña `FinCore*2026`):

```sql
-- 1. La carga ETL de Bogotá: 9 leídas, 4 cargadas, 5 rechazadas y por qué
SELECT * FROM etl.vw_ExecutionSummary;
SELECT RowNumber, SourceKey, ErrorCode, ErrorMessage FROM etl.ETLExecutionDetails ORDER BY RowNumber;

-- 2. Conciliar Montería del 1 al 15 de noviembre
DECLARE @acc INT = (SELECT Id FROM fin.Accounts WHERE Code = N'MON-001'), @rid INT;
EXEC rec.usp_RunReconciliation @AccountId = @acc, @PeriodFrom = '2026-11-01', @PeriodTo = '2026-11-15',
     @DateToleranceDays = 1, @ExecutedBy = N'demo', @CorrelationId = N'demo-001', @ReconciliationId = @rid OUTPUT;

-- 3. Lo que hay que revisar
SELECT ResultTypeName, InternalReference, BankReference, InternalAmount, BankAmount, DifferenceAmount, Notes
FROM rec.vw_OpenDifferences ORDER BY ResultTypeCode;

-- 4. Historia de una operación: quién, cuándo, qué petición
SELECT h.*, u.UserName FROM fin.TransactionStatusHistory h
JOIN fin.Transactions t ON t.Id = h.TransactionId LEFT JOIN sec.Users u ON u.Id = h.ChangedByUserId
WHERE t.Reference = N'TRF-1001' ORDER BY h.Id;

-- 5. Reporte por sucursal del supervisor (con operador.monteria falla con 50300: no tiene reports.read)
DECLARE @sup INT = (SELECT Id FROM sec.Users WHERE UserName = N'supervisor');
EXEC rpt.usp_BranchSummary @UserId = @sup, @DateFrom = '2026-11-01', @DateTo = '2026-11-30';
```

## Pruebas

| Archivo | Qué verifica |
|---|---|
| `test_00_static_checks` | Sin referencias a objetos inexistentes, todas las tablas con PK, FK confiables, catálogos completos |
| `test_01_reconciliation_matching` | Cada tipo de resultado, borde del período, efectos, historial con CorrelationId, repetición, duplicado contra conciliado |
| `test_02_reconciliation_validation` | Errores 50101–50105 y ejecución abandonada |
| `test_03_promote_bank_movements` | Cada código de rechazo, normalización, balance de conteos, recarga idempotente, 50202 |
| `test_04_transaction_guards` | 50010, 50011, 50012, transiciones permitidas, historial, restricciones CHECK/UNIQUE |
| `test_05_append_only` | Auditoría e historial de solo inserción; JSON válido |
| `test_06_authorization_and_reports` | Alcance por sucursal, totales, saldo con reversión, permisos de reportes |
