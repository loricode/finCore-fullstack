import { createHmac, timingSafeEqual } from 'node:crypto';

/**
 * Firma de webhooks del proveedor (esquema tipo Stripe):
 *   X-Bank-Signature: t=<unix segundos>,v1=<hex HMAC-SHA256 de "<t>.<cuerpo crudo>">
 * Firmar el timestamp junto con el cuerpo impide reenviar un webhook capturado tiempo después (replay).
 */
export const SIGNATURE_HEADER = 'x-bank-signature';

export function sign(rawBody: string | Buffer, secret: string, timestamp: number): string {
  const digest = createHmac('sha256', secret).update(`${timestamp}.`).update(rawBody).digest('hex');
  return `t=${timestamp},v1=${digest}`;
}

export type VerifyResult = { ok: true } | { ok: false; reason: string };

export function verify(header: string | undefined, rawBody: Buffer, secret: string, nowSeconds: number,
  toleranceSeconds = 300): VerifyResult {
  if (!header) return { ok: false, reason: 'missing_signature' };

  const parts = Object.fromEntries(header.split(',').map((p) => p.trim().split('=', 2) as [string, string]));
  const timestamp = Number(parts.t);
  const provided = parts.v1;
  if (!Number.isInteger(timestamp) || !provided || !/^[a-f0-9]{64}$/.test(provided)) return { ok: false, reason: 'malformed_signature' };
  if (Math.abs(nowSeconds - timestamp) > toleranceSeconds) return { ok: false, reason: 'stale_signature' };

  const expected = sign(rawBody, secret, timestamp).split('v1=')[1];
  // Comparación en tiempo constante: no revela cuántos caracteres coinciden.
  const ok = timingSafeEqual(Buffer.from(expected, 'hex'), Buffer.from(provided, 'hex'));
  return ok ? { ok: true } : { ok: false, reason: 'invalid_signature' };
}
