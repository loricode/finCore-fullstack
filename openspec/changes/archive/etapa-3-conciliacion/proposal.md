# Etapa 3 · Conciliación bancaria

## Por qué
El motor de conciliación existía en SQL, pero nadie podía usarlo: faltaba importar extractos, ejecutar la conciliación desde la
aplicación y revisar las diferencias.

## Qué cambia
- Importación de extractos CSV a staging con validación por línea y rechazos explicados.
- Conciliación desde la API sobre `rec.usp_RunReconciliation` (set-based, una por cuenta a la vez).
- Bandeja de diferencias y resolución con nota, auditada.
- Página de conciliación con pestañas: bandeja, conciliaciones y extractos.

## Specs afectadas
[conciliacion-bancaria](../../../specs/conciliacion-bancaria/spec.md) · [importacion-extractos](../../../specs/importacion-extractos/spec.md)

## Historias
HU-13 a HU-18.
