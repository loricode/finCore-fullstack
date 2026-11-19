# ADR-001 · Monolito modular con Clean Architecture

**Estado**: aceptada · 2026-11-19

## Contexto
FinCore es un MVP que debe poder construir y mantener una persona de nivel semi senior, pero crecer sin rehacerse.
Los módulos (seguridad, operaciones, conciliación, reportes, integración) comparten transacciones: revertir una operación
y actualizar su conciliación deben ser atómicos.

## Decisión
Una sola API ASP.NET Core con cuatro proyectos por capa (Api, Application, Domain, Infrastructure) y carpetas por módulo
dentro de Application. Un esquema de SQL Server por módulo. Los componentes con ciclo de vida propio (Worker, SSIS, SSRS,
Node.js) se despliegan aparte.

## Consecuencias
- Una transacción local en lugar de sagas; depuración y despliegue simples.
- Los límites entre módulos se cuidan con disciplina: un módulo no consulta las tablas de otro desde sus casos de uso, usa sus interfaces.
- Si un módulo necesita escalar o desplegarse por separado (p. ej. la recepción de webhooks), su esquema y sus interfaces
  permiten extraerlo sin reescribir el resto.
