# FinCore.Integration.SSIS

Extracción de movimientos del sistema bancario legado (Oracle · `LEGACY_BANK.BANK_MOVEMENTS`) hacia FinCore con
SQL Server Integration Services. Es la alternativa Windows/SQL Agent al trabajo `LegacyBankImport` del `FinCore.Worker`:
**las dos usan la misma consulta de Oracle, la misma tabla de staging, los mismos procedimientos y la misma marca de agua**.

> **Estado:** el SQL de cada tarea está en `sql/` y se valida en cada corrida del Worker (la consulta de Oracle se
> incrusta en `FinCore.Infrastructure`). El archivo `.dtsx` **aún no existe**: se construye en Visual Studio con la
> extensión *SQL Server Integration Services Projects* siguiendo este diseño. No se generó a mano porque un paquete
> que no se puede abrir ni validar con SSIS sería peor que no tenerlo. En el equipo de desarrollo hay `DTExec.exe`
> (SQL Server 2025) para ejecutarlo una vez creado.

## Por qué SSIS no corre en Docker

SSIS es un componente de Windows: el diseñador vive en Visual Studio y el motor (`DTExec`, catálogo SSISDB, SQL Agent)
en SQL Server sobre Windows. En Docker el mismo flujo lo ejecuta el Worker (ver `docs/Deployment/docker.md`).

## Paquete `ImportOracleBankMovements.dtsx`

### Administradores de conexión

| Nombre | Tipo | Destino |
|---|---|---|
| `FinCore` | OLE DB (Microsoft OLE DB Driver for SQL Server) | `localhost,14333` · base `FinCore` · login de la aplicación |
| `LegacyBank` | ADO.NET (Oracle Data Provider) o *Microsoft Connector for Oracle* | `localhost:1523/FREEPDB1` · usuario `legacy_bank` |

Contraseñas: parámetros del proyecto marcados como sensibles, asignados en el entorno del catálogo SSISDB (nunca en el `.dtsx`).

### Variables

| Variable | Tipo | Uso |
|---|---|---|
| `User::CompanyId` | Int32 | Empresa destino (1 en la demo) |
| `User::TriggeredBy` | String | `sql-agent` |
| `User::LastSequence` | Int64 | Marca de agua leída |
| `User::EtlExecutionId` | Int64 | Id de la ejecución abierta |
| `User::MaxSequence` | Int64 | Mayor `MOVEMENT_SEQ` leído |
| `User::RowsRead` | Int32 | Filas del lote (Row Count) |

### Flujo de control

```
[1] Leer marca de agua ──▶ [2] Abrir ejecución ETL ──▶ [3] Data Flow: Oracle → staging ──▶ [4] Validar y cargar ──▶ [5] Mover marca de agua
                                                            │ (RowsRead = 0: termina sin promover)
OnError (paquete) ──▶ [99] Marcar ejecución FAILED
```

| Paso | Tarea | SQL |
|---|---|---|
| 1 | Execute SQL Task (Single row → `LastSequence`) | [`sql/01_get_watermark.sql`](sql/01_get_watermark.sql) |
| 2 | Execute SQL Task (parámetro OUTPUT → `EtlExecutionId`) | [`sql/02_start_execution.sql`](sql/02_start_execution.sql) |
| 3 | Data Flow Task | ver abajo |
| 4 | Execute SQL Task | [`sql/03_promote.sql`](sql/03_promote.sql) |
| 5 | Execute SQL Task | [`sql/04_set_watermark.sql`](sql/04_set_watermark.sql) |
| 99 | Execute SQL Task en el manejador OnError | [`sql/99_on_error.sql`](sql/99_on_error.sql) |

### Data Flow

| Componente | Configuración |
|---|---|
| Origen ADO.NET `LegacyBank` | Consulta [`sql/oracle_extract_movements.sql`](sql/oracle_extract_movements.sql) con `:last_sequence` = `User::LastSequence` y `:max_rows` = 5000 (expresión de la propiedad *SqlCommand*) |
| Row Count | → `User::RowsRead` |
| Aggregate (salida secundaria con Multicast) | `MAX(MOVEMENT_SEQ)` → Script Component que asigna `User::MaxSequence` |
| Derived Column | `EtlExecutionId = @[User::EtlExecutionId]` |
| Destino OLE DB `FinCore` · tabla `stg.BankMovements` (fast load) | `EtlExecutionId`, `ROW_NUMBER → RowNumber`, `ACCOUNT_NO → AccountNumber`, `MOVEMENT_ID → ExternalMovementId`, `REFERENCE → BankReference`, `BOOKING_DATE → MovementDate`, `AMOUNT → Amount`, `NARRATIVE → Description` |

**La transformación** (fecha a `YYYY-MM-DD`, valor con punto decimal) la hace la consulta de Oracle; **la validación**
la hace `stg.usp_PromoteBankMovements`. El paquete no tiene reglas propias: si cambia una regla, cambia en un solo lugar.

## Despliegue y programación

1. Compilar el proyecto en Visual Studio (`.ispac`) y desplegarlo en el catálogo SSISDB (carpeta `FinCore`).
2. Crear un entorno con las cadenas de conexión y contraseñas; referenciarlo desde el proyecto.
3. Job de SQL Agent "FinCore · Importación Oracle" con un paso *SQL Server Integration Services Package* cada 15 minutos.
4. Si se usa SSIS, **desactivar** el trabajo del Worker (`Worker__Jobs__LegacyBankImport__Enabled=false`) para no extraer dos veces
   (no duplicaría datos gracias a `ALREADY_LOADED`, pero generaría rechazos innecesarios).

Ejecución manual de prueba:

```powershell
& "C:\Program Files\Microsoft SQL Server\170\DTS\Binn\DTExec.exe" /File ImportOracleBankMovements.dtsx /Rep EW
```

## Verificación

Cada ejecución queda en `etl.vw_ExecutionSummary` (leídos, cargados, rechazados y códigos de error) y la marca de agua en
`etl.Watermarks`. Para generar un movimiento nuevo en Oracle: `infrastructure/oracle/scripts/nuevo_movimiento.sql`.
