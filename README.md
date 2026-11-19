# FinCore

**Plataforma de operaciones y conciliación financiera.** Una empresa con varias sucursales registra ingresos, egresos,
transferencias y pagos; FinCore los centraliza, los concilia contra los movimientos del banco, detecta cada diferencia
y deja trazabilidad de quién hizo qué, cuándo y en qué petición.

**C# / .NET 10 · ASP.NET Core · Angular · SQL Server · Oracle · SSIS · SSRS · Node.js · Windows Service**

```
 Angular ──REST──▶ FinCore.Api ──▶ Application ──▶ Domain           SQL Server
                       ▲            (casos de uso)  (reglas)         ├─ sec   seguridad y alcance por sucursal
 Proveedor ─webhook─▶ Node.js                                        ├─ fin   operaciones + historial (trigger)
 bancario   (HMAC)   (adaptador)   Infrastructure ──────────────────▶├─ rec   motor de conciliación set-based
                                   (EF Core, SQL, Oracle)            ├─ stg/etl staging + historial de cargas
 Oracle ──SSIS──▶ staging ─────────────────────────────────────────▶ ├─ audit auditoría de solo inserción
 SSRS ◀── rpt.* ◀────────────────────────────────────────────────────└─ rpt   reportes (API y SSRS)
 FinCore.Worker (Windows Service): conciliación nocturna, reintentos, depuración
```

## Estado

| Etapa | Contenido | Estado |
|---|---|---|
| 1 · Fundamentos | Documentación, base de datos, solución .NET 10 en capas, autenticación JWT, usuarios y sucursales, Angular, Docker | ✅ |
| 2 · Operaciones | Cuentas con saldo, registro con idempotencia, procesar con doble control, rechazar, revertir, historial y auditoría | ✅ |
| 3 · Conciliación | Importación CSV con rechazos por línea, conciliación desde la app, bandeja de diferencias y resolución con nota | ✅ |
| 4 · Integraciones | Adaptador Node.js de webhooks, Oracle legado con extracción incremental, Worker como Windows Service; SSIS diseñado | ✅ (falta construir el `.dtsx` en Visual Studio) |
| 5 · Reportería | Página Reportes (indicadores, gráfico, resumen por sucursal, actividad, CSV auditado) y 5 informes SSRS publicados por script; cada cuenta de Windows ve en SSRS solo lo de su usuario | ✅ |
| 6 · Calidad y producción | Pruebas, optimización, monitoreo, Docker, CI/CD | 🟡 9 archivos T-SQL · 98 pruebas .NET · 16 Angular · 16 Node; todo en Docker |

## Empezar

Requisito: **Docker Desktop**. No hace falta tener .NET, Node ni SQL Server instalados.

```powershell
docker compose up -d --build      # base + migración + API + web (el primer arranque tarda unos minutos)
docker compose ps                 # db y api "healthy"; db-migrator "Exited (0)"
start http://localhost:4300
```

| Qué | Dónde |
|---|---|
| Aplicación web | http://localhost:4300 |
| Documentación de la API (Scalar / OpenAPI) | http://localhost:5080/scalar · http://localhost:5080/openapi/v1.json |
| SQL Server (SSMS, Azure Data Studio) | `localhost,14333` · usuario `sa` · contraseña `FinCore_Sa_2026!` (solo desarrollo) |
| Oracle legado (SQL Developer) | `localhost:1523/FREEPDB1` · usuario `legacy_bank` · contraseña `Legacy_Bank_2026` |
| Adaptador de webhooks | http://localhost:5090/health · `docker compose --profile demo run --rm bank-simulator` |

Usuarios de demo: `admin`, `supervisor`, `operador.monteria`, `operador.bogota`, `auditor`, `svc.integration` — contraseña `FinCore*2026`.
Entra con `operador.monteria` y luego con `admin` para ver cómo cambian el menú, los permisos y las sucursales.

```powershell
docker compose --profile test run --rm --build api-tests   # .NET: unitarias, arquitectura e integración contra la base
docker compose --profile test run --rm db-tests            # T-SQL
docker compose --profile test run --rm --build web-tests   # Angular
```

Detalles, variables y decisiones: [docs/Deployment/docker.md](docs/Deployment/docker.md). Para la base sin Docker y el
[recorrido de demostración](database/README.md#recorrido-de-demostración) (carga ETL con rechazos y conciliación con los siete
tipos de resultado), ver [database/README.md](database/README.md).

## Documentación

