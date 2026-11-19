# FinCore.Worker como Windows Service

El Worker ejecuta los procesos programados de FinCore. Es el mismo binario en los dos entornos:

| Entorno | Cómo corre |
|---|---|
| Windows Server | Servicio de Windows (`UseWindowsService`: integra inicio, parada y registro de eventos con el Administrador de servicios) |
| Docker / Linux | Proceso de consola (servicio `worker` de `docker-compose.yml`); `UseWindowsService` no hace nada fuera de Windows |

Se verificó que el ejecutable nativo `FinCore.Worker.exe` arranca en Windows contra la base y el Oracle de Docker
(inicia como `svc.integration` y respeta la marca de agua). La instalación con `sc.exe` requiere permisos de administrador
y no se ejecutó en el equipo de desarrollo.

## Trabajos

| Trabajo | Programación por defecto | Qué hace | Historia |
|---|---|---|---|
| `LegacyBankImport` | cada 300 s (60 s en Docker) + al iniciar | Extrae de Oracle los movimientos posteriores a la marca de agua y los carga por staging | HU-20 |
| `InboundEventRecovery` | cada 60 s | Procesa webhooks que quedaron recibidos sin procesar (la API cayó a mitad del proceso) | HU-19 |
| `NightlyReconciliation` | 02:00 (hora de Colombia) | Concilia todas las cuentas activas del día anterior (`DaysBack`) | HU-21 |
| `StagingPurge` | 03:00 | Borra por lotes el staging de cargas terminadas hace más de 30 días | HU-22 |

Cada ejecución tiene su CorrelationId (`legacybankimport-…`, `nightlyreconciliation-…`), que aparece en los logs,
en `etl.ETLExecutions`, en `rec.Reconciliations` y en la auditoría. Un trabajo que falla se registra y se reintenta en
la siguiente ejecución; no detiene el servicio.

El Worker actúa como la cuenta de servicio `svc.integration` (configurable en `Worker:ServiceUser`): la auditoría y el
historial de estados registran ese usuario, y el alcance por sucursal se calcula con sus permisos (`branches.all`).

## Instalación en Windows Server

```powershell
# 1. Publicar (requiere el runtime de ASP.NET Core 10 en el servidor)
dotnet publish backend\FinCore.Worker -c Release -r win-x64 --self-contained false -o C:\FinCore\Worker

# 2. Configuración: variables de entorno del sistema o C:\FinCore\Worker\appsettings.Production.json
#    ConnectionStrings__FinCore     login de la aplicación (fincore_app_role)
#    ConnectionStrings__LegacyBank  User Id=...;Password=...;Data Source=servidor-oracle:1521/SERVICIO
#    Jwt__SigningKey                cualquier valor de 32+ caracteres (el Worker no emite tokens)

# 3. Crear el servicio con inicio automático retrasado y reinicio ante fallas
sc.exe create "FinCoreWorker" binPath= "C:\FinCore\Worker\FinCore.Worker.exe" start= delayed-auto DisplayName= "FinCore Worker"
sc.exe description "FinCoreWorker" "FinCore: importación Oracle, conciliación nocturna, recuperación de webhooks y depuración de staging"
sc.exe failure "FinCoreWorker" reset= 86400 actions= restart/60000/restart/60000/restart/300000

# 4. Iniciar y revisar
sc.exe start "FinCoreWorker"
Get-Service FinCoreWorker
```

Cuenta de ejecución: un usuario de dominio o virtual (`NT SERVICE\FinCoreWorker`) sin privilegios de administrador.
Si SQL Server usa autenticación de Windows, ese usuario debe ser miembro de `fincore_app_role`.

Logs: JSON por la salida estándar (con `Logging__Json=false`, texto legible). En un servicio de Windows conviene
agregar un sink de archivo o del registro de eventos en `appsettings.Production.json` (sección `Serilog`).

## Desinstalación

```powershell
sc.exe stop "FinCoreWorker"
sc.exe delete "FinCoreWorker"
```

## Ejecutarlo en desarrollo (sin instalar)

```powershell
docker compose up -d db db-migrator oracle
$env:DOTNET_ENVIRONMENT = "Development"   # appsettings.Development.json apunta a localhost,14333 y localhost:1523
dotnet run --project backend\FinCore.Worker
```
