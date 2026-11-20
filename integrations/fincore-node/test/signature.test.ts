import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { sign, verify } from '../src/signature.ts';

const secret = 'secreto-de-prueba';
const now = 1_800_000_000;
const body = Buffer.from('{"event_id":"evt_1"}');

describe('firma de webhooks', () => {
  it('acepta una firma correcta y reciente', () => {
    assert.deepEqual(verify(sign(body, secret, now), body, secret, now + 10), { ok: true });
  });

  it('rechaza un cuerpo alterado', () => {
    const header = sign(body, secret, now);
    assert.deepEqual(verify(header, Buffer.from('{"event_id":"evt_2"}'), secret, now), { ok: false, reason: 'invalid_signature' });
  });

  it('rechaza otro secreto', () => {
    assert.equal(verify(sign(body, 'otro', now), body, secret, now).ok, false);
  });

  it('rechaza un webhook viejo (replay) aunque la firma sea válida', () => {
    assert.deepEqual(verify(sign(body, secret, now), body, secret, now + 301), { ok: false, reason: 'stale_signature' });
  });

  it('verifica sobre los bytes crudos: re-serializar el JSON rompe la firma', () => {
    // Espacios y un importe con cero final: exactamente lo que el proveedor firmó.
    const raw = Buffer.from('{ "event_id" : "evt_1",  "amount" : 10.50 }');
    const header = sign(raw, secret, now);

    // Parsear y volver a serializar cambia los bytes (sin espacios, 10.50 -> 10.5)...
    const reserialized = Buffer.from(JSON.stringify(JSON.parse(raw.toString('utf8'))));
    assert.equal(reserialized.toString('utf8'), '{"event_id":"evt_1","amount":10.5}');

    // ...por eso solo el cuerpo original verifica.
    assert.deepEqual(verify(header, raw, secret, now), { ok: true });
    assert.deepEqual(verify(header, reserialized, secret, now), { ok: false, reason: 'invalid_signature' });
  });

  it('el mismo cuerpo enviado dos veces: la firma no lo impide dentro de la ventana; el timestamp sí está firmado', () => {
    const header = sign(body, secret, now);

    // Dentro de los 5 minutos la firma es válida las dos veces: la protección contra el reenvío es la
    // idempotencia por (Provider, ExternalEventId) en la API, no la firma.
    assert.deepEqual(verify(header, body, secret, now + 1), { ok: true });
    assert.deepEqual(verify(header, body, secret, now + 2), { ok: true });

    // El timestamp forma parte de lo firmado: reutilizar la firma con un t nuevo para alargar la ventana falla.
    const forged = header.replace(`t=${now}`, `t=${now + 600}`);
    assert.deepEqual(verify(forged, body, secret, now + 600), { ok: false, reason: 'invalid_signature' });
  });

  it('rechaza cabeceras ausentes o mal formadas', () => {
    assert.deepEqual(verify(undefined, body, secret, now), { ok: false, reason: 'missing_signature' });
    assert.deepEqual(verify('t=abc,v1=xyz', body, secret, now), { ok: false, reason: 'malformed_signature' });
  });
});
