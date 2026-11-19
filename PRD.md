# PRD · FinCore

Plataforma de operaciones y conciliación financiera.

## Problema

Una entidad financiera con varias sucursales registra a diario ingresos, egresos, transferencias y pagos.
Hoy no tiene una vista única de esas operaciones y la conciliación contra el banco se hace a mano en hojas de cálculo:

- Hay operaciones pendientes de confirmar que nadie hace seguir.
- Algunas operaciones están en el sistema interno pero no en el banco, y al revés (comisiones, cargos duplicados).
- Los movimientos del banco llegan de fuentes distintas: un sistema legado en Oracle, archivos CSV y notificaciones del proveedor.
- Los administradores no tienen reportes confiables por sucursal ni por período.
- Cuando algo sale mal, soporte no puede reconstruir quién hizo qué, cuándo y en qué petición.

## Objetivo del MVP

Centralizar las operaciones de todas las sucursales, conciliarlas automáticamente contra los movimientos bancarios
y dejar trazabilidad completa de cada operación, cada conciliación y cada carga de datos.

| Métrica | Meta del MVP | Cómo se mide |
|---|---|---|
| Operaciones conciliadas automáticamente (escenario de demo) | 100 % de las que coinciden en referencia, valor y fecha | `database/tests/test_01_reconciliation_matching.sql` |
| Diferencias sin clasificar | 0: cada operación y cada movimiento del período recibe un resultado | Ídem |
| Movimientos bancarios perdidos o duplicados en una carga | 0: leídos = cargados + rechazados, y una recarga no duplica | `CK_ETLExecutions_Balance`, `test_03_promote_bank_movements.sql` |
| Operaciones duplicadas por reintento del cliente | 0 | `Idempotency-Key` + `UX_Transactions_Company_IdempotencyKey` |
| Cambios de estado sin rastro | 0: historial por trigger con usuario y CorrelationId | `test_04_transaction_guards.sql` |
| Conciliación de una cuenta con 10.000 movimientos | < 5 s en un equipo de desarrollo | Prueba de carga (Etapa 6) |

## Usuarios y roles

| Rol | Qué necesita | Alcance |
|---|---|---|
| **Operador de sucursal** | Registrar operaciones y consultarlas | Solo sus sucursales asignadas |
| **Supervisor de operaciones** | Procesar, rechazar y revertir; importar extractos; conciliar; resolver diferencias; reportes | Todas las sucursales |
| **Administrador** | Gestionar usuarios, roles, sucursales y cuentas | Todas las sucursales |
| **Auditor** | Consultar operaciones, conciliaciones, reportes, auditoría y actividad de usuarios | Todas, solo lectura |
| **Cuenta de integración** | Enviar a la API los eventos del proveedor bancario (Node.js) y ejecutar procesos (Worker) | Técnica |

La matriz exacta de permisos está en [`database/008_Seed/002_sec_RolesPermissions.sql`](database/008_Seed/002_sec_RolesPermissions.sql).

## Alcance

### MVP 1.0

1. Inicio de sesión con JWT y refresh token; bloqueo tras intentos fallidos.
2. Administración de usuarios, roles, sucursales, cuentas y asignación de usuarios a sucursales.
3. Registro, consulta, procesamiento, rechazo y reversión de operaciones.
4. Importación de extractos bancarios desde CSV (staging + validación + historial de carga).
5. Conciliación automática y bandeja de diferencias con resolución manual.
6. Reportes básicos exportables (CSV/Excel) desde la API.
7. Auditoría de acciones, historial de estados, CorrelationId y health checks.
8. Documentación OpenAPI y scripts SQL versionados.

### Versión 2

- Extracción desde Oracle con SSIS (mismo staging y mismo procedimiento de promoción que el CSV).
- Reportes empresariales en SSRS.
- Adaptador Node.js para webhooks del proveedor bancario.
- Windows Service (FinCore.Worker) para conciliaciones programadas, reintentos y depuración de staging.
- Monitoreo, Docker para los componentes compatibles y pipeline CI/CD.

### Fuera de alcance

Multimoneda, contabilidad (plan de cuentas, asientos), cálculo de impuestos (GMF, retenciones), conexión con bancos reales,
firma de operaciones con doble factor y Row-Level Security en SQL Server (se evaluará si aparece un consumidor directo de la base).

## Reglas de negocio

### Operaciones

