# Tareas · Etapa 3 · Conciliación bancaria

## 1. Base de datos
- [x] 1.1 `stg.usp_PromoteBankMovements`: 8 validaciones por fila, carga y conteos balanceados
- [x] 1.2 `rec.usp_RunReconciliation`: 7 tipos de resultado, tolerancia de fechas, duplicados y cabecera `FAILED` ante error
- [x] 1.3 Vistas `rec.vw_ReconciliationSummary` y `rec.vw_OpenDifferences`

## 2. API
- [x] 2.1 HU-13 · Importar un extracto CSV con rechazos por línea
- [x] 2.2 HU-14 · Conciliar una cuenta y un período
- [x] 2.3 HU-15 · Repetir una conciliación sin duplicar resultados
- [x] 2.4 HU-16 · Una conciliación a la vez por cuenta (409)
- [x] 2.5 HU-17 · Bandeja de diferencias abiertas
- [x] 2.6 HU-18 · Resolver una diferencia con nota, auditada

## 3. Angular
- [x] 3.1 Pestaña Bandeja con resolución
- [x] 3.2 Pestaña Conciliaciones: nueva conciliación y detalle con conteos por resultado
- [x] 3.3 Pestaña Extractos: carga del CSV y resultado por línea

## 4. Pruebas
- [x] 4.1 `test_01` a `test_03` (motor, validaciones y promoción)
- [x] 4.2 `ReconciliationApiTests`
