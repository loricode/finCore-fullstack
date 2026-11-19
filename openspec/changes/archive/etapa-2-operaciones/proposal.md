# Etapa 2 · Operaciones financieras

## Por qué
Las sucursales necesitan registrar ingresos, egresos, pagos y transferencias con control: estados claros, doble control para montos
altos, reversión en vez de borrado y evidencia de quién hizo cada cambio.

## Qué cambia
- Máquina de estados en el dominio (`Transaction`): PENDING → PROCESSED / REJECTED → RECONCILED / REVERSED.
- Registro idempotente (`Idempotency-Key`), concurrencia optimista (RowVersion) y doble control desde 10 millones.
- Reversión con operación espejo; historial de estados por trigger con usuario y CorrelationId.
- Cuentas con saldo a la fecha; página de operaciones con detalle, alta y acciones.

## Specs afectadas
[operaciones-financieras](../../../specs/operaciones-financieras/spec.md)

## Historias
HU-06 a HU-12.
