import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AUTH_API, AuthService, withRenewedToken } from './auth.service';

const CORRELATION_HEADER = 'X-Correlation-Id';

/**
 * - Agrega el access token y un X-Correlation-Id a cada petición a la API (soporte puede rastrear la petición).
 * - Ante un 401 renueva el access token una vez y reintenta. Si la renovación falla, la sesión terminó.
 * Las rutas de autenticación no se reintentan (evita bucles).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/')) return next(req);

  const auth = inject(AuthService);
  const isAuthEndpoint = req.url.startsWith(`${AUTH_API}/login`) || req.url.startsWith(`${AUTH_API}/refresh`)
    || req.url.startsWith(`${AUTH_API}/logout`);

  const send = (token: string | null) => next(decorate(req, token));

  return send(auth.accessToken).pipe(
    catchError((error: unknown) => {
      if (isAuthEndpoint || !(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return withRenewedToken(auth, (token) => send(token), () => {
        void auth.sessionExpired();
        return throwError(() => error);
      });
    }),
  );
};

function decorate(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  const headers: Record<string, string> = { [CORRELATION_HEADER]: crypto.randomUUID().replaceAll('-', '') };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return req.clone({ setHeaders: headers });
}
