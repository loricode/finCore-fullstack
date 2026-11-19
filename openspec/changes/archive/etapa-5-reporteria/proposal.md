# Etapa 5 · Reportería

## Por qué
La gerencia necesita ver cómo van las sucursales y la conciliación, y exportarlo. Los procedimientos `rpt.*` existían y estaban
probados; faltaba mostrarlos en la aplicación y en Reporting Services con un solo cálculo para ambos.

## Qué cambia
- Indicadores financieros (`rpt.usp_FinancialIndicators`) y listas de parámetros para SSRS.
- Endpoints de reportes en JSON y CSV; exportación auditada.
- Página Reportes en Angular con indicadores, gráfico, resumen por sucursal y actividad por usuario.
- Cinco informes SSRS generados desde código y publicados con la API REST; login de solo `EXECUTE` sobre `rpt`.

## Specs afectadas
[reportes-financieros](../../../specs/reportes-financieros/spec.md)

## Historias
HU-23 a HU-26.
