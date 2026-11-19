# Soporte a producción

El objetivo es que soporte pueda responder, para cualquier operación: quién la registró, cuándo, con qué referencia,
qué proceso intentó conciliarla, qué error ocurrió, si hubo reintentos y cómo se resolvió.

## Requisito: CorrelationId
Toda petición DEBE tener un CorrelationId: el de la cabecera `X-Correlation-Id` si es válido (≤ 64 caracteres, `[A-Za-z0-9-_]`),
o uno nuevo. La API DEBE devolverlo en la respuesta, incluirlo en cada log, en la auditoría y fijarlo en la conexión SQL con
`sp_set_session_context 'CorrelationId'` (junto con `'UserId'`), para que el historial de estados lo registre.

### Escenario: reconstruir una petición
- **Dado** el CorrelationId `c-123` de un reporte de error
- **Entonces** con él se encuentran los logs de la API, las filas de `audit.AuditLogs`, `fin.TransactionStatusHistory`
  y `rec.Reconciliations` de esa petición

### Escenario: propagación a integraciones
- **Entonces** Node genera un CorrelationId por webhook y lo envía a la API; el Worker genera uno por trabajo y por cuenta

## Requisito: logging estructurado
Los logs DEBEN ser JSON (Serilog) con `timestamp`, `level`, `message`, `correlationId`, `userId`, `requestPath`, `elapsedMs`.
NO DEBEN incluir contraseñas, tokens, cabeceras `Authorization`, números de cuenta completos ni cuerpos de petición con datos financieros.

## Requisito: manejo global de errores
Todo error DEBE responder `application/problem+json` con `type`, `title`, `status`, `detail`, `traceId` y `correlationId`.
Los errores no controlados devuelven 500 con un mensaje genérico; el detalle solo va al log.

| Origen | HTTP |
|---|---|
| Validación (FluentValidation) | 400 con `errors` por campo |
| Regla de dominio violada | 409 |
| Recurso inexistente o fuera del alcance | 404 |
| Sin permiso | 403 |
| `RowVersion` distinto | 412 |
| Error SQL 50000–50999 | Según el catálogo de `database/README.md` |
| SQL 2601/2627 (clave duplicada) | 409 |

## Requisito: auditoría
Altas, cambios, cambios de estado, logins, logins fallidos, exportaciones y cambios de permisos DEBEN auditarse con
usuario, fecha UTC, IP, entidad, id, valores anteriores y nuevos (JSON) y CorrelationId. La tabla es de solo inserción (50090).

## Requisito: health checks
- `GET /health/live`: 200 si el proceso responde (no consulta dependencias).
- `GET /health/ready`: 200 si SQL Server responde en menos de 2 s; 503 en otro caso, con el detalle por dependencia.

## Requisito: reintentos e idempotencia
Solo se reintentan fallas transitorias de integraciones (red, 5xx, timeout, deadlock 1205) con espera exponencial y máximo 5 intentos.
Nunca se reintenta un error de negocio. Toda entrada que pueda repetirse (altas de la API, webhooks, cargas ETL, conciliaciones)
DEBE ser idempotente.
