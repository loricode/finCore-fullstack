/**
 * Traducción del formato del proveedor al contrato de FinCore (spec integracion-webhooks).
 * Aquí NO hay reglas financieras: solo forma (campos, tipos, unidades). Las reglas viven en la API.
 *
 * Proveedor "banco-simulado":
 *   { event_id, type: "movement.created", created_at,
 *     data: { account_number, movement_id, reference, booking_date: "YYYY-MM-DD", amount_cents, currency, narrative } }
 */
export interface ProviderEvent {
  event_id?: unknown;
  type?: unknown;
  created_at?: unknown;
  data?: {
    account_number?: unknown;
    movement_id?: unknown;
    reference?: unknown;
    booking_date?: unknown;
    amount_cents?: unknown;
    currency?: unknown;
    narrative?: unknown;
  };
}

export interface FinCoreBankEvent {
  provider: string;
  externalEventId: string;
  eventType: 'movement.created';
  occurredAt: string | null;
  movement: {
    accountNumber: string;
    movementId: string;
    reference: string | null;
    date: string;
    amount: number;
    description: string | null;
  };
}

export type TranslateResult =
  | { kind: 'event'; event: FinCoreBankEvent }
  | { kind: 'ignored'; reason: string }
  | { kind: 'invalid'; reason: string };

const text = (v: unknown): string | null => (typeof v === 'string' && v.trim() ? v.trim() : null);

export function translate(provider: string, payload: ProviderEvent): TranslateResult {
  const eventId = text(payload.event_id);
  if (!eventId) return { kind: 'invalid', reason: 'event_id es obligatorio' };
  if (payload.type !== 'movement.created') return { kind: 'ignored', reason: `tipo de evento no soportado: ${String(payload.type)}` };

  const d = payload.data ?? {};
  const account = text(d.account_number);
  const movementId = text(d.movement_id);
  const date = text(d.booking_date);
  if (!account || !movementId) return { kind: 'invalid', reason: 'data.account_number y data.movement_id son obligatorios' };
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return { kind: 'invalid', reason: 'data.booking_date debe ser YYYY-MM-DD' };
  if ((d.currency ?? 'COP') !== 'COP') return { kind: 'invalid', reason: `moneda no soportada: ${String(d.currency)}` };
  if (!Number.isSafeInteger(d.amount_cents) || d.amount_cents === 0) return { kind: 'invalid', reason: 'data.amount_cents debe ser un entero distinto de cero' };

  return {
    kind: 'event',
    event: {
      provider,
      externalEventId: eventId,
      eventType: 'movement.created',
      occurredAt: text(payload.created_at),
      movement: {
        accountNumber: account,
        movementId,
        reference: text(d.reference),
        date,
        // El proveedor envía centavos (entero) para evitar errores de coma flotante; FinCore usa pesos con 2 decimales.
        amount: Number(((d.amount_cents as number) / 100).toFixed(2)),
        description: text(d.narrative),
      },
    },
  };
}
