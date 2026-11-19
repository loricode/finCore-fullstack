# Importación de extractos (CSV y Oracle)

```
 CSV (API) ─────┐
                ├──▶ stg.BankMovements ──▶ stg.usp_PromoteBankMovements ──▶ rec.BankStatements / Details
 Oracle (SSIS) ─┘     (texto crudo)           (valida, rechaza, carga)       etl.ETLExecutions / Details
```

Ambos orígenes comparten el staging y el procedimiento de promoción: la validación existe una sola vez.

## Endpoints

| Método | Ruta | Permiso |
|---|---|---|
| POST | `/api/v1/bank-statements/import` (multipart, campo `file`) → `{ etlExecutionId, status, rowsRead, rowsLoaded, rowsRejected, errors[] }` | `statements.import` |
| GET | `/api/v1/etl-executions?page&pageSize` (CSV y Oracle, la más reciente primero) | `reconciliation.read` |
| GET | `/api/v1/etl-executions/{id}/errors` (hasta 1.000 rechazos con línea, movimiento, código y mensaje) | `reconciliation.read` |

Pendiente (no implementado): listado de extractos `GET /api/v1/bank-statements` y filtros por estado o fecha del historial.

`statements.import` es un permiso de alcance empresa: los roles que lo tienen también tienen `branches.all`.
Un archivo que no se puede leer (no es .csv, sin encabezado, faltan columnas, más de 20.000 filas o más de 5 MB) responde 400
sin abrir una ejecución ETL. La carga a staging usa `SqlBulkCopy` (un solo viaje a la base). Se audita `STATEMENT_IMPORTED`.

Formato CSV (UTF-8, separador `;` o `,` detectado en el encabezado, columnas por nombre, comillas dobles opcionales):
`account_number;movement_id;reference;date;amount;description` — fecha `yyyy-MM-dd`, valor con punto decimal y signo.

## Requisito: ciclo de una ejecución
Toda carga DEBE abrirse con `etl.usp_StartExecution`, cargar filas crudas con su número de fila y cerrarse con
`stg.usp_PromoteBankMovements`, o con `etl.usp_FailExecution` si falla antes de promover.

## Requisito: validación por fila
Cada fila DEBE validarse en este orden; se registra el primer error:

| Código | Condición |
|---|---|
| `MISSING_MOVEMENT_ID` | Sin identificador del banco |
| `INVALID_MOVEMENT_ID` | Identificador de más de 60 caracteres |
| `ACCOUNT_NOT_FOUND` | La cuenta bancaria no existe o está inactiva en la empresa de la ejecución |
| `INVALID_REFERENCE` | Referencia de más de 40 caracteres |
| `INVALID_DATE` | Fecha que no está en `yyyy-MM-dd` |
| `INVALID_AMOUNT` | No numérico, cero o con más de 2 decimales (p. ej. `1.200.000` o `10.555`) |
| `DUPLICATE_IN_FILE` | Mismo identificador y cuenta repetidos en la carga (se conserva la primera fila) |
| `ALREADY_LOADED` | Ya existe en `rec.BankStatementDetails` |

La referencia se normaliza (sin espacios, en mayúsculas) y el número de cuenta se compara sin espacios en los extremos.

### Escenario: carga con errores
- **Dado** el CSV de demostración de Banco de Bogotá con 9 filas
- **Entonces** la ejecución queda `COMPLETED_WITH_ERRORS` con `RowsRead = 9`, `RowsLoaded = 4`, `RowsRejected = 5`

### Escenario: balance
- **Entonces** al terminar `RowsRead = RowsLoaded + RowsRejected` (restricción `CK_ETLExecutions_Balance`)

### Escenario: recarga
- **Dado** un archivo ya cargado
- **Cuando** se carga de nuevo
- **Entonces** todas sus filas se rechazan como `ALREADY_LOADED` y no se crea un extracto vacío

### Escenario: falla
- **Si** la promoción falla a mitad de camino
- **Entonces** no queda ningún movimiento ni rechazo de esa ejecución y la ejecución queda `FAILED` con el error
- **Dado** una ejecución que ya terminó, **cuando** se intenta promover, **entonces** error 50202

## Requisito: extracción incremental desde Oracle (Etapa 4)
El trabajo `LegacyBankImport` del Worker y el paquete SSIS DEBEN leer solo los movimientos con `MOVEMENT_SEQ` mayor que la marca de
agua de `etl.Watermarks` (`ORACLE:LEGACY_BANK.BANK_MOVEMENTS`), con la consulta compartida
`etl/FinCore.Integration.SSIS/sql/oracle_extract_movements.sql`, y mover la marca al mayor valor leído después de promover.
Una falla entre promover y mover la marca solo provoca rechazos `ALREADY_LOADED` en la siguiente ejecución, nunca duplicados.

## Requisito: paquete SSIS (alternativa en Windows)
El paquete `ImportOracleBankMovements.dtsx` DEBE: (1) ejecutar `etl.usp_StartExecution` y guardar el Id en una variable;
(2) leer de Oracle los movimientos posteriores a la última carga exitosa; (3) convertir fecha y valor a texto con los formatos
de staging; (4) cargar `stg.BankMovements`; (5) ejecutar `stg.usp_PromoteBankMovements`; (6) en `OnError` ejecutar
`etl.usp_FailExecution`. No DEBE contener reglas de validación propias: las del procedimiento son la única fuente.

## Requisito: depuración
El Worker DEBE borrar a diario, por lotes, el staging de ejecuciones terminadas hace más de 30 días (`stg.usp_PurgeStaging`).
