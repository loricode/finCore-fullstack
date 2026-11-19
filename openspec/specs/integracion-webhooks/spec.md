# Integración con proveedores (webhooks vía Node.js)

```
 Proveedor bancario ──webhook──▶ fincore-node ──REST──▶ FinCore.Api ──▶ integration.InboundEvents
 (simulado)          firma HMAC   verifica, traduce,    valida, aplica      rec.BankStatementDetails (extracto WEBHOOK)
                                  reintenta             idempotencia
                                                              ▲
                                       FinCore.Worker ────────┘ recupera eventos que quedaron sin procesar
```

**Separación de responsabilidades** (ADR-004): Node.js es un adaptador. Verifica la firma, traduce el formato del proveedor
al contrato de FinCore y reintenta la entrega. NO contiene reglas financieras ni accede a SQL Server.

## Contratos

### Entrada en el adaptador

`POST /webhooks/{provider}` · cabecera `X-Bank-Signature: t=<unix>,v1=<hex HMAC-SHA256 de "<t>.<cuerpo crudo>">`

```json
{ "event_id": "evt_01J...", "type": "movement.created", "created_at": "2026-11-19T15:04:05Z",
  "data": { "account_number": "123-456789-01", "movement_id": "BCO-0101", "reference": "TRF-2001",
            "booking_date": "2026-11-19", "amount_cents": -25000000, "currency": "COP", "narrative": "TRANSFERENCIA" } }
```

### Salida hacia la API

`POST /api/v1/integrations/bank-events` · permiso `integration.webhooks` (cuenta `svc.integration`) · cabecera `X-Correlation-Id`

```json
{ "provider": "banco-simulado", "externalEventId": "evt_01J...", "eventType": "movement.created",
  "occurredAt": "2026-11-19T15:04:05Z",
  "movement": { "accountNumber": "123-456789-01", "movementId": "BCO-0101", "reference": "TRF-2001",
                "date": "2026-11-19", "amount": -250000.00, "description": "TRANSFERENCIA" } }
```

## Requisito: autenticidad
### Escenario: firma inválida o vieja
- **Dado** un webhook con firma incorrecta, sin firma, mal formada o con más de 5 minutos de antigüedad
- **Entonces** el adaptador responde 401 y no llama a la API (la comparación es en tiempo constante)

## Requisito: respuesta rápida al proveedor
### Escenario: evento válido
- **Cuando** llega un evento con firma válida
- **Entonces** el adaptador responde 202 de inmediato y entrega el evento a la API en segundo plano, en orden

| Respuesta del adaptador | Cuándo |
|---|---|
| 202 `accepted` / `ignored` | Aceptado · tipo de evento no soportado |
| 400 · 401 · 404 · 422 | JSON inválido · firma · proveedor desconocido · contenido inválido (moneda distinta de COP, `amount_cents` no entero o cero, fecha mal formada) |

## Requisito: traducción sin reglas
El adaptador convierte `amount_cents` (entero) a pesos con 2 decimales y renombra campos. No decide nada financiero.

## Requisito: idempotencia
La API DEBE registrar cada evento en `integration.InboundEvents` con `(Provider, ExternalEventId)` único y el SHA-256 del cuerpo.

### Escenario: evento repetido
- **Dado** un evento ya procesado
- **Cuando** llega otra vez con el mismo contenido
- **Entonces** la API responde 200 con `duplicate = true` y el resultado original, sin crear otro movimiento

### Escenario: mismo id con otro contenido
- **Entonces** 409 `integration.event_conflict`

### Escenario: movimiento nuevo
- **Cuando** se procesa `movement.created`
- **Entonces** el movimiento se agrega al extracto `WEBHOOK` del día de la cuenta (referencia normalizada en mayúsculas) y el evento
  queda `PROCESSED` con `BankStatementDetailId`, en una transacción

### Escenario: movimiento que ya llegó por otro canal
- **Dado** un movimiento con el mismo `(cuenta, movementId)` cargado por CSV u Oracle
- **Entonces** el evento queda `IGNORED` (no es un error) y no se duplica el movimiento

## Requisito: reintentos controlados
### Escenario: API no disponible
- **Si** la API responde 5xx, 408, 429 o no responde
- **Entonces** el adaptador reintenta 5 veces con espera exponencial y variación aleatoria de ±20 % (1, 2, 4, 8, 16 s)
- **Y** tras el último intento guarda el evento en el archivo de dead letter (`DEAD_LETTER_PATH`) y lo registra en el log

### Escenario: error de negocio
- **Si** la API responde 4xx (p. ej. cuenta inexistente → 422 `integration.business_error`)
- **Entonces** el adaptador NO reintenta; la API deja el evento `FAILED` con el error para revisión

### Escenario: la API cae a mitad del proceso
- **Dado** un evento `RECEIVED` con menos de 5 intentos y más de 2 minutos de antigüedad
- **Entonces** el trabajo `InboundEventRecovery` del Worker lo procesa (o el reenvío del adaptador lo completa)

## Requisito: privacidad
El payload guardado NO incluye firmas ni cabeceras. Los logs del adaptador y los errores guardados por la API enmascaran el número
de cuenta (`***9-01`). Los logs del adaptador nunca incluyen el payload.
