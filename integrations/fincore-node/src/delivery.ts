import { appendFile, mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';
import type { BankEventSender } from './fincore-client.ts';
import { log, maskAccount } from './logger.ts';
import type { FinCoreBankEvent } from './translate.ts';

export interface DeliveryItem {
  event: FinCoreBankEvent;
  correlationId: string;
  receivedAt: string;
}

export type DeliveryOutcome = 'delivered' | 'rejected' | 'dead-lettered';

interface DeliveryOptions {
  sender: BankEventSender;
  retryDelaysMs: number[];
  deadLetterPath: string;
  /** Para pruebas: espera sin temporizadores reales. */
  sleep?: (ms: number) => Promise<void>;
  onOutcome?: (item: DeliveryItem, outcome: DeliveryOutcome) => void;
}

/**
 * Cola de entrega a la API (spec integracion-webhooks · reintentos controlados).
 * - El proveedor ya recibió 202: aquí se entrega en segundo plano, en orden (uno a la vez).
 * - 2xx: entregado. 4xx (salvo 408 y 429): error de negocio o de contrato → NO se reintenta.
 * - 5xx, 408, 429 o error de red: se reintenta con espera exponencial y variación aleatoria (±20 %).
 * - Agotados los reintentos: se guarda en el archivo de "dead letter" para reprocesarlo a mano.
 */
export class DeliveryQueue {
  private readonly options: DeliveryOptions;
  private readonly items: DeliveryItem[] = [];
  private running: Promise<void> | null = null;

  constructor(options: DeliveryOptions) {
    this.options = options;
  }

  get size(): number {
    return this.items.length;
  }

  enqueue(item: DeliveryItem): void {
    this.items.push(item);
    this.running ??= this.drain().finally(() => (this.running = null));
  }

  /** Resuelve cuando la cola queda vacía (pruebas y apagado ordenado). */
  async idle(): Promise<void> {
    while (this.running) await this.running;
  }

  private async drain(): Promise<void> {
    for (let item = this.items.shift(); item; item = this.items.shift()) {
      const outcome = await this.deliver(item);
      this.options.onOutcome?.(item, outcome);
    }
  }

  private async deliver(item: DeliveryItem): Promise<DeliveryOutcome> {
    const { event, correlationId } = item;
    const context = { correlationId, externalEventId: event.externalEventId, account: maskAccount(event.movement.accountNumber) };
    const sleep = this.options.sleep ?? ((ms: number) => new Promise((r) => setTimeout(r, ms)));
    const delays = this.options.retryDelaysMs;

    for (let attempt = 0; attempt <= delays.length; attempt++) {
      let status = 0;
      let body: unknown = null;
      try {
        ({ status, body } = await this.options.sender.send(event, correlationId));
      } catch (error) {
        log('warn', 'Fallo de red al entregar el evento', { ...context, attempt: attempt + 1, error: String(error) });
      }

      if (status >= 200 && status < 300) {
        log('info', 'Evento entregado a FinCore', { ...context, attempt: attempt + 1, result: (body as { status?: string })?.status });
        return 'delivered';
      }
      if (status >= 400 && status < 500 && status !== 408 && status !== 429) {
        log('warn', 'FinCore rechazó el evento; no se reintenta', { ...context, status, detail: (body as { detail?: string })?.detail });
        return 'rejected';
      }
      if (attempt < delays.length) {
        const wait = Math.round(delays[attempt] * (0.8 + Math.random() * 0.4));
        log('warn', 'Entrega fallida; se reintentará', { ...context, status, attempt: attempt + 1, retryInMs: wait });
        await sleep(wait);
      }
    }

    await this.deadLetter(item);
    log('error', 'Evento enviado a dead letter tras agotar los reintentos', context);
    return 'dead-lettered';
  }

  private async deadLetter(item: DeliveryItem): Promise<void> {
    await mkdir(dirname(this.options.deadLetterPath), { recursive: true });
    await appendFile(this.options.deadLetterPath, JSON.stringify({ ...item, deadLetteredAt: new Date().toISOString() }) + '\n');
  }
}
