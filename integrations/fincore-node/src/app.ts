import { randomUUID } from 'node:crypto';
import express, { type Express } from 'express';
import type { Config } from './config.ts';
import type { DeliveryQueue } from './delivery.ts';
import { log, maskAccount } from './logger.ts';
import { SIGNATURE_HEADER, verify } from './signature.ts';
import { translate, type ProviderEvent } from './translate.ts';

interface AppDeps {
  config: Pick<Config, 'providers' | 'signatureToleranceSeconds'>;
  queue: Pick<DeliveryQueue, 'enqueue' | 'size'>;
  nowSeconds?: () => number;
}

/**
 * Adaptador HTTP. Responde al proveedor en milisegundos (202) y entrega a FinCore en segundo plano.
 * Códigos para el proveedor: 202 aceptado o ignorado · 400 JSON inválido · 401 firma · 404 proveedor · 422 contenido.
 */
export function createApp({ config, queue, nowSeconds = () => Math.floor(Date.now() / 1000) }: AppDeps): Express {
  const app = express();
  app.disable('x-powered-by');

  app.get('/health', (_req, res) => {
    res.json({ status: 'ok', pendingDeliveries: queue.size });
  });

  // Cuerpo crudo: la firma se calcula sobre los bytes exactos que envió el proveedor.
  app.post('/webhooks/:provider', express.raw({ type: '*/*', limit: '64kb' }), (req, res) => {
    const provider = String(req.params.provider);
    const correlationId = /^[A-Za-z0-9_-]{1,64}$/.test(String(req.get('x-correlation-id') ?? ''))
      ? String(req.get('x-correlation-id'))
      : randomUUID().replaceAll('-', '');
    const providerConfig = config.providers[provider];
    if (!providerConfig) {
      res.status(404).json({ error: 'unknown_provider' });
      return;
    }

    const raw: Buffer = Buffer.isBuffer(req.body) ? req.body : Buffer.alloc(0);
    const check = verify(req.get(SIGNATURE_HEADER), raw, providerConfig.secret, nowSeconds(), config.signatureToleranceSeconds);
    if (!check.ok) {
      log('warn', 'Webhook rechazado por firma', { provider, correlationId, reason: check.reason });
      res.status(401).json({ error: check.reason });
      return;
    }

    let payload: ProviderEvent;
    try {
      payload = JSON.parse(raw.toString('utf8')) as ProviderEvent;
    } catch {
      res.status(400).json({ error: 'invalid_json' });
      return;
    }

    const result = translate(provider, payload);
    if (result.kind === 'ignored') {
      log('info', 'Webhook ignorado', { provider, correlationId, reason: result.reason });
      res.status(202).json({ status: 'ignored', reason: result.reason });
      return;
    }
    if (result.kind === 'invalid') {
      log('warn', 'Webhook con contenido inválido', { provider, correlationId, reason: result.reason });
      res.status(422).json({ error: 'invalid_payload', reason: result.reason });
      return;
    }

    queue.enqueue({ event: result.event, correlationId, receivedAt: new Date().toISOString() });
    log('info', 'Webhook aceptado', {
      provider, correlationId, externalEventId: result.event.externalEventId, account: maskAccount(result.event.movement.accountNumber),
    });
    res.status(202).json({ status: 'accepted', eventId: result.event.externalEventId, correlationId });
  });

  return app;
}
