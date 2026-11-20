# fincore-node · adaptador de webhooks bancarios

Recibe los webhooks del proveedor bancario, verifica la firma, traduce el formato del proveedor al contrato de FinCore
y lo entrega a la API con reintentos. **No contiene reglas financieras ni accede a la base de datos** (ADR-004).

```
Proveedor ──POST /webhooks/banco-simulado──▶ fincore-node ──POST /api/v1/integrations/bank-events──▶ FinCore.Api
            X-Bank-Signature: t=..,v1=..     │ 202 en milisegundos                                  (idempotente)
                                             └─ cola en memoria · reintentos 1-2-4-8-16 s · dead letter
```

Node 24 ejecuta TypeScript directamente (sin compilación); `tsc` solo verifica tipos. Única dependencia: Express 5.

| Archivo | Responsabilidad |
|---|---|
| `src/signature.ts` | HMAC-SHA256 de `"<timestamp>.<cuerpo>"`, comparación en tiempo constante, ventana de 5 minutos (anti replay) |
| `src/translate.ts` | Formato del proveedor → contrato de FinCore (centavos → pesos, campos, moneda) |
| `src/delivery.ts` | Cola ordenada con reintentos exponenciales (±20 %), sin reintentar errores 4xx de negocio, dead letter |
| `src/fincore-client.ts` | Login con la cuenta `svc.integration`, token en memoria, nuevo login ante 401 |
| `src/app.ts` | Endpoints HTTP (`POST /webhooks/:provider`, `GET /health`) |
| `scripts/simulate-bank.ts` | Simulador del proveedor: envía webhooks firmados (nuevos, duplicado, cuenta inexistente, firma falsa) |

## Respuestas al proveedor

| Código | Cuándo |
|---|---|
| 202 `accepted` | Firma válida y contenido traducible: se entrega en segundo plano |
| 202 `ignored` | Tipo de evento no soportado |
| 400 | Cuerpo que no es JSON |
| 401 | Firma ausente, inválida o con más de 5 minutos |
| 404 | Proveedor desconocido |
| 422 | Contenido inválido (moneda, valor, fecha): el proveedor no debe reenviarlo igual |

## Entrega a FinCore

| Respuesta de la API | Acción |
|---|---|
| 2xx | Entregado (incluye eventos duplicados: la API devuelve el resultado original) |
| 4xx salvo 408/429 | Error de negocio (p. ej. cuenta inexistente → 422): se registra, **no** se reintenta |
| 5xx, 408, 429, red | Reintento a 1, 2, 4, 8 y 16 s; agotados → `DEAD_LETTER_PATH` (JSON por línea) para reprocesar a mano |

## Variables de entorno

| Variable | Defecto (solo desarrollo) |
|---|---|
| `PORT` | `5090` |
| `FINCORE_API_URL` | `http://localhost:5080` |
| `FINCORE_SERVICE_USER` / `FINCORE_SERVICE_PASSWORD` | `svc.integration` / `FinCore*2026` |
| `BANK_WEBHOOK_SECRET` | `dev-secreto-webhook` (obligatoria con `NODE_ENV=production`) |
| `DEAD_LETTER_PATH` | `./data/dead-letter.jsonl` |
| `RETRY_BASE_MS` | `1000` |

## Comandos

```powershell
npm test                 # 18 pruebas (node:test)
npm run typecheck        # tsc --noEmit
npm start                # adaptador en :5090
npm run simulate         # envía los webhooks de prueba
docker compose --profile demo run --rm bank-simulator   # lo mismo, en Docker
```
