# Despliegue con Docker

Todo FinCore corre en Docker: la base, la migración, la API, la web y las tres suites de pruebas.

```
                      ┌──────────── red fincore_default ────────────────────────────┐
 navegador ──:4300──▶ │ web (nginx)  ──/api, /health──▶  api (.NET 10)  ──▶  db      │
                      │  SPA Angular                      :8080             SQL 2022 │
 Scalar  ────:5080──▶ │                                   ▲                  :1433   │
 SSMS    ───:14333──▶ │                    db-migrator ───┘ (deploy.ps1) ────▶       │
                      └─────────────────────────────────────────────────────────────┘
```

| Servicio | Imagen | Qué hace | Puerto en el equipo |
|---|---|---|---|
| `db` | `mcr.microsoft.com/mssql/server:2022-latest` | SQL Server Developer con volumen `sqldata` | 14333 |
| `db-migrator` | `infrastructure/docker/db-migrator.Dockerfile` | PowerShell 7 + sqlcmd: ejecuta `database/deploy.ps1 -IncludeDemo -AppLogin …` y termina | — |
| `api` | `infrastructure/docker/backend.Dockerfile` (`runtime`) | FinCore.Api, usuario no root, healthcheck `/health/ready` | 5080 (docs en `/scalar`) |
| `web` | `infrastructure/docker/frontend.Dockerfile` (`runtime`) | Angular compilado + nginx con proxy a la API y cabeceras de seguridad | 4300 |
| `oracle` | `gvenzl/oracle-free:23-slim-faststart` | Sistema bancario legado; el primer arranque crea `LEGACY_BANK` (`infrastructure/oracle/init`) | 1523 |
| `worker` | `backend.Dockerfile` (`worker`) | FinCore.Worker: importación Oracle (60 s), recuperación de webhooks, conciliación nocturna, depuración | — |
| `bank-adapter` | `infrastructure/docker/node-adapter.Dockerfile` | Adaptador Node.js de webhooks, con volumen para el dead letter | 5090 |
| `bank-simulator` | perfil `demo` | Envía webhooks firmados de prueba al adaptador | — |
| `db-test-migrator` | perfil `test` | Recrea desde cero la base `FinCore_Tests` (con la demo) antes de cada corrida de pruebas | — |
| `api-tests` · `db-tests` · `web-tests` · `node-tests` | perfil `test` | Pruebas .NET y T-SQL contra `FinCore_Tests`; pruebas Angular y del adaptador Node | — |

Orden de arranque: `db` sano → `db-migrator` termina bien → `api` sana → `web` y `bank-adapter`; el `worker` espera además a `oracle` sano.
Si la migración falla, la API no arranca. Oracle usa unos 2 GB de RAM y tarda alrededor de un minuto en el primer arranque.

## Comandos

```powershell
docker compose up -d --build                         # todo -> http://localhost:4300
docker compose ps                                    # db/api "healthy", db-migrator "Exited (0)"
docker compose logs -f api                           # logs JSON con CorrelationId
docker compose run --rm db-migrator                  # volver a aplicar cambios de database/ (incremental)

docker compose --profile test run --rm --build api-tests   # 66 pruebas .NET (30 de integración contra FinCore_Tests)
docker compose --profile test run --rm db-tests            # 7 archivos de pruebas T-SQL
docker compose --profile test run --rm --build web-tests   # pruebas Angular (Vitest)
docker compose --profile test run --rm --build node-tests  # pruebas del adaptador Node

docker compose --profile demo run --rm bank-simulator      # webhooks firmados de prueba (ver docker compose logs bank-adapter)
docker exec -i fincore-oracle sqlplus -s legacy_bank/Legacy_Bank_2026@//localhost/FREEPDB1 < infrastructure/oracle/scripts/nuevo_movimiento.sql
                                                           # movimiento nuevo en Oracle: el Worker lo extrae en el siguiente ciclo
docker compose logs -f worker                              # trabajos del Worker con su CorrelationId

docker compose down                                  # detiene; conserva los datos
docker compose down -v                               # detiene y BORRA la base (la próxima subida la recrea con la demo)
```

## Configuración

Sin `.env`, `docker-compose.yml` usa valores **de desarrollo**. Para cambiarlos copia `.env.example` a `.env` (está en `.gitignore`).

| Variable | Uso |
|---|---|
| `MSSQL_SA_PASSWORD` | Administrador de SQL Server. Solo lo usan el migrador y las pruebas |
| `DB_APP_USER` / `DB_APP_PASSWORD` | Login de la API, miembro de `fincore_app_role`: sin `DELETE` ni DDL. La contraseña no puede contener el nombre del login (política de SQL Server) |
| `JWT_SIGNING_KEY` | Firma de los access tokens (32+ caracteres) |
| `DB_PORT` · `API_PORT` · `WEB_PORT` | Puertos publicados (14333 · 5080 · 4300) |

La API recibe su configuración por variables de entorno (`ConnectionStrings__FinCore`, `Jwt__SigningKey`, `Api__ExposeDocs`…).

## Decisiones

- **El migrador reutiliza `deploy.ps1`**: el mismo script, la misma bitácora y los mismos checksums dentro y fuera de Docker.
- **Mismo origen**: nginx sirve la SPA y reenvía `/api`; la cookie del refresh token (`SameSite=Strict`) funciona sin CORS.
  Los navegadores aceptan cookies `Secure` en `http://localhost`; fuera de localhost la web debe servirse con HTTPS.
- **nginx con `resolver`**: vuelve a resolver el nombre `api`, así recrear el contenedor de la API no deja la web en 502.
- **nginx habla HTTP/1.1 con Kestrel**: con HTTP/1.0 un `POST` sin cuerpo llega sin `Content-Length` y Kestrel responde 400.
- **Base de pruebas aparte (`FinCore_Tests`)**: las operaciones no se pueden borrar (por diseño), así que probar contra la base de la demo la llenaría de datos de prueba. `db-test-migrator` la recrea en cada corrida: siempre se prueba sobre datos conocidos.
- **Pruebas de integración con `sa`**: consultan y limpian tablas directamente; el login de la aplicación casi no tiene `DELETE` a propósito.

## Desarrollo sin reconstruir imágenes

```powershell
docker compose stop api web                          # libera el puerto 5080 si el stack completo está arriba
docker compose up -d db db-migrator                  # solo la base en localhost,14333
cd backend; dotnet run --project FinCore.Api         # http://localhost:5080 (appsettings.Development.json apunta a la base de Docker)
cd frontend/fincore-angular; npm start               # http://localhost:4200 con proxy a :5080
```