| Documento | Para qué |
|---|---|
| [PRD.md](PRD.md) | Problema, alcance, métricas, reglas de negocio (RN, RC, RI) |
| [docs/presentacion/videos](docs/presentacion/video/LEEME.md) | **5 videos narrados** grabados sobre la app real: camino feliz, operaciones, conciliación, reportes y SSRS, seguridad |
| [HISTORIAS_DE_USUARIO.md](HISTORIAS_DE_USUARIO.md) | 31 historias con criterios de aceptación y la prueba que verifica cada una |
| [openspec/](openspec/project.md) | Comportamiento observable, contratos REST por módulo y tareas `[x]` / `[ ]` por etapa |
| [docs/Architecture](docs/Architecture/README.md) | Capas, flujos, dónde vive cada regla y decisiones (ADR) |
| [docs/Database](docs/Database/modelo-de-datos.md) | Diagrama entidad-relación, tablas, estados e índices |
| [database/](database/README.md) | Cómo desplegar, cambiar y probar el esquema; catálogo de errores |
| [docs/Deployment](docs/Deployment/docker.md) | Servicios Docker, puertos, variables, pruebas en contenedores |
| [CLAUDE.md](CLAUDE.md) | Reglas del proyecto para personas y asistentes de IA |

## Estructura

```text
FinCore/
├── database/                 ✅ scripts versionados + deploy.ps1 + pruebas T-SQL + rol de mínimo privilegio
├── backend/                  ✅ FinCore.Api · Application · Domain · Infrastructure · Worker (Windows Service) · Tests
├── frontend/fincore-angular/ ✅ Angular 22 + Material 3: login, inicio, operaciones, conciliación, reportes, usuarios, sucursales
├── infrastructure/docker/    ✅ Dockerfiles (api, web, migrador) y nginx
├── docker-compose.yml        ✅ db · db-migrator · api · web · perfiles de prueba
├── integrations/fincore-node/✅ adaptador de webhooks (Node 24 + TypeScript + Express)
├── etl/FinCore.Integration.SSIS/ 🟡 diseño del paquete y SQL de cada tarea (validados); falta el .dtsx
├── reporting/FinCore.Reports/    ✅ generador de RDL (Node) + publicación REST en SSRS
├── openspec/                 ✅ specs por módulo
└── docs/                     ✅ Architecture (ADR), Database, Deployment
```

## Cómo cubre los requisitos de la vacante

| Requisito | Evidencia |
|---|---|
| SQL Server | 24 tablas con FK compuestas, CHECK y columnas calculadas; índices filtrados documentados; procedimientos set-based con `UPDLOCK`, `OUTPUT`, `STRING_AGG`, `TRY_CONVERT`; funciones en línea; triggers `INSTEAD OF` y `AFTER`; `SESSION_CONTEXT`; pruebas T-SQL |
| Procesamiento de datos / ETL | Staging de texto crudo, 8 validaciones por fila, historial de cargas con balance garantizado, recarga idempotente |
| Reglas de negocio | Máquina de estados con reversión y doble control, conciliación con 7 resultados, alcance por sucursal |
| Soporte a producción | CorrelationId hasta la base, historial de estados por trigger, auditoría inmutable, cabeceras de ejecución con el error, catálogo de errores |
| C# / .NET | Clean Architecture verificada con NetArchTest; casos de uso con `Result`; EF Core mapeando el esquema sin migraciones; JWT + refresh token rotado con detección de reutilización; políticas por permiso; `ProblemDetails`; Serilog JSON; health checks; rate limiting; pruebas unitarias y de integración con `WebApplicationFactory` |
| Angular | Angular 22 zoneless con signals y Material 3 con tema propio (claro y oscuro), degradados y animaciones; tablas con paginación del servidor y filas intercaladas; formularios reactivos con errores por campo de la API; interceptor con renovación de sesión única ante 401; guards por permiso |
| Docker | Compose con healthchecks y orden de arranque, migrador que reutiliza `deploy.ps1`, imágenes multi-etapa no root, nginx con CSP, pruebas en contenedores |
| Windows Service | `FinCore.Worker` con `UseWindowsService`: 4 trabajos programados con CorrelationId por ejecución; el mismo binario corre en Docker |
| Node.js | Adaptador con firma HMAC anti-replay, traducción sin reglas, cola con reintentos exponenciales y dead letter; 16 pruebas `node:test` |
| Oracle · SSIS | Oracle 23ai en Docker como sistema legado; extracción incremental por marca de agua con una consulta compartida entre el Worker y el diseño del paquete SSIS |
| SSRS | 5 informes RDL 2016 (tablas con filas intercaladas, gráficos, tarjetas, parámetros en cascada) generados desde código y publicados con la API REST v2.0; origen compartido con un login que solo puede ejecutar `rpt.*`; mismos procedimientos que la API |
| Documentación · Scrum/Kanban · Code Review | PRD, historias con criterios verificables, specs, ADR; Conventional Commits y PR por historia |
