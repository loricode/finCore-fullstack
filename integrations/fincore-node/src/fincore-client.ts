import type { FinCoreBankEvent } from './translate.ts';

export interface ApiResponse {
  status: number;
  body: unknown;
}

/** Lo que necesita la cola de entrega (permite usar un doble en las pruebas). */
export interface BankEventSender {
  send(event: FinCoreBankEvent, correlationId: string): Promise<ApiResponse>;
}

interface ClientOptions {
  baseUrl: string;
  userName: string;
  password: string;
  fetchImpl?: typeof fetch;
}

/**
 * Cliente de la API de FinCore con la cuenta de servicio (permiso integration.webhooks).
 * Guarda el access token en memoria y vuelve a iniciar sesión si vence (401), una sola vez por envío.
 */
export class FinCoreClient implements BankEventSender {
  private readonly options: ClientOptions;
  private readonly fetchImpl: typeof fetch;
  private token: string | null = null;

  constructor(options: ClientOptions) {
    this.options = options;
    this.fetchImpl = options.fetchImpl ?? fetch;
  }

  async send(event: FinCoreBankEvent, correlationId: string): Promise<ApiResponse> {
    let response = await this.post(event, correlationId);
    if (response.status === 401) {
      this.token = null;
      response = await this.post(event, correlationId);
    }
    return response;
  }

  private async post(event: FinCoreBankEvent, correlationId: string): Promise<ApiResponse> {
    const token = await this.accessToken();
    const res = await this.fetchImpl(`${this.options.baseUrl}/api/v1/integrations/bank-events`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, 'X-Correlation-Id': correlationId },
      body: JSON.stringify(event),
      signal: AbortSignal.timeout(15_000),
    });
    return { status: res.status, body: await res.json().catch(() => null) };
  }

  private async accessToken(): Promise<string> {
    if (this.token) return this.token;
    const res = await this.fetchImpl(`${this.options.baseUrl}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName: this.options.userName, password: this.options.password, rememberMe: false }),
      signal: AbortSignal.timeout(15_000),
    });
    if (!res.ok) throw new Error(`No se pudo iniciar sesión en FinCore como ${this.options.userName} (HTTP ${res.status})`);
    const body = (await res.json()) as { accessToken: string };
    this.token = body.accessToken;
    return this.token;
  }
}
