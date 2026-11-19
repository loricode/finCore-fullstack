import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { translate } from '../src/translate.ts';

const valid = {
  event_id: 'evt_1',
  type: 'movement.created',
  created_at: '2026-11-19T15:04:05Z',
  data: {
    account_number: '123-456789-01', movement_id: 'BCO-0101', reference: ' trf-2001 ',
    booking_date: '2026-11-19', amount_cents: -25_000_050, currency: 'COP', narrative: 'TRANSFERENCIA',
  },
};

describe('traducción al contrato de FinCore', () => {
  it('convierte centavos a pesos con 2 decimales y mapea los campos', () => {
    const result = translate('banco-simulado', valid);
    assert.equal(result.kind, 'event');
    if (result.kind !== 'event') return;
    assert.deepEqual(result.event, {
      provider: 'banco-simulado',
      externalEventId: 'evt_1',
      eventType: 'movement.created',
      occurredAt: '2026-11-19T15:04:05Z',
      movement: {
        accountNumber: '123-456789-01', movementId: 'BCO-0101', reference: 'trf-2001',
        date: '2026-11-19', amount: -250000.5, description: 'TRANSFERENCIA',
      },
    });
  });

  it('ignora tipos de evento que no son movimientos', () => {
    assert.equal(translate('banco-simulado', { ...valid, type: 'account.updated' }).kind, 'ignored');
  });

  it('rechaza otra moneda, valores no enteros o en cero y fechas mal formadas', () => {
    assert.equal(translate('banco-simulado', { ...valid, data: { ...valid.data, currency: 'USD' } }).kind, 'invalid');
    assert.equal(translate('banco-simulado', { ...valid, data: { ...valid.data, amount_cents: 10.5 } }).kind, 'invalid');
    assert.equal(translate('banco-simulado', { ...valid, data: { ...valid.data, amount_cents: 0 } }).kind, 'invalid');
    assert.equal(translate('banco-simulado', { ...valid, data: { ...valid.data, booking_date: '19/11/2026' } }).kind, 'invalid');
    assert.equal(translate('banco-simulado', { ...valid, event_id: '' }).kind, 'invalid');
  });
});
