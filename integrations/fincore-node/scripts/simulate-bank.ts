/**
 * Simulador del proveedor bancario: envía webhooks firmados al adaptador para ver el flujo completo.
 *   npm run simulate                      (adaptador en http://localhost:5090)
 *   docker compose --profile demo run --rm bank-simulator
 *
 * Envía: 3 movimientos nuevos de Montería, un reenvío del primero (idempotencia), uno de una cuenta inexistente
 * (FinCore lo rechaza: 422, sin reintentos) y uno con firma falsa (el adaptador responde 401).
 */
import { randomUUID } from 'node:crypto';
import { sign } from '../src/signature.ts';

const adapterUrl = process.env.ADAPTER_URL ?? 'http://localhost:5090';
const secret = process.env.BANK_WEBHOOK_SECRET ?? 'dev-secreto-webhook';
const today = new Date(Date.now() - 5 * 3600_000).toISOString().slice(0, 10); // fecha de Colombia
const run = randomUUID().slice(0, 6).toUpperCase();

function movement(n: number, account = '123-456789-01', amountCents = 15_000_000 + n * 1_000_000) {
  return {
    event_id: `evt_${run}_${n}`,
    type: 'movement.created',
    created_at: new Date().toISOString(),
    data: {
      account_number: account, movement_id: `WH-${run}-${n}`, reference: `WH-${run}-${n}`,
      booking_date: today, amount_cents: amountCents, currency: 'COP', narrative: `ABONO EN LINEA ${n}`,
    },
  };
}

async function send(label: string, payload: unknown, opts: { badSignature?: boolean } = {}) {
  const body = JSON.stringify(payload);
  const signature = opts.badSignature ? sign(body, 'secreto-equivocado', Math.floor(Date.now() / 1000))
    : sign(body, secret, Math.floor(Date.now() / 1000));
  const res = await fetch(`${adapterUrl}/webhooks/banco-simulado`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Bank-Signature': signature }, body,
  });
  console.log(`${label.padEnd(38)} → ${res.status} ${await res.text()}`);
}

const first = movement(1);
await send('Movimiento 1 (nuevo)', first);
await send('Movimiento 2 (nuevo)', movement(2));
await send('Movimiento 3 (egreso)', movement(3, '123-456789-01', -4_250_000));
await send('Movimiento 1 reenviado (duplicado)', first);
await send('Cuenta inexistente', movement(4, '000-000000-00'));
await send('Firma falsa', movement(5), { badSignature: true });
console.log('\nRevisa: docker compose logs bank-adapter · integration.InboundEvents · extracto WEBHOOK de MON-001 del día.');
