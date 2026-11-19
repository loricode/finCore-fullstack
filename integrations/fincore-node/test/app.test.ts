import assert from 'node:assert/strict';
import type { AddressInfo } from 'node:net';
import { after, before, describe, it } from 'node:test';
import { createApp } from '../src/app.ts';
import type { DeliveryItem } from '../src/delivery.ts';
import { sign } from '../src/signature.ts';

process.env.LOG_SILENT = '1';

const secret = 'secreto-de-prueba';
const enqueued: DeliveryItem[] = [];
const app = createApp({
  config: { providers: { 'banco-simulado': { secret } }, signatureToleranceSeconds: 300 },
  queue: { enqueue: (item) => { enqueued.push(item); }, size: 0 },
});

let base = '';
let server: ReturnType<typeof app.listen>;
before(async () => {
  server = app.listen(0);
  await new Promise((r) => server.once('listening', r));
  base = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
});
after(() => server.close());

const payload = {
  event_id: 'evt_app_1', type: 'movement.created',
  data: { account_number: '123-456789-01', movement_id: 'M1', booking_date: '2026-11-19', amount_cents: 1000, currency: 'COP' },
};

function post(body: string, headers: Record<string, string> = {}, provider = 'banco-simulado') {
  return fetch(`${base}/webhooks/${provider}`, { method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body });
}

const signed = (body: string) => ({ 'X-Bank-Signature': sign(body, secret, Math.floor(Date.now() / 1000)) });

describe('POST /webhooks/:provider', () => {
  it('acepta un webhook firmado (202) y lo deja en la cola con su CorrelationId', async () => {
    const body = JSON.stringify(payload);
    const res = await post(body, { ...signed(body), 'X-Correlation-Id': 'corr-prueba-1' });
    assert.equal(res.status, 202);
    assert.equal((await res.json()).status, 'accepted');
    const item = enqueued.at(-1)!;
    assert.equal(item.event.externalEventId, 'evt_app_1');
    assert.equal(item.event.movement.amount, 10);
    assert.equal(item.correlationId, 'corr-prueba-1');
  });

  it('rechaza una firma inválida (401) sin encolar', async () => {
    const before = enqueued.length;
    const res = await post(JSON.stringify(payload), { 'X-Bank-Signature': sign('otro cuerpo', secret, Math.floor(Date.now() / 1000)) });
    assert.equal(res.status, 401);
    assert.equal(enqueued.length, before);
  });

  it('proveedor desconocido 404, JSON inválido 400, contenido inválido 422, tipo no soportado 202 ignorado', async () => {
    assert.equal((await post('{}', {}, 'otro-banco')).status, 404);
    assert.equal((await post('no-json', signed('no-json'))).status, 400);

    const badCurrency = JSON.stringify({ ...payload, data: { ...payload.data, currency: 'USD' } });
    assert.equal((await post(badCurrency, signed(badCurrency))).status, 422);

    const other = JSON.stringify({ ...payload, type: 'account.updated' });
    const ignored = await post(other, signed(other));
    assert.equal(ignored.status, 202);
    assert.equal((await ignored.json()).status, 'ignored');
  });

  it('GET /health responde', async () => {
    assert.equal((await fetch(`${base}/health`)).status, 200);
  });
});
