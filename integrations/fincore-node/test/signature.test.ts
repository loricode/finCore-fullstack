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

  it('rechaza cabeceras ausentes o mal formadas', () => {
    assert.deepEqual(verify(undefined, body, secret, now), { ok: false, reason: 'missing_signature' });
    assert.deepEqual(verify('t=abc,v1=xyz', body, secret, now), { ok: false, reason: 'malformed_signature' });
  });
});
