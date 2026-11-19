# Arquitectura

FinCore es un **monolito modular con Clean Architecture**: un solo despliegue de la API, dividido por módulos de negocio
(seguridad, operaciones, conciliación, reportes, integración) y por capas con dependencias hacia adentro.
Los procesos con ciclo de vida propio (Worker, SSIS, SSRS, adaptador Node.js) son componentes aparte.

```
                    ┌──────────────────────────────┐
                    │ Angular (fincore-angular)    │  componentes · servicios · guards
                    └──────────────┬───────────────┘
                                   │ REST /api/v1 + JWT
 Proveedor ─webhook─▶ fincore-node ┤
 bancario   (HMAC)   (adaptador)   │
                    ┌──────────────▼───────────────┐
                    │ FinCore.Api                  │  controladores · middlewares · autenticación · ProblemDetails
                    ├──────────────────────────────┤
                    │ FinCore.Application          │  casos de uso (commands/queries) · DTO · validadores · interfaces
                    ├──────────────────────────────┤
                    │ FinCore.Domain               │  entidades · value objects · máquina de estados · reglas
                    └──────────────▲───────────────┘
                                   │ implementa interfaces de Application
                    ┌──────────────┴───────────────┐
                    │ FinCore.Infrastructure       │  EF Core · procedimientos SQL · Oracle · logging · reloj
                    └───────┬──────────────┬───────┘
                            │              │
                  ┌─────────▼──────┐  ┌────▼─────────┐
                  │ SQL Server     │  │ Oracle       │ ◀── SSIS (etl/) extrae hacia staging de SQL Server
                  │ base principal │  │ legado       │
                  └─────────▲──────┘  └──────────────┘
                            │ rpt.*
                  SSRS (reporting/) · FinCore.Worker (Windows Service)
```

## Reglas de dependencia

| Proyecto | Puede depender de | No puede |
|---|---|---|
| `FinCore.Domain` | Nada (solo BCL) | EF Core, ASP.NET, Infrastructure |
| `FinCore.Application` | Domain | EF Core, ASP.NET, Infrastructure |
| `FinCore.Infrastructure` | Application, Domain | Api |
| `FinCore.Api` | Application, Infrastructure (solo para registrar servicios en `Program.cs`) | Repositorios desde controladores |
| `FinCore.Worker` | Application, Infrastructure | Api |

Se verifican con pruebas de arquitectura (NetArchTest) en `FinCore.Tests`. Los controladores solo traducen HTTP ↔ casos de uso.

## Dónde vive cada regla

| Regla | Lugar | Por qué |
|---|---|---|
| Validación de formato de entrada | Application (FluentValidation) | Errores por campo en la respuesta 400 |
| Máquina de estados, doble control, reversión | Domain (`Transaction`) | Se prueba sin base de datos |
| Autorización por permiso y alcance por sucursal | Application (`ICurrentUser`, filtro por sucursales) + `sec.tvf_AuthorizedBranches` para reportes | Un mismo criterio para API y SSRS |
| Emparejamiento de conciliación | SQL (`rec.usp_RunReconciliation`) | Set-based sobre miles de filas ([ADR-003](adr/ADR-003-conciliacion-set-based-en-sql.md)) |
| Validación de movimientos importados | SQL (`stg.usp_PromoteBankMovements`) | Un solo validador para CSV y SSIS |
| Protecciones irreversibles | Domain + triggers | Defensa en profundidad ante código o SQL manual |
| Traducción del formato del proveedor | Node.js | Adaptador; sin reglas financieras ([ADR-004](adr/ADR-004-node-como-adaptador.md)) |

## Flujos principales

**Registrar una operación**: Angular → `POST /transactions` con `Idempotency-Key` → validador → caso de uso verifica la sucursal autorizada
→ `Transaction.Register(...)` (dominio) → el caso de uso registra la auditoría → EF Core guarda todo en una transacción → el trigger escribe el historial → 201.

**Conciliar**: `POST /reconciliations` → caso de uso valida permiso y alcance de la cuenta → `IReconciliationEngine` (Infrastructure)
ejecuta `rec.usp_RunReconciliation` con `UserId` y `CorrelationId` → devuelve el resumen. El Worker usa el mismo caso de uso cada noche.

**Importar desde Oracle**: Worker (Docker) o SSIS (Windows) → marca de agua `etl.Watermarks` → `etl.usp_StartExecution` → consulta compartida `oracle_extract_movements.sql` → `stg.BankMovements` → `stg.usp_PromoteBankMovements` → mover la marca de agua.
El CSV de la API sigue el mismo camino desde el paso de staging.

**Webhook**: proveedor → Node verifica firma y traduce → `POST /integrations/bank-events` → `integration.InboundEvents` (idempotencia)
→ movimiento en el extracto `WEBHOOK` del día.

## Transversales

- **CorrelationId**: middleware → logs (Serilog), auditoría, `SESSION_CONTEXT` de SQL, Node y Worker.
- **Auditoría**: cada caso de uso registra su entrada (`IAuditLog`) con valores antes y después; se guarda en la misma transacción que el cambio. Se eligió explícita en vez de un interceptor genérico para auditar acciones de negocio (procesar, revertir) y no solo columnas cambiadas.
- **Errores**: manejador global → `ProblemDetails`; mapa de errores SQL 50xxx en `database/README.md`.
- **Hora**: `TimeProvider` inyectado; UTC en todo instante y fecha de negocio de Colombia (`dbo.fn_BusinessDate`).

## Decisiones

| ADR | Decisión |
|---|---|
| [ADR-001](adr/ADR-001-monolito-modular.md) | Monolito modular con Clean Architecture, no microservicios |
| [ADR-002](adr/ADR-002-scripts-sql-fuente-de-verdad.md) | Los scripts SQL versionados son la fuente de verdad del esquema; EF Core sin migraciones |
| [ADR-003](adr/ADR-003-conciliacion-set-based-en-sql.md) | Emparejamiento de conciliación en un procedimiento set-based, orquestado por .NET |
| [ADR-004](adr/ADR-004-node-como-adaptador.md) | Node.js como adaptador de integración sin reglas de negocio |
