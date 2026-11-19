# OpenSpec · FinCore

Las specs describen **comportamiento observable** con escenarios Dado/Cuando/Entonces. Son el contrato entre el negocio,
el código y los asistentes de IA: antes de cambiar un comportamiento se actualiza su spec, en el mismo commit.

```
openspec/
├── specs/<módulo>/spec.md            qué hace el sistema HOY (requisitos y escenarios)
├── changes/<cambio>/proposal.md      por qué y qué cambia
├── changes/<cambio>/tasks.md         tareas con casillas: [x] hecha · [ ] pendiente
├── changes/archive/<cambio>/         cambios con todas las tareas en [x]
└── progress.mjs                      avance por cambio y lista de pendientes
```

| Spec | Qué cubre | Estado |
|---|---|---|
| [seguridad-acceso](specs/seguridad-acceso/spec.md) | Login, tokens, roles, permisos, alcance por sucursal | Implementada (falta administrar sucursales y cuentas, HU-03) |
| [operaciones-financieras](specs/operaciones-financieras/spec.md) | Registro, estados, reversión, idempotencia, protección de cerradas | Implementada |
| [conciliacion-bancaria](specs/conciliacion-bancaria/spec.md) | Motor de conciliación, resultados, concurrencia, resolución | Implementada |
| [importacion-extractos](specs/importacion-extractos/spec.md) | CSV y Oracle → staging → validación → carga; historial ETL | Implementada (falta el `.dtsx` de SSIS) |
| [integracion-webhooks](specs/integracion-webhooks/spec.md) | Adaptador Node.js, firma, reintentos, idempotencia | Implementada |
| [reportes-financieros](specs/reportes-financieros/spec.md) | Totales, conciliación, períodos, actividad, identidad en SSRS; API y SSRS | Implementada |
| [soporte-produccion](specs/soporte-produccion/spec.md) | CorrelationId, logs, auditoría, errores, health checks | Implementada (faltan CI/CD y monitoreo) |

## Tareas

| Cambio | Historias | Estado |
|---|---|---|
| [etapa-1-fundamentos](changes/etapa-1-fundamentos/tasks.md) | HU-01 a HU-05 | Activo: falta HU-03 (administrar sucursales y cuentas) |
| [etapa-2-operaciones](changes/archive/etapa-2-operaciones/tasks.md) | HU-06 a HU-12 | Archivado |
| [etapa-3-conciliacion](changes/archive/etapa-3-conciliacion/tasks.md) | HU-13 a HU-18 | Archivado |
| [etapa-4-integraciones](changes/etapa-4-integraciones/tasks.md) | HU-19 a HU-22 | Activo: falta el `.dtsx` de SSIS |
| [etapa-5-reporteria](changes/archive/etapa-5-reporteria/tasks.md) | HU-23 a HU-26 | Archivado |
| [etapa-6-calidad-produccion](changes/etapa-6-calidad-produccion/tasks.md) | HU-27 a HU-31 | Activo |

```powershell
node openspec/progress.mjs           # avance por cambio y tareas pendientes
node openspec/progress.mjs --check   # falla si un cambio archivado tiene tareas sin marcar
```

**Cómo se trabaja con las tareas**

1. Un trabajo nuevo se agrega como `- [ ] N.M descripción` en el `tasks.md` del cambio activo, o en un cambio nuevo
   (`changes/<nombre>/proposal.md` + `tasks.md`) si es un tema aparte.
2. Al terminarlo se marca `- [x]` **en el mismo commit** que el código, con su prueba. Si cambia el comportamiento, también se actualiza
   la spec de `specs/`. Una tarea solo se marca cuando está verificada (prueba automatizada o verificación documentada), no cuando "casi" está.
3. Si al trabajar aparece algo que falta, se agrega como `[ ]` en vez de dejarlo en un comentario.
4. Cuando todas las tareas de un cambio están en `[x]`, la carpeta se mueve a `changes/archive/` y se actualiza esta tabla.

## Convenciones

- **Stack**: .NET 10 (C# 14), ASP.NET Core, EF Core, SQL Server 2019+ (probado en 2025), Angular, Node.js 22+, Oracle (fuente legada), SSIS, SSRS.
- **Idioma**: documentación, mensajes de error e interfaz en español; identificadores de código y de base de datos en inglés.
- **REST**: prefijo `/api/v1`, JSON en camelCase, errores `application/problem+json` (RFC 9457), paginación `page`/`pageSize` (máx. 100),
  fechas de negocio `yyyy-MM-dd`, instantes en UTC ISO 8601, valores monetarios como número decimal con 2 decimales.
- **Cabeceras**: `Authorization: Bearer`, `X-Correlation-Id` (entrada opcional, salida siempre), `Idempotency-Key` en altas,
  `If-Match` con el `RowVersion` en ediciones.
- **Errores SQL de negocio**: números 50000–50999; el catálogo y su código HTTP están en [`database/README.md`](../database/README.md#catálogo-de-errores).
- **Palabras clave**: DEBE / NO DEBE son obligatorias; PUEDE es opcional.
