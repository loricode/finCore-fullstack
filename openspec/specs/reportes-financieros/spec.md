# Reportes financieros

Los reportes de la API y los de SSRS usan los **mismos procedimientos** `rpt.*`: un solo cálculo, dos presentaciones.
SSRS ejecuta con una cuenta de servicio y pasa el usuario final como `@UserId`; cada procedimiento valida permisos y alcance.

## Endpoints

| Método | Ruta | Procedimiento | Permiso |
|---|---|---|---|
| GET | `/api/v1/reports/indicators?from&to` | `rpt.usp_FinancialIndicators` | `reports.read` |
| GET | `/api/v1/reports/branch-summary?from&to&format` | `rpt.usp_BranchSummary` | `reports.read` |
| GET | `/api/v1/reports/operations-by-period?from&to&granularity&branchId&format` | `rpt.usp_OperationsByPeriod` | `reports.read` |
| GET | `/api/v1/reports/user-activity?from&to&format` | `rpt.usp_UserActivity` | `reports.read` + `audit.read` |

`format`: `json` (por defecto) o `csv`. Exportar exige `reports.export` y se audita como `EXPORT` (EntityName `Report`, EntityId = nombre
del reporte, con el CorrelationId de la petición).
- Período por defecto: del día 1 del mes en curso a hoy (hora de Colombia). Máximo 366 días; `to` < `from` → 400 (`validation.to`).
- CSV: separador `;`, UTF-8 con BOM (Excel en es-CO lo abre bien), decimales sin separador de miles, fechas `yyyy-MM-dd`. Los textos que
  empiezan por `= + - @` se prefijan con `'` para evitar inyección de fórmulas.
- El detalle de una conciliación se consulta en `GET /api/v1/reconciliations/{id}` (módulo de conciliación) y en el informe SSRS
  *Conciliación*; Excel, PDF y Word se obtienen desde SSRS, no desde la API.

## Requisito: definición de los totales
- **Efectiva**: operación `PROCESSED` o `RECONCILED` que no es una reversión.
- **Ingresos** = suma de efectivas con valor positivo. **Egresos** = suma de efectivas con valor negativo (en positivo).
- **Neto** = Ingresos − Egresos.
- Las pendientes, rechazadas, revertidas y reversiones NO DEBEN sumar en ingresos ni egresos; sí cuentan en los conteos por estado.
- **Saldo** de una cuenta (`fin.tvf_AccountBalance`): suma con signo de `PROCESSED`, `RECONCILED` y `REVERSED` + reversiones; un par
  original/reversión suma cero.

### Escenario: totales por sucursal
- **Dado** ingreso de 1.000.000 y egreso de 300.000 efectivos, un ingreso pendiente de 777.000, un egreso rechazado y un pago de 200.000 revertido
- **Entonces** ingresos 1.000.000, egresos 300.000, neto 700.000, 6 operaciones, 1 pendiente, 1 revertida
- **Y** aparece también la sucursal autorizada sin operaciones, con ceros

## Requisito: seguridad
| Condición | Error | HTTP |
|---|---|---|
| Sin `reports.read` | 50300 | 403 |
| Período inválido | 50301 | 400 |
| Granularidad distinta de `DAY`/`MONTH` | 50302 | 400 |
| Conciliación inexistente o de sucursal no autorizada | 50303 | 404 |
| Actividad de usuarios sin `audit.read` | 50304 | 403 |

## Requisito: indicadores financieros
`rpt.usp_FinancialIndicators` devuelve una fila: conteos por estado, ingresos/egresos/neto (misma definición de efectiva),
**cobertura de conciliación** = valor de las conciliadas / valor efectivo × 100, **diferencias abiertas** (conteo y valor absoluto de las que
requieren acción y no están resueltas) y calidad del ETL (filas leídas, rechazadas y % de rechazo de las cargas del período).

## Requisito: informes SSRS
`reporting/FinCore.Reports` contiene cinco informes (RDL 2016 generados por `build-rdl.mjs` y publicados con `deploy-reports.ps1`):

| Informe | Procedimiento | Parámetros |
|---|---|---|
| Resumen por sucursal (gráfico + tabla con totales) | `rpt.usp_BranchSummary` | UserId, DateFrom, DateTo |
| Operaciones por período (gráfico por tipo + tabla) | `rpt.usp_OperationsByPeriod` | UserId, DateFrom, DateTo, Granularity, BranchId (nullable) |
| Conciliación (tarjetas de cabecera + detalle) | `rpt.usp_ReconciliationReport` | UserId, ReconciliationId (en cascada) |
| Indicadores financieros (tarjetas) | `rpt.usp_FinancialIndicators` | UserId, DateFrom, DateTo |
| Actividad por usuario | `rpt.usp_UserActivity` | UserId, DateFrom, DateTo |

- Las listas de los parámetros salen de `rpt.usp_ReportUsers`, `rpt.usp_ReportBranches` y `rpt.usp_ReportReconciliations`.
- Origen de datos compartido `/FinCore/Datos/FinCore` con credenciales guardadas del login `fincore_reports` (rol `fincore_reports_role`:
  solo `EXECUTE` sobre el esquema `rpt`; no puede leer tablas).
- Paleta de la aplicación, filas intercaladas, encabezado con período y usuario, pie con página y aviso de confidencialidad.

## Requisito: identidad en SSRS (HU-31)
- Cada informe DEBE obtener la lista del parámetro `UserId` ("Ver como") de `rpt.usp_ReportUsers @WindowsAccount = User!UserID`,
  con valor por defecto la primera fila.
- La cuenta se busca en `sec.UserWindowsAccounts` (una cuenta por usuario y un usuario por cuenta, sin distinguir mayúsculas,
  formato `DOMINIO\usuario`). Sin asociación o con el usuario inactivo → 50305. Sin `reports.read` → 50300.
- La lista DEBE contener solo al usuario asociado, salvo que tenga `reports.viewas` (rol ADMIN): entonces incluye además a los usuarios
  activos de su empresa con `reports.read`. SSRS valida el valor contra la lista, también por URL.
- `PUT /api/v1/users/{id}/windows-account` (`users.manage`) asocia, cambia o (con `null`) quita la cuenta; se audita como
  `USER_WINDOWS_ACCOUNT` con antes y después. 400 formato inválido · 409 cuenta de otro usuario.

### Escenario: la supervisora no puede ver como el administrador
- **Dado** `EQUIPO\laura` asociada a la supervisora
- **Cuando** abre *Resumen por sucursal* con `&UserId=1` en la URL
- **Entonces** SSRS responde "el parámetro UserId no es un valor válido" y no ejecuta `rpt.usp_BranchSummary`
