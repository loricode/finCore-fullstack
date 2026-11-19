# Etapa 4 · Integraciones

## Por qué
Los movimientos bancarios no llegan solo por CSV: el proveedor envía webhooks y el banco legado vive en Oracle. Además, la
conciliación y la depuración deben correr solas, sin que alguien las lance.

## Qué cambia
- Adaptador Node.js de webhooks: firma HMAC con anti-replay, traducción sin reglas de negocio, cola con reintentos y dead letter.
- Oracle en Docker como sistema legado; extracción incremental por marca de agua.
- `FinCore.Worker` como Windows Service con trabajos programados (importación Oracle, recuperación de eventos, conciliación nocturna,
  depuración de staging).
- Diseño del paquete SSIS con el SQL de cada tarea, compartiendo la consulta de extracción con el Worker.

## Specs afectadas
[integracion-webhooks](../../specs/integracion-webhooks/spec.md) · [importacion-extractos](../../specs/importacion-extractos/spec.md) ·
[soporte-produccion](../../specs/soporte-produccion/spec.md)

## Historias
HU-19 a HU-22.
