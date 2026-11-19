# Tareas · Etapa 5 · Reportería

## 1. Base de datos
- [x] 1.1 `rpt.usp_FinancialIndicators` (cobertura de conciliación, diferencias abiertas, calidad del ETL)
- [x] 1.2 Listas de parámetros: `rpt.usp_ReportUsers`, `rpt.usp_ReportBranches`, `rpt.usp_ReportReconciliations`
- [x] 1.3 Rol `fincore_reports_role` (solo `EXECUTE` sobre `rpt`) y login de reportes en `deploy.ps1 -ReportsLogin`
- [x] 1.4 `test_07_indicators_and_lookups.sql`

## 2. API
- [x] 2.1 HU-23 · `GET /reports/branch-summary`
- [x] 2.2 HU-25 · `GET /reports/operations-by-period` (DAY/MONTH, sucursal opcional)
- [x] 2.3 HU-26 · `GET /reports/user-activity` (exige `audit.read`)
- [x] 2.4 `GET /reports/indicators`
- [x] 2.5 Exportación CSV (`reports.export`), protegida contra inyección de fórmulas y auditada como `EXPORT`
- [x] 2.6 `CsvWriterTests` y `ReportsApiTests`

## 3. Angular
- [x] 3.1 Página Reportes: período, tarjetas de indicadores y barra de estados
- [x] 3.2 Gráfico SVG de entradas y salidas por día o mes (`period-chart.component.spec.ts`)
- [x] 3.3 Resumen por sucursal y actividad por usuario con exportación
- [x] 3.4 Ruta `/reportes` con `permissionGuard`, ítem de menú y enlace al portal de SSRS

## 4. Reporting Services
- [x] 4.1 Generador de RDL 2016 (`build-rdl.mjs`) con la paleta y filas intercaladas
- [x] 4.2 HU-24 · Informes: resumen por sucursal, operaciones por período, conciliación, indicadores y actividad
- [x] 4.3 `deploy-reports.ps1`: carpeta, origen de datos compartido con credenciales guardadas e informes (REST v2.0)
- [x] 4.4 Verificación renderizando a PNG, PDF y CSV con datos reales
- [x] 4.5 Guía `docs/Deployment/ssrs.md`
