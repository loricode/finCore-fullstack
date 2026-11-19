# Tareas · Etapa 1 · Fundamentos

## 1. Documentación
- [x] 1.1 PRD, historias de usuario con criterios verificables y specs por módulo
- [x] 1.2 ADR de arquitectura (monolito modular, esquema en scripts sin migraciones de EF, conciliación set-based)

## 2. Base de datos
- [x] 2.1 Esquemas, tablas con llaves foráneas compuestas, CHECK y columnas calculadas
- [x] 2.2 Índices (incluye filtrados), funciones, vistas, procedimientos y triggers de protección
- [x] 2.3 Catálogos: roles, permisos y su matriz; datos de demostración (`-IncludeDemo`)
- [x] 2.4 `deploy.ps1` incremental con bitácora `dbo.SchemaVersions` (SHA-256) y `-Recreate`
- [x] 2.5 Rol `fincore_app_role` sin DDL y sin DELETE (salvo tablas de relación) y login de la API
- [x] 2.6 Framework de pruebas T-SQL y `run-tests.ps1`

## 3. API
- [x] 3.1 Solución en capas verificada con NetArchTest; casos de uso con `Result<T>`
- [x] 3.2 HU-01 · Login con JWT de 15 min, refresh token rotado en cookie HttpOnly y detección de reutilización
- [x] 3.3 HU-01 · Bloqueo por intentos fallidos y rate limit en el login
- [x] 3.4 Políticas por permiso (`PermissionPolicyProvider`) y alcance por sucursal (`sec.tvf_AuthorizedBranches`)
- [x] 3.5 HU-02 · Crear, editar (con RowVersion), activar y desactivar usuarios
- [x] 3.6 HU-04 · Asignar sucursales a un usuario
- [x] 3.7 HU-05 · Auditoría en la misma transacción, sin contraseñas ni tokens; solo inserción en la base
- [x] 3.8 ProblemDetails, CorrelationId hasta `SESSION_CONTEXT`, Serilog JSON y health checks
- [ ] 3.9 HU-03 · Endpoints para crear y editar sucursales (`branches.manage`); hoy solo existe `GET /api/v1/branches`
- [ ] 3.10 HU-03 · Endpoints para crear y editar cuentas bancarias (`accounts.manage`); hoy solo existe `GET /api/v1/accounts`

## 4. Angular
- [x] 4.1 Tema Material 3 con la paleta de la sala (claro y oscuro), degradados, animaciones y filas intercaladas
- [x] 4.2 Login con modo demostración, "recordarme" y recuperación de contraseña
- [x] 4.3 Interceptor con renovación única ante 401, guards por permiso y página 403
- [x] 4.4 Páginas de inicio, usuarios (con formulario de roles y sucursales) y sucursales (consulta)
- [ ] 4.5 HU-03 · Formularios para crear y editar sucursales y cuentas

## 5. Docker
- [x] 5.1 Compose con `db`, `db-migrator`, `api` y `web` (nginx con CSP), healthchecks y orden de arranque
- [x] 5.2 Perfil `test`: base de pruebas recreada, `api-tests`, `db-tests` y `web-tests`
