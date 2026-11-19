# Operaciones financieras

## Endpoints

| Método | Ruta | Permiso | Respuesta |
|---|---|---|---|
| GET | `/api/v1/transactions?branchId&accountId&status&type&from&to&reference&page&pageSize` | `operations.read` | 200 paginado |
| GET | `/api/v1/transactions/{publicId}` | `operations.read` | 200 `{ transaction, history, actions }` · 404 |
| GET | `/api/v1/transactions/summary` | `operations.read` | 200 pendientes, estados del mes, ingresos/egresos/neto del mes |
| GET | `/api/v1/accounts` | `operations.read` | 200 cuentas de las sucursales autorizadas con saldo a hoy (número enmascarado) |
| POST | `/api/v1/transactions` (cabecera `Idempotency-Key`) | `operations.create` | 201 · 200 (reintento) · 400 · 403 · 422 |
| PUT | `/api/v1/transactions/{publicId}` (cabecera `If-Match`) | `operations.create` | 200 · 409 · 412 |
| POST | `/api/v1/transactions/{publicId}/process` | `operations.process` | 200 · 409 |
| POST | `/api/v1/transactions/{publicId}/reject` `{ reason }` | `operations.reject` | 200 · 400 · 409 |
| POST | `/api/v1/transactions/{publicId}/reverse` `{ reason }` | `operations.reverse` | 201 con la reversión · 409 |

El detalle incluye `history` (de `fin.TransactionStatusHistory`, con usuario y CorrelationId) y `actions`
(`canEdit`, `canProcess`, `canReject`, `canReverse`), calculadas por la API según estado, permisos y doble control.
La interfaz solo muestra los botones que `actions` permite; la API vuelve a validar cada acción.

## Requisito: validación del registro
La API DEBE rechazar con 400 y el error de cada campo una operación que incumpla RN-01 a RN-04.

### Escenario: datos inválidos
- **Dado** valor 0, fecha de mañana, referencia `" ab"` y un pago sin contraparte
- **Cuando** se envía `POST /api/v1/transactions`
- **Entonces** 400 con `title = "Datos inválidos"` y `errors` contiene `amount`, `operationDate`, `reference` y `counterpartyName`

### Escenario: alta correcta
- **Cuando** se registra una operación válida
- **Entonces** 201 con `Location`, estado `PENDING`, `direction` según el tipo, `createdBy` = usuario autenticado
- **Y** el historial tiene `NULL → PENDING`

## Requisito: idempotencia
Reenviar un alta con la misma `Idempotency-Key` NO DEBE crear otra operación.

### Escenario: reintento
- **Dado** una operación registrada con la clave `k`
- **Cuando** se reenvía el mismo contenido con `k`
- **Entonces** 200 con la operación original
### Escenario: conflicto de clave
- **Cuando** se envía otro contenido con `k`
- **Entonces** 422 `"La Idempotency-Key ya se usó con otros datos"`

## Requisito: máquina de estados
Las transiciones DEBEN ser exactamente:

```
             process            (motor de conciliación)
 PENDING ───────────────▶ PROCESSED ───────────────▶ RECONCILED
    │                        │                           │
    │ reject (motivo)        │ reverse (motivo)          │ reverse (motivo)
    ▼                        ▼                           ▼
 REJECTED                 REVERSED ◀─────────────────────┘
 (final)                  (final)
```

La máquina vive en `FinCore.Domain` (`Transaction`). La base de datos protege lo irreversible (`fin.trg_Transactions_Guard`).

### Escenario: doble control
- **Dado** una operación de $10.000.000 registrada por `u1`
- **Cuando** `u1` intenta procesarla
- **Entonces** 409 `"Una operación de $10.000.000 o más debe procesarla otra persona"`

### Escenario: transición inválida
- **Dado** una operación `REJECTED`
- **Cuando** se intenta procesar
- **Entonces** 409; si el código lo intentara directamente en la base, falla con 50012

### Escenario: conciliar manualmente
- **Entonces** no existe endpoint para pasar a `RECONCILED`: solo lo hace `rec.usp_RunReconciliation`

## Requisito: edición solo en pendiente
Solo una operación `PENDING` PUEDE editarse, por su creador o por un supervisor, con concurrencia optimista.

### Escenario: edición concurrente
- **Dado** dos usuarios que leyeron la misma versión
- **Cuando** ambos guardan
- **Entonces** el segundo recibe 412 (el `RowVersion` cambió)

### Escenario: editar una procesada
- **Entonces** 409; en la base, cambiar valor, referencia, fecha, cuenta, sucursal, tipo o sentido falla con 50010

## Requisito: reversión
Revertir DEBE crear una operación de tipo `REV` con el mismo valor, sentido contrario, `OriginalTransactionId` y motivo,
en estado `PROCESSED`, y pasar la original a `REVERSED`, en una sola transacción. La reversión tiene la referencia
`REV-` + la original (recortada a 40 caracteres) y la fecha de negocio del día; su descripción incluye el motivo.

### Escenario: reversión de una conciliada
- **Dado** una operación `RECONCILED` de −150.000
- **Cuando** se revierte
- **Entonces** existe `REV-…` por +150.000 `PROCESSED`, la original queda `REVERSED` y el saldo de la cuenta vuelve al valor anterior

### Escenario: segunda reversión
- **Cuando** se intenta revertir otra vez la misma operación, o revertir una reversión
- **Entonces** 409 (en la base: `UX_Transactions_OriginalTransactionId`)

## Requisito: no se eliminan operaciones
NO DEBE existir endpoint de borrado. Un `DELETE` en la base falla con 50011.
