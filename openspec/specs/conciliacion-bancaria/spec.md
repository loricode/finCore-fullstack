# Conciliación bancaria

Implementación: [`rec.usp_RunReconciliation`](../../../database/006_Procedures/004_rec_usp_RunReconciliation.sql),
orquestado por el caso de uso `RunReconciliation` de `FinCore.Application` ([ADR-003](../../../docs/Architecture/adr/ADR-003-conciliacion-set-based-en-sql.md)).

## Endpoints

| Método | Ruta | Permiso | Respuesta |
|---|---|---|---|
| POST | `/api/v1/reconciliations` `{ accountId, periodFrom, periodTo, dateToleranceDays }` | `reconciliation.run` | 201 con el resumen · 400 · 404 · 409 |
| GET | `/api/v1/reconciliations?accountId&from&to&page` | `reconciliation.read` | 200 |
| GET | `/api/v1/reconciliations/{id}` | `reconciliation.read` | 200 resumen (`rec.vw_ReconciliationSummary`) |
| GET | `/api/v1/reconciliations/{id}/details?resultType` | `reconciliation.read` | 200 |
| GET | `/api/v1/reconciliations/open-differences?branchId` | `reconciliation.read` | 200 (`rec.vw_OpenDifferences`) |
| POST | `/api/v1/reconciliation-details/{id}/resolve` `{ note }` | `reconciliation.resolve` | 200 · 400 · 409 |

La ejecución y la resolución se auditan (`RECONCILIATION_RUN`, `RECONCILIATION_RESOLVED`). El caso de uso valida permiso y alcance
de la cuenta (fuera de alcance: 404) y llama al procedimiento sin transacción externa.

## Requisito: insumos de la conciliación
Para una cuenta y un período `[desde, hasta]` con tolerancia `t` días (0 a 5), el motor DEBE evaluar:
- operaciones de la cuenta con fecha en el período en estado `PROCESSED`, o `REVERSED` sin conciliar;
- operaciones `PENDING` del período, solo para reportarlas;
- movimientos bancarios `UNMATCHED` con fecha en `[desde − t, hasta + t]`. Los que están fuera del período solo sirven
  para emparejar; si no emparejan se ignoran (se evaluarán en su propio período).

## Requisito: clasificación
Cada operación y cada movimiento del período DEBE recibir exactamente un resultado, en este orden:

| # | Resultado | Condición |
|---|---|---|
| 6 | `DUPLICATE_REFERENCE` | La referencia bancaria aparece en más de un movimiento de la cuenta (no ignorado), incluso si uno ya está conciliado. Ni la operación ni los movimientos se concilian. |
| 1 | `MATCHED` | Misma referencia, mismo valor con signo, `|fecha banco − fecha operación| ≤ t`. |
| 2 | `AMOUNT_MISMATCH` | Misma referencia, valor distinto. |
| 3 | `DATE_MISMATCH` | Misma referencia y valor, fecha fuera de la tolerancia. |
| 7 | `PENDING_REVIEW` | Operación pendiente (con el movimiento del banco si existe). |
| 4 | `MISSING_IN_BANK` | Operación sin movimiento con su referencia. |
| 5 | `MISSING_INTERNAL` | Movimiento del período sin operación con su referencia (o sin referencia). |

`DifferenceAmount = BankAmount − InternalAmount` (lo ausente vale 0).

### Escenario: el ejemplo del negocio
- **Dado** la transferencia interna `TRF-1001` por −$500.000 del 3 de noviembre
- **Y** el movimiento bancario `TRF-1001` por −$500.000 del 3 de noviembre
- **Cuando** se concilia del 1 al 15 de noviembre con tolerancia 1
- **Entonces** resultado `MATCHED`, la operación queda `RECONCILED` con `ReconciledAtUtc` y el movimiento `MATCHED` apuntando a ella

### Escenario: datos de demostración
- **Dado** los datos de `database/009_Demo` (cuenta MON-001)
- **Entonces** 4 `MATCHED`, 1 `AMOUNT_MISMATCH` (−5.000), 1 `DATE_MISMATCH`, 1 `MISSING_IN_BANK`, 1 `MISSING_INTERNAL`,
  2 `DUPLICATE_REFERENCE`, 1 `PENDING_REVIEW`; la rechazada no participa

### Escenario: borde del período
- **Dado** una operación del 15 de noviembre que el banco registra el 16
- **Cuando** se concilia hasta el 15 con tolerancia 1
- **Entonces** `MATCHED`

### Escenario: par de reversión
- **Dado** `PAG-1009` (revertida) y `REV-1009`, ambos en el banco
- **Entonces** ambos `MATCHED`; `PAG-1009` conserva el estado `REVERSED` y recibe `ReconciledAtUtc`

## Requisito: repetible
Ejecutar de nuevo la conciliación del mismo período NO DEBE conciliar dos veces ni modificar lo conciliado,
y DEBE reevaluar todo lo demás. Cada ejecución guarda sus propios resultados; la bandeja usa la última.

## Requisito: validación y concurrencia
| Condición | Error SQL | HTTP |
|---|---|---|
| Cuenta inexistente o inactiva | 50101 | 404 |
| `desde > hasta` | 50102 | 400 |
| Período de más de 93 días | 50103 | 400 |
| Ya hay una conciliación `RUNNING` para la cuenta | 50104 | 409 |
| Tolerancia fuera de 0–5 | 50105 | 400 |

### Escenario: ejecución colgada
- **Dado** una conciliación `RUNNING` iniciada hace más de 30 minutos
- **Cuando** se inicia otra
- **Entonces** la anterior pasa a `FAILED` ("Ejecución abandonada") y la nueva se ejecuta

### Escenario: falla durante la ejecución
- **Si** ocurre un error después de crear la cabecera
- **Entonces** se revierten todos los efectos, la cabecera queda `FAILED` con número, procedimiento, línea y mensaje del error, y el error se propaga

## Requisito: trazabilidad
La cabecera DEBE guardar usuario o proceso que ejecutó, `CorrelationId`, inicio, fin y conteos. El cambio a `RECONCILED`
DEBE quedar en el historial de estados con ese `CorrelationId`.

## Requisito: resolución de diferencias
Resolver un resultado que requiere acción DEBE exigir nota y registrar usuario y fecha, sin modificar el resultado.
Un resultado ya resuelto no se resuelve otra vez (409). `MATCHED` no se resuelve.
