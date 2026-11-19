import { createApp } from './app.ts';
import { loadConfig } from './config.ts';
import { DeliveryQueue } from './delivery.ts';
import { FinCoreClient } from './fincore-client.ts';
import { log } from './logger.ts';

const config = loadConfig();
const queue = new DeliveryQueue({
  sender: new FinCoreClient({ baseUrl: config.fincoreBaseUrl, userName: config.serviceUser, password: config.servicePassword }),
  retryDelaysMs: config.retryDelaysMs,
  deadLetterPath: config.deadLetterPath,
});

const server = createApp({ config, queue }).listen(config.port, () => {
  log('info', 'Adaptador de webhooks escuchando', { port: config.port, providers: Object.keys(config.providers), api: config.fincoreBaseUrl });
});

// Apagado ordenado: deja de aceptar webhooks y espera (máx. 20 s) a que se entreguen los pendientes.
const shutdown = (signal: string) => {
  log('info', 'Apagando adaptador', { signal, pendingDeliveries: queue.size });
  server.close();
  Promise.race([queue.idle(), new Promise((r) => setTimeout(r, 20_000))]).finally(() => process.exit(0));
};
process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT', () => shutdown('SIGINT'));
