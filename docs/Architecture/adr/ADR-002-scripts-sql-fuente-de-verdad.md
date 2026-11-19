# ADR-002 · Los scripts SQL versionados son la fuente de verdad del esquema

**Estado**: aceptada · 2026-11-19

## Contexto
El proyecto necesita procedimientos, funciones, triggers, índices filtrados y columnas calculadas que EF Core no modela bien,
y trazabilidad de cada objeto de base de datos. Mantener migraciones de EF Core y scripts SQL a la vez crea dos fuentes de verdad
que terminan divergiendo.

## Decisión
- Todo el esquema vive en `database/`, desplegado por `deploy.ps1` con bitácora y checksum en `dbo.SchemaVersions`.
- EF Core **mapea** las tablas existentes (configuraciones `IEntityTypeConfiguration` con nombres de tabla, esquema, longitudes,
  `RowVersion`, columna calculada `SignedAmount`) pero **no genera migraciones** ni llama `EnsureCreated`.
- Tablas, índices y datos de demostración son scripts versionados (inmutables una vez aplicados); objetos programables y catálogos son repetibles.

## Consecuencias
- Un cambio de esquema es un script nuevo revisado en el Pull Request, igual que el código.
- La base se reconstruye desde cero con un comando, y las pruebas T-SQL validan el resultado.
- Las pruebas de integración de .NET despliegan la base con el mismo `deploy.ps1` (en Docker en CI).
- Si una entidad de EF Core y su tabla no coinciden, falla la prueba de integración que valida el modelo contra la base.
