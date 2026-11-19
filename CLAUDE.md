# CLAUDE.md

Guía para asistentes de IA (y para personas) que trabajan en este repositorio.

## Qué es

FinCore: plataforma de operaciones y conciliación financiera para una empresa con varias sucursales. Las sucursales registran
operaciones; el motor de conciliación las compara con los movimientos bancarios (CSV, Oracle vía SSIS, webhooks vía Node.js);
los reportes salen de la API y de SSRS. Ver [`PRD.md`](PRD.md), [`HISTORIAS_DE_USUARIO.md`](HISTORIAS_DE_USUARIO.md) y
[`docs/Architecture`](docs/Architecture/README.md).

**Estado**: Etapas 1 a 5 terminadas (seguridad; operaciones; conciliación; integraciones: Node.js, Oracle y Worker; reportería).
Existen `database/`, `backend/` (sin Worker), `frontend/fincore-angular/` e `infrastructure/docker/`.
Pendiente: el `.dtsx` de SSIS en Visual Studio (diseño en `etl/`). Etapa 6 en curso: hecha la identidad en SSRS (HU-31,
`sec.UserWindowsAccounts` + `reports.viewas`); faltan CI/CD, monitoreo y optimización.
SSRS corre en Windows, fuera de Docker: `reporting/FinCore.Reports` (`node build-rdl.mjs` y luego `./deploy-reports.ps1`). Los `.rdl`
se generan: se edita `build-rdl.mjs`, no el XML. Ver [`docs/Deployment/ssrs.md`](docs/Deployment/ssrs.md).

## Comandos

**Todo corre en Docker** (ver [`docs/Deployment/docker.md`](docs/Deployment/docker.md)):

```powershell
docker compose up -d --build                               # db + migración + api + web -> http://localhost:4300
docker compose run --rm db-migrator                        # aplicar cambios de database/ (incremental)
docker compose --profile test run --rm --build api-tests   # pruebas .NET (incluye integración contra la base)
docker compose --profile test run --rm db-tests            # pruebas T-SQL
docker compose --profile test run --rm --build web-tests   # pruebas Angular
docker compose --profile test run --rm --build node-tests  # pruebas del adaptador Node (tipos + node:test)
docker compose --profile demo run --rm bank-simulator      # webhooks firmados de prueba
```

Sin Docker (rápido mientras se programa): `cd backend; dotnet test` (las de integración se omiten sin `FINCORE_TEST_CONNECTION`),
`cd frontend/fincore-angular; npm run test:ci`, `cd database; .\deploy.ps1` contra un SQL Server local.

## Reglas del proyecto

### Base de datos
- **El esquema vive solo en `database/`.** EF Core mapea las tablas, nunca genera migraciones ni llama `EnsureCreated` (ADR-002).
- **Nunca edites un script versionado ya aplicado** (`001_`, `002_`, `003_`, `009_`): crea uno nuevo con el siguiente número.
  Funciones, vistas, procedimientos, triggers y semillas se editan en su archivo (`CREATE OR ALTER` / `MERGE`).
- Un objeto nuevo o modificado lleva su prueba en `database/tests/` y pasa `run-tests.ps1`.
- Errores de negocio en SQL: `THROW 50xxx` dentro del rango del módulo y agregados al catálogo de `database/README.md`.
- Procedimientos con `SET NOCOUNT ON; SET XACT_ABORT ON;` y TRY/CATCH que revierte y relanza. `rec.usp_RunReconciliation`
  y `stg.usp_PromoteBankMovements` manejan su transacción: se llaman sin transacción externa.
- Dinero `DECIMAL(18,2)` en SQL y `decimal` en C#; nunca `double`/`float`. Instantes en UTC (`...Utc`); fechas de negocio en hora de Colombia.
- `Amount` es positivo y `Direction` da el signo; compara siempre `SignedAmount` con el banco.

### Backend (.NET 10)
- **Capas**: Api → Application → Domain; Infrastructure implementa interfaces de Application. Domain no referencia nada.
  Los controladores no usan `DbContext` ni repositorios; los casos de uso no conocen HTTP. Lo verifican pruebas de arquitectura.
- **Reglas financieras en Domain** (máquina de estados, doble control, reversión). La única excepción documentada es el
  emparejamiento de la conciliación en SQL (ADR-003). No dupliques reglas en Node.js ni en Angular.
- **Autorización** por permiso (`[Authorize(Policy = "operations.create")]`), nunca por nombre de rol; y siempre filtra por
  las sucursales autorizadas del usuario en el servidor.
- **Casos de uso**: una clase `sealed` `XxxHandler` con `HandleAsync` que devuelve `Result<T>`; errores esperados como `Error`,
  no como excepciones. Se registran en `FinCore.Application/DependencyInjection.cs`. Sin MediatR.
