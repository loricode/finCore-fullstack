import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { CurrentUser, SessionResponse } from './auth.models';

export const AUTH_API = '/api/v1/auth';

/**
 * Sesión del usuario.
 * - El access token vive solo en memoria (signal): no se guarda en localStorage, así un script inyectado
 *   no puede robar una sesión persistente.
 * - El refresh token es una cookie HttpOnly que el navegador envía solo a /api/v1/auth.
 * - Al recargar la página, restoreSession() pide un access token nuevo con esa cookie.
 * La interfaz usa los permisos para mostrar u ocultar opciones; la API valida cada petición.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly token = signal<string | null>(null);
  private readonly user = signal<CurrentUser | null>(null);
  private refreshInFlight: Observable<string | null> | null = null;

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.token() !== null && this.user() !== null);

  get accessToken(): string | null {
    return this.token();
  }

  hasPermission(permission: string): boolean {
    return this.user()?.permissions.includes(permission) ?? false;
  }

  /** rememberMe = false: la sesión termina al cerrar el navegador (cookie de sesión). */
  async login(userName: string, password: string, rememberMe = true): Promise<void> {
    const session = await firstValueFrom(this.http.post<SessionResponse>(`${AUTH_API}/login`, { userName, password, rememberMe }));
    this.token.set(session.accessToken);
    await this.loadCurrentUser();
  }

  /** Al iniciar la aplicación: si la cookie de refresh sigue vigente, la sesión continúa sin pedir contraseña. */
  async restoreSession(): Promise<void> {
    const token = await firstValueFrom(this.refreshAccessToken());
    if (token) {
      await this.loadCurrentUser().catch(() => this.clear());
    }
  }

  /**
   * Pide un access token nuevo. Si varias peticiones reciben 401 a la vez, comparten una sola renovación
   * (el refresh token rota en cada uso: dos renovaciones simultáneas invalidarían la sesión).
   */
  refreshAccessToken(): Observable<string | null> {
    this.refreshInFlight ??= this.http.post<SessionResponse>(`${AUTH_API}/refresh`, {}).pipe(
      map((session) => session.accessToken),
      tap((token) => this.token.set(token)),
      catchError(() => {
        this.clear();
        return of(null);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post(`${AUTH_API}/logout`, {}).pipe(catchError(() => of(null))));
    this.clear();
    await this.router.navigate(['/login']);
  }

  /** La sesión terminó del lado del servidor (refresh rechazado). */
  async sessionExpired(): Promise<void> {
    this.clear();
    await this.router.navigate(['/login'], { queryParams: { expired: 1 } });
  }

  private async loadCurrentUser(): Promise<void> {
    const me = await firstValueFrom(this.http.get<CurrentUser>(`${AUTH_API}/me`));
    this.user.set(me);
  }

  private clear(): void {
    this.token.set(null);
    this.user.set(null);
  }
}

/** Utilidad para operadores RxJS que necesitan reintentar con el token renovado. */
export function withRenewedToken<T>(auth: AuthService, retry: (token: string) => Observable<T>, onExpired: () => Observable<T>) {
  return auth.refreshAccessToken().pipe(switchMap((token) => (token ? retry(token) : onExpired())));
}
