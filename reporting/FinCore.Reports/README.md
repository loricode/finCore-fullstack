# FinCore.Reports · informes de SQL Server Reporting Services

Cinco informes sobre los procedimientos `rpt.*`, los mismos que usa la API: un solo cálculo, dos presentaciones.

| Informe | Contenido |
|---|---|
| `ResumenPorSucursal` | Gráfico de ingresos y egresos por sucursal y tabla con estados y totales |
| `OperacionesPorPeriodo` | Gráfico por día o mes, una serie por tipo de operación, y tabla; filtro opcional por sucursal |
| `Conciliacion` | Tarjetas con la cuenta y los conteos, y detalle de cada resultado (abierta/resuelta) |
| `IndicadoresFinancieros` | Tarjetas: ingresos, egresos, neto, cobertura de conciliación, diferencias abiertas y calidad del ETL |
| `ActividadPorUsuario` | Operaciones, valor, cambios de estado y acciones auditadas por usuario (exige `audit.read`) |

## Cómo está hecho

- `build-rdl.mjs` genera los `.rdl` (esquema 2016) desde definiciones en código: la paleta de la aplicación, la tipografía y las
  filas intercaladas son iguales en todos los informes, y los cambios se revisan como código. Los `.rdl` generados se versionan y
  abren en Report Builder, pero **los cambios se hacen en el generador**.
- `deploy-reports.ps1` publica con la API REST v2.0: carpeta `/FinCore`, origen compartido `/FinCore/Datos/FinCore` y los informes.

```powershell
node build-rdl.mjs
./deploy-reports.ps1
```

## Quién ve qué

El parámetro **Ver como** sale de `rpt.usp_ReportUsers @WindowsAccount = User!UserID`: cada cuenta de Windows ve solo los datos del
usuario de FinCore al que está asociada (pantalla **Usuarios**). Solo quien tiene `reports.viewas` (ADMIN) puede elegir a otro.

```powershell
./deploy-reports.ps1 -Browsers 'EMPRESA\Contabilidad'   # da lectura en /FinCore a un grupo o cuenta de Windows
./demo-windows-users.ps1                                # (como administrador) cuentas locales para Laura y Marta, asociadas y probadas
./demo-windows-users.ps1 -Remove                        # deshace la demostración
```

Guía completa (requisitos, verificación por URL, seguridad e identidad):
[`docs/Deployment/ssrs.md`](../../docs/Deployment/ssrs.md).
