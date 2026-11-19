# ADR-004 · Node.js como adaptador de integración sin reglas de negocio

**Estado**: aceptada · 2026-11-19

## Contexto
Los proveedores bancarios envían webhooks con formatos y firmas propios, esperan respuesta en menos de un segundo y reintentan
si no la reciben. Esa lógica cambia por proveedor y no debe contaminar la API.

## Decisión
`integrations/fincore-node` recibe el webhook, verifica la firma HMAC y su antigüedad, responde 202, traduce el payload al contrato
`bank-events` de FinCore y lo entrega a la API con reintentos exponenciales. No accede a SQL Server ni decide nada financiero:
la API valida, aplica idempotencia y registra.

## Consecuencias
- Agregar un proveedor es agregar un traductor en Node, sin desplegar la API.
- Las reglas financieras siguen existiendo una sola vez (.NET y SQL).
- Node necesita una cuenta de servicio con un único permiso (`integration.webhooks`).
