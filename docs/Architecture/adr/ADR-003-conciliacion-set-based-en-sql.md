# ADR-003 · Emparejamiento de conciliación set-based en SQL, orquestado por .NET

**Estado**: aceptada · 2026-11-19

## Contexto
Conciliar es comparar miles de operaciones contra miles de movimientos. Traer ambos conjuntos a memoria en .NET, comparar
y escribir resultado por resultado es lento y abre ventanas de concurrencia. Por otro lado, la regla general del proyecto es
que las reglas de negocio viven en el dominio.

## Decisión
- El **emparejamiento** (clasificar operaciones y movimientos en los 7 resultados) y sus efectos (marcar conciliadas) se hacen en
  `rec.usp_RunReconciliation`, con operaciones de conjunto, `UPDLOCK` sobre las filas evaluadas y una transacción.
- El **caso de uso** `RunReconciliation` en Application valida permiso, alcance de la cuenta y parámetros, fija `UserId` y
  `CorrelationId`, invoca el procedimiento mediante una interfaz (`IReconciliationEngine`) y devuelve el resumen.
- La transición `PROCESSED → RECONCILED` es la única que hace SQL. El resto de transiciones son del dominio.
- Resolver diferencias es lógica de dominio en .NET (no está en el procedimiento).

## Consecuencias
- Rendimiento y atomicidad con un plan de ejecución que se puede analizar y optimizar con índices.
- La regla de emparejamiento está en un solo lugar y tiene pruebas T-SQL (`test_01`, `test_02`).
- Cambiar las reglas de emparejamiento exige saber T-SQL; por eso la spec `conciliacion-bancaria` documenta cada regla con ejemplos.