| Id | Regla |
|---|---|
| RN-01 | El valor es positivo, con máximo 2 decimales y hasta $5.000.000.000 COP. Moneda única: COP. |
| RN-02 | La fecha de operación no puede ser futura ni anterior a 30 días (hora de Colombia, UTC-5). |
| RN-03 | La referencia es única por empresa, de 4 a 40 caracteres, se guarda en mayúsculas y sin espacios en los extremos. |
| RN-04 | Transferencia y Pago exigen contraparte (nombre; la transferencia también la cuenta destino). |
| RN-05 | Estados: `Pendiente → Procesada → Conciliada`; `Pendiente → Rechazada`; `Procesada / Conciliada → Revertida`. Rechazada y Revertida son finales. |
| RN-06 | Solo una operación Pendiente puede editarse, y solo por quien la creó o por un supervisor. |
| RN-07 | Rechazar exige motivo. Revertir exige motivo y crea una operación de tipo Reversión con el mismo valor y sentido contrario; la original queda Revertida. Una operación se revierte una sola vez y una reversión no se revierte. |
| RN-08 | Doble control: una operación de $10.000.000 o más no puede procesarla quien la registró. |
| RN-09 | Un usuario solo registra y consulta operaciones de sus sucursales autorizadas (o de todas, con `branches.all`). |
| RN-10 | Las operaciones no se eliminan. Pasar a Conciliada solo lo hace el motor de conciliación. |
| RN-11 | Reenviar un registro con la misma `Idempotency-Key` devuelve la operación original; con la misma clave y otro contenido, 422. |

### Conciliación

| Id | Regla |
|---|---|
| RC-01 | Se concilia por cuenta y período (máximo 93 días), con tolerancia de fecha de 0 a 5 días (por defecto 1). |
| RC-02 | Participan las operaciones Procesadas (y las Revertidas aún no conciliadas) y los movimientos bancarios sin conciliar. |
| RC-03 | Emparejamiento por referencia; luego se comparan valor con signo y fecha. Resultados: Conciliada, Diferencia de valor, Diferencia de fecha, No está en el banco, No está en el sistema, Referencia duplicada, Pendiente de revisión. |
| RC-04 | Una referencia que aparece en más de un movimiento bancario no se concilia: se reporta como duplicada. |
| RC-05 | Repetir la conciliación no altera lo ya conciliado y vuelve a evaluar todo lo demás. |
| RC-06 | Una sola conciliación en curso por cuenta. Si falla, queda registrada con el error. |
| RC-07 | Resolver una diferencia exige nota y deja usuario y fecha; no modifica el resultado original. |

### Importación de extractos

| Id | Regla |
|---|---|
| RI-01 | Todo registro leído termina cargado o rechazado con un código de error; nunca se pierde. |
| RI-02 | Un movimiento ya cargado (misma cuenta e identificador del banco) se rechaza como `ALREADY_LOADED`. |
| RI-03 | Formatos: fecha `yyyy-MM-dd`, valor con punto decimal y signo, máximo 2 decimales. |

## Requisitos no funcionales

- **Seguridad**: JWT de corta duración y refresh token rotado (se guarda su hash); contraseñas con PBKDF2;
  autorización por permisos; el alcance por sucursal se aplica en el servidor, nunca solo en Angular.
- **Trazabilidad**: auditoría de solo inserción, historial de estados por trigger, CorrelationId en logs, auditoría,
  historial y conciliaciones.
- **Privacidad**: los logs no registran contraseñas, tokens, números de cuenta completos ni payloads bancarios.
- **Consistencia**: escrituras financieras en una transacción; concurrencia optimista con `RowVersion`.
- **Resiliencia**: reintentos con espera exponencial solo en integraciones (Node → API, Worker); idempotencia en cada entrada.
- **Mantenibilidad**: Clean Architecture verificada con pruebas de arquitectura; esquema reconstruible desde cero con un comando.
- **Operación**: health checks de API y base de datos; logs estructurados en JSON.

## Tecnologías y responsabilidad

| Tecnología | Responsabilidad | Etapa |
|---|---|---|
| SQL Server | Base principal: modelo, restricciones, conciliación set-based, reportes | 1 ✅ |
| Docker | Todo el sistema y sus pruebas en contenedores (compose) | 1 ✅ |
| C# / .NET 10 · ASP.NET Core | API REST, casos de uso, reglas de dominio | 1 ✅ (autenticación) · 2–3 |
| Angular | Aplicación web | 1 ✅ (sesión y menú) · 2–3 |
| Node.js | Adaptador de webhooks del proveedor bancario (sin reglas financieras) | 4 |
| Oracle | Sistema legado simulado con movimientos bancarios | 4 |
| SSIS | Extracción de Oracle hacia staging de SQL Server | 4 |
| Windows Service | Conciliaciones programadas, reintentos y depuración | 4 |
| SSRS | Reportes empresariales sobre los procedimientos `rpt.*` | 5 |

## Riesgos y decisiones

| Riesgo | Mitigación |
|---|---|
| Duplicar reglas entre .NET y SQL | Las transiciones son del dominio; SQL solo protege lo irreversible y hace el emparejamiento set-based ([ADR-003](docs/Architecture/adr/ADR-003-conciliacion-set-based-en-sql.md)). |
| Dos fuentes de verdad del esquema (EF Core y scripts) | Los scripts SQL son la única fuente; EF Core mapea sin migraciones ([ADR-002](docs/Architecture/adr/ADR-002-scripts-sql-fuente-de-verdad.md)). |
| Alcance excesivo para una persona | MVP 1.0 sin Oracle/SSIS/SSRS/Node; cada etapa entrega algo usable. |