- **Paquetes**: versiones solo en `backend/Directory.Packages.props` (línea 10.0.x de Microsoft; las 11.x requieren .NET 11).
  Las fuentes NuGet están fijadas en `backend/nuget.config`.
- **Conexión SQL**: un interceptor fija `sp_set_session_context` `UserId` y `CorrelationId` en cada conexión.
  La API usa el login de `fincore_app_role` (sin `DELETE`): si un caso de uso necesita borrar, es una señal de diseño a revisar.
- **EF Core y triggers**: una tabla con trigger se mapea con `ToTable(..., t => t.HasTrigger("..."))`.
- **Idempotencia**: altas con `Idempotency-Key`; webhooks por `(Provider, ExternalEventId)`; cada trabajo del Worker repetible.
- **Logs**: nunca contraseñas, tokens, números de cuenta completos ni payloads bancarios.
- Errores con `ProblemDetails`; errores SQL 50xxx traducidos según el catálogo.

### Integraciones (Etapa 4)
- **Node.js** (`integrations/fincore-node`): solo verifica firmas, traduce y reintenta. Nada de reglas financieras ni acceso a SQL.
  TypeScript con sintaxis borrable (`erasableSyntaxOnly`): sin enums ni parameter properties; imports con extensión `.ts`.
- **Oracle**: la consulta de extracción vive en `etl/FinCore.Integration.SSIS/sql/oracle_extract_movements.sql` y se incrusta en
  `FinCore.Infrastructure`. Es la misma para el Worker y para SSIS: no se duplica.
- **Worker**: un trabajo = una clase que hereda de `ScheduledJob` y llama a un caso de uso. Programación en `Worker:Jobs:{Nombre}`.
  La identidad es la cuenta de servicio (`WorkerExecutionContext`); cada ejecución abre su propio CorrelationId.

### Frontend (Angular)
- Angular 22 zoneless: componentes standalone, signals, `inject()` y rutas con carga diferida. Pruebas con Vitest.
- **Diseño "sala de estar"** (Angular Material 3): paleta del verde azulado del sofá, lino, ocre, rosa empolvado, salvia,
  lavanda y roble. Colores solo con los tokens `--fc-*` de `src/styles.scss` (claro y "noche en casa"), nunca hexadecimales en componentes.
  - Botón principal: `matButton="filled" class="fc-sofa-btn"`. Secundarios: `matButton="tonal" class="fc-pastel fc-pastel--sage|blush|ochre|lavender|teal"`.
  - Estados: `<span class="fc-status fc-status--PENDING">`. Tablas: `mat-table` con `class="fc-table"` dentro de `.fc-table-card` (filas intercaladas).
  - Entrada animada: `class="fc-enter" [style.--i]="n"`. Respeta `prefers-reduced-motion`.
  - Fuentes e íconos alojados en la app (`@fontsource-variable/*`, `material-symbols`): la CSP de nginx no permite CDNs.
  - `inlineCritical` está desactivado en el build: Angular lo carga con un `onload` en línea que la CSP bloquea.
- Llamadas a la API solo desde `core/api/fincore-api.service.ts`; errores por campo con `applyFieldErrors`.
- Fechas: `EsDateAdapter` lee y muestra `dd/MM/yyyy` (el adaptador nativo interpretaría `1/11/2026` como 11 de enero).
- Procedimientos SQL con su propia transacción (`rec.usp_RunReconciliation`, `stg.usp_PromoteBankMovements`) se llaman con ADO
  sobre la conexión del DbContext (`Infrastructure/Reconciliation/SqlProcedures.cs`), nunca dentro de `ExecuteInTransactionAsync`.
- El access token vive solo en memoria (`AuthService`); nunca en `localStorage`. El refresh token es una cookie HttpOnly.
- Ocultar un botón no es seguridad: la API decide.

### Proceso
- **Specs**: si un cambio altera un comportamiento, actualiza la spec de `openspec/specs/` en el mismo commit.
- **Tareas**: el trabajo vive en `openspec/changes/<cambio>/tasks.md` (`[x]` hecha · `[ ]` pendiente). Marca `[x]` solo lo verificado,
  agrega como `[ ]` lo que descubras que falta y archiva el cambio en `changes/archive/` cuando todo esté en `[x]`.
  `node openspec/progress.mjs` muestra el avance. Convención completa en [`openspec/project.md`](openspec/project.md#tareas).
- **Historias**: al terminar una historia, marca su estado y la prueba que la verifica en `HISTORIAS_DE_USUARIO.md`.
- **Commits**: Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`, `db:` para scripts de base de datos).
- **Pull Requests**: uno por historia o cambio de esquema, con la prueba que lo verifica.
