# Etapa 1 · Fundamentos

## Por qué
Sin seguridad, base de datos y una solución en capas no hay dónde construir los módulos de negocio. Esta etapa deja la base
desplegable con un comando, la API con autenticación y permisos, y la aplicación Angular conectada, todo en Docker.

## Qué cambia
- Base de datos SQL Server como única fuente de verdad del esquema (scripts versionados y repetibles, bitácora con SHA-256).
- Solución .NET 10 con Clean Architecture: Api → Application → Domain; Infrastructure implementa las interfaces.
- Autenticación JWT con refresh token rotado, políticas por permiso y alcance por sucursal.
- Administración de usuarios y asignación de sucursales.
- Angular 22 con Material 3 y el tema de la sala; login, inicio, usuarios y sucursales.
- Docker Compose: base, migrador, API, web y perfiles de prueba.

## Specs afectadas
[seguridad-acceso](../../specs/seguridad-acceso/spec.md) · [soporte-produccion](../../specs/soporte-produccion/spec.md)

## Historias
HU-01 a HU-05.
