# Despliegue de los informes en SQL Server Reporting Services

SSRS es un servicio de Windows: no corre en Docker. Lee la base de FinCore (que sí puede estar en Docker) por TCP con un login propio
de solo lectura. Probado con SSRS 2022 (16.0) en el mismo equipo que `docker compose`.

```
 Navegador ──▶ http://localhost/Reports  (portal SSRS, autenticación Windows)
                    │  origen compartido /FinCore/Datos/FinCore
                    ▼
              localhost,14333  ── contenedor fincore-db ──  EXEC rpt.*  (login fincore_reports)
```

## Requisitos

- SSRS 2022 (o 2019/2017) configurado con *Report Server Configuration Manager* (base `ReportServer` y URLs `/ReportServer` y `/Reports`).
- La cuenta de Windows que publica con rol **Administrador de contenido** en la carpeta raíz (los administradores locales lo tienen
  por defecto).
- Node.js 20+ para generar los `.rdl`.
- La base desplegada con el login de reportes. `docker compose up` ya lo hace (`DB_REPORTS_USER` / `DB_REPORTS_PASSWORD` en `.env`);
  fuera de Docker: `./database/deploy.ps1 -ReportsLogin fincore_reports -ReportsPassword <clave>`.

## Publicar

```powershell
cd reporting/FinCore.Reports
node build-rdl.mjs                       # genera Reports/*.rdl
./deploy-reports.ps1                     # crea /FinCore, el origen de datos y sube los informes (idempotente)

# Otro servidor:
./deploy-reports.ps1 -PortalUrl http://reportes01/Reports -SqlServer "sql01,1433" -ReportsPassword $env:REPORTS_PASSWORD
```

El script usa la API REST v2.0 del portal con la identidad de Windows actual. Cada ejecución vuelve a crear el origen de datos y los
informes (las suscripciones o el historial de los informes se pierden: está pensado para entornos de desarrollo y CI).

Si PowerShell bloquea el script: `powershell -ExecutionPolicy Bypass -File ./deploy-reports.ps1`.

## Verificar

Portal: <http://localhost/Reports/browse/FinCore>. Desde la aplicación, el botón **Reporting Services** de la página Reportes abre esa
carpeta (`APP_SETTINGS.ssrsPortalUrl` en Angular).

Por URL (útil en CI), renderizando a PNG, PDF o CSV:

```powershell
$u = 'http://localhost/ReportServer?/FinCore/ResumenPorSucursal&rs:Format=PDF&UserId=1&DateFrom=2026-11-01&DateTo=2026-11-19'
Invoke-WebRequest -UseDefaultCredentials $u -OutFile resumen.pdf
```

Formatos: `PDF`, `EXCELOPENXML`, `WORDOPENXML`, `CSV`, `IMAGE` (con `rc:OutputFormat=PNG`). Un parámetro nulo se pasa como
`BranchId:isnull=true`.

## Seguridad

- El login `fincore_reports` está en `fincore_reports_role`: solo `EXECUTE` sobre el esquema `rpt`. No puede leer tablas aunque alguien
  escriba una consulta propia en Report Builder.
- Cada procedimiento `rpt.*` valida `reports.read` (y `audit.read` para la actividad de usuarios) y filtra por las sucursales del
  `@UserId`. Un usuario sin permiso hace que el informe falle (error 50300/50304 en el log de SSRS).
- Las credenciales quedan cifradas en la base `ReportServer`; nunca están en los `.rdl`.

### Identidad: cuenta de Windows → usuario de FinCore

SSRS autentica con Windows y entrega la cuenta en `User!UserID`. Cada informe la pasa a `rpt.usp_ReportUsers @WindowsAccount`, que
la busca en `sec.UserWindowsAccounts` y devuelve la lista del parámetro **Ver como**:

| Quién abre el informe | Ver como |
|---|---|
| Cuenta asociada a un usuario con `reports.read` | Solo ese usuario (valor por defecto, marcado "· yo") |
| … y además con `reports.viewas` (rol ADMIN) | Ese usuario primero y los demás usuarios de la empresa con `reports.read` |
| Cuenta sin asociar o usuario inactivo | Error 50305: "La cuenta de Windows … no está asociada" |
| Usuario asociado sin `reports.read` | Error 50300 |

SSRS valida el valor del parámetro contra esa lista **también cuando llega por URL** (`rsInvalidReportParameter`): sin
`reports.viewas` nadie puede ver como otro usuario. Verificado: con la cuenta asociada a la supervisora, `&UserId=1` se rechaza.

**Asociar cuentas:** pantalla **Usuarios** → editar → *Cuenta de Windows (opcional)*, o `PUT /api/v1/users/{id}/windows-account`
(auditado como `USER_WINDOWS_ACCOUNT`; 409 si la cuenta ya pertenece a otro usuario). Además, la cuenta (o un grupo del dominio
que la contenga) necesita el rol de lectura en la carpeta:

```powershell
./deploy-reports.ps1 -Browsers 'EMPRESA\Contabilidad', 'EMPRESA\Auditoria'
```

### Demostración con los usuarios de ejemplo

Los usuarios de ejemplo solo existen en FinCore. `reporting/FinCore.Reports/demo-windows-users.ps1` (PowerShell **como
administrador**, con Docker arriba) crea las cuentas locales `fc.supervisora` y `fc.auditora` (clave `FinCore*2026`), las asocia a
Laura y a Marta, asocia tu propia cuenta a `admin`, les da lectura en /FinCore y comprueba qué informes abre cada una:

| Cuenta de Windows | Usuario de FinCore | Resumen por sucursal | Actividad por usuario | Ver como |
|---|---|---|---|---|
| tu cuenta | admin | sí | sí | cualquiera |
| EQUIPO\fc.supervisora | supervisor | sí | no (sin `audit.read`) | solo ella |
| EQUIPO\fc.auditora | auditor | sí | sí | solo ella |

Para entrar al portal como una de ellas: Edge en InPrivate → http://localhost/Reports → usuario `EQUIPO\fc.auditora`.
`./demo-windows-users.ps1 -Remove` quita las asociaciones y borra las cuentas locales. Son cuentas reales del equipo (pueden iniciar
sesión en Windows): úsalas solo en un equipo de desarrollo.

## Problemas frecuentes

| Síntoma | Causa |
|---|---|
| `The ConnectionString property has not been initialized` | El origen se creó por REST sin `IsConnectionStringOverridden = true` (el script ya lo envía). |
| `Cannot create a connection to data source` / timeout | La base no escucha en `localhost,14333` (¿contenedor `fincore-db` detenido?) o el login no existe. |
| `401` al publicar | La cuenta no tiene rol de Administrador de contenido en SSRS. |
| "no está asociada a un usuario activo de FinCore" (50305) | Falta asociar la cuenta en Usuarios, o el usuario está inactivo. |
| "el parámetro UserId no es un valor válido" | Se pidió ver como otro usuario sin `reports.viewas`. |
| `The DELETE permission was denied on … UserWindowsAccounts` | La base no tiene el último `010_Security` (ejecuta el migrador). |
| `not a CLS-compliant identifier` | Un `DataElementName` con espacios o tildes; el generador ya los normaliza. |
