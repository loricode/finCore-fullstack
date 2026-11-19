/** Configuración por variables de entorno (Docker). Sin valores secretos por defecto fuera de desarrollo. */
export interface ProviderConfig {
  /** Secreto compartido con el proveedor para firmar los webhooks (HMAC-SHA256). */
  secret: string;
}

export interface Config {
  port: number;
  providers: Record<string, ProviderConfig>;
  fincoreBaseUrl: string;
  serviceUser: string;
  servicePassword: string;
  /** Esperas entre reintentos de entrega a la API (ms): 1 s, 2 s, 4 s, 8 s, 16 s. */
  retryDelaysMs: number[];
  deadLetterPath: string;
  /** Antigüedad máxima aceptada de un webhook firmado (evita repetir uno capturado). */
  signatureToleranceSeconds: number;
}

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  const required = (name: string, fallback?: string): string => {
    const value = env[name] ?? fallback;
    if (!value) throw new Error(`Falta la variable de entorno ${name}`);
    return value;
  };

  const base = Number(env.RETRY_BASE_MS ?? 1000);
  return {
    port: Number(env.PORT ?? 5090),
    providers: {
      'banco-simulado': { secret: required('BANK_WEBHOOK_SECRET', env.NODE_ENV === 'production' ? undefined : 'dev-secreto-webhook') },
    },
    fincoreBaseUrl: required('FINCORE_API_URL', 'http://localhost:5080'),
    serviceUser: required('FINCORE_SERVICE_USER', 'svc.integration'),
    servicePassword: required('FINCORE_SERVICE_PASSWORD', env.NODE_ENV === 'production' ? undefined : 'FinCore*2026'),
    retryDelaysMs: [1, 2, 4, 8, 16].map((n) => n * base),
    deadLetterPath: env.DEAD_LETTER_PATH ?? './data/dead-letter.jsonl',
    signatureToleranceSeconds: Number(env.SIGNATURE_TOLERANCE_SECONDS ?? 300),
  };
}
