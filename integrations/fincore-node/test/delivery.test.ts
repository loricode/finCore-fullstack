import assert from 'node:assert/strict';
import { mkdtemp, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { DeliveryQueue, type DeliveryOutcome } from '../src/delivery.ts';
import type { ApiResponse, BankEventSender } from '../src/fincore-client.ts';
import type { FinCoreBankEvent } from '../src/translate.ts';

process.env.LOG_SILENT = '1';

const event: FinCoreBankEvent = {
  provider: 'banco-simulado', externalEventId: 'evt_1', eventType: 'movement.created', occurredAt: null,
  movement: { accountNumber: '123-456789-01', movementId: 'M1', reference: null, date: '2026-11-19', amount: 100, description: null },
};

/** Responde una secuencia de estados; un 0 simula un error de red. */
function senderReturning(...statuses: number[]): BankEventSender & { calls: number } {
  const sender = {
    calls: 0,
    async send(): Promise<ApiResponse> {
      const status = statuses[Math.min(sender.calls++, statuses.length - 1)];
      if (status === 0) throw new Error('ECONNREFUSED');
      return { status, body: { status: 'PROCESSED' } };
    },
  };
  return sender;
}

async function run(sender: BankEventSender, deadLetterPath = join(tmpdir(), 'nunca.jsonl')) {
  const outcomes: DeliveryOutcome[] = [];
  const waits: number[] = [];
  const queue = new DeliveryQueue({
    sender, retryDelaysMs: [1000, 2000, 4000, 8000, 16000], deadLetterPath,
    sleep: async (ms) => { waits.push(ms); },
    onOutcome: (_item, outcome) => outcomes.push(outcome),
  });
  queue.enqueue({ event, correlationId: 'c1', receivedAt: new Date().toISOString() });
  await queue.idle();
  return { outcomes, waits };
}

describe('entrega a FinCore con reintentos', () => {
  it('reintenta errores 5xx y de red hasta entregar, con espera exponencial', async () => {
    const sender = senderReturning(503, 0, 200);
    const { outcomes, waits } = await run(sender);
    assert.deepEqual(outcomes, ['delivered']);
    assert.equal(sender.calls, 3);
    assert.equal(waits.length, 2);
    assert.ok(waits[0] >= 800 && waits[0] <= 1200, `primer reintento ~1 s (fue ${waits[0]})`);
    assert.ok(waits[1] >= 1600 && waits[1] <= 2400, `segundo reintento ~2 s (fue ${waits[1]})`);
  });

  it('no reintenta un error de negocio (422)', async () => {
    const sender = senderReturning(422);
    const { outcomes } = await run(sender);
    assert.deepEqual(outcomes, ['rejected']);
    assert.equal(sender.calls, 1);
  });

  it('sí reintenta 429 (demasiadas solicitudes)', async () => {
    const sender = senderReturning(429, 200);
    assert.deepEqual((await run(sender)).outcomes, ['delivered']);
  });

  it('agotados 5 reintentos guarda el evento en dead letter', async () => {
    const dir = await mkdtemp(join(tmpdir(), 'fincore-node-'));
    const path = join(dir, 'dead-letter.jsonl');
    const sender = senderReturning(500);
    const { outcomes } = await run(sender, path);
    assert.deepEqual(outcomes, ['dead-lettered']);
    assert.equal(sender.calls, 6); // 1 intento + 5 reintentos
    const saved = JSON.parse((await readFile(path, 'utf8')).trim());
    assert.equal(saved.event.externalEventId, 'evt_1');
    assert.ok(saved.deadLetteredAt);
  });
});
