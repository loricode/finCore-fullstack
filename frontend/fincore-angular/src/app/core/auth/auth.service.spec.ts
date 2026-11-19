import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CurrentUser } from './auth.models';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const me: CurrentUser = {
  id: 3,
  userName: 'operador.monteria',
  fullName: 'Carlos Operador Montería',
  email: 'operador.monteria@fincore.demo',
  companyId: 1,
  roles: ['OPERATOR'],
  permissions: ['operations.create', 'operations.read'],
  branches: [{ branchId: 1, code: 'MON', name: 'Sucursal Montería', city: 'Montería' }],
};

describe('AuthService e interceptor', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'login', children: [] }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function login(): Promise<void> {
    const done = auth.login('operador.monteria', 'FinCore*2026');
    http.expectOne('/api/v1/auth/login').flush({ accessToken: 'token-1', expiresAtUtc: '2026-11-19T15:00:00Z' });
    await Promise.resolve();
    const meReq = http.expectOne('/api/v1/auth/me');
    expect(meReq.request.headers.get('Authorization')).toBe('Bearer token-1');
    meReq.flush(me);
    await done;
  }

  it('inicia sesión, carga el perfil y expone los permisos', async () => {
    await login();
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.hasPermission('operations.create')).toBe(true);
    expect(auth.hasPermission('users.manage')).toBe(false);
  });

  it('envía un X-Correlation-Id en cada petición a la API', async () => {
    await login();
    const client = TestBed.inject(HttpClient);
    const pending = firstValueFrom(client.get('/api/v1/branches'));
    const req = http.expectOne('/api/v1/branches');
    expect(req.request.headers.get('X-Correlation-Id')).toMatch(/^[a-f0-9]{32}$/);
    req.flush([]);
    await pending;
  });

  it('ante un 401 renueva el token una vez y reintenta la petición', async () => {
    await login();
    const client = TestBed.inject(HttpClient);
    const pending = firstValueFrom(client.get<unknown[]>('/api/v1/branches'));

    http.expectOne('/api/v1/branches').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush({ accessToken: 'token-2', expiresAtUtc: '2026-11-19T15:15:00Z' });
    const retry = http.expectOne('/api/v1/branches');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
    retry.flush([{ branchId: 1 }]);

    expect(await pending).toEqual([{ branchId: 1 }]);
  });

  it('si la renovación falla, la sesión termina', async () => {
    await login();
    const client = TestBed.inject(HttpClient);
    const pending = firstValueFrom(client.get('/api/v1/branches')).catch((e: unknown) => e);

    http.expectOne('/api/v1/branches').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    await pending;
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('restoreSession sin cookie válida deja la sesión cerrada sin errores', async () => {
    const done = auth.restoreSession();
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    await done;
    expect(auth.isAuthenticated()).toBe(false);
  });
});
