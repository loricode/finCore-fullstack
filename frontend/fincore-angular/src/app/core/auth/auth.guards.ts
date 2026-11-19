import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Exige sesión; si no hay, envía al login recordando a dónde quería ir. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Exige un permiso. Es solo experiencia de usuario: la API vuelve a validar y responde 403. */
export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    return auth.hasPermission(permission) ? true : inject(Router).createUrlTree(['/sin-permiso']);
  };
}

/** El login no tiene sentido con una sesión activa. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/inicio']) : true;
