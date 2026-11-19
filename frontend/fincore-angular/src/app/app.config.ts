import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import localeEsCo from '@angular/common/locales/es-CO';
import {
  ApplicationConfig,
  DEFAULT_CURRENCY_CODE,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { DateAdapter, MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { EsDateAdapter } from './shared/es-date-adapter';
import { MatIconRegistry } from '@angular/material/icon';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS } from '@angular/material/snack-bar';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';

registerLocaleData(localeEsCo);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor])),
    // withViewTransitions: transición suave entre páginas (View Transitions API del navegador).
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    provideNativeDateAdapter(),
    // Va después de provideNativeDateAdapter para reemplazarlo: lee las fechas escritas como dd/MM/yyyy.
    { provide: DateAdapter, useClass: EsDateAdapter },
    { provide: LOCALE_ID, useValue: 'es-CO' },
    { provide: MAT_DATE_LOCALE, useValue: 'es-CO' },
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'COP' },
    { provide: MAT_SNACK_BAR_DEFAULT_OPTIONS, useValue: { duration: 3500, horizontalPosition: 'end', verticalPosition: 'bottom' } },
    // Íconos Material Symbols (redondeados), alojados en la propia aplicación.
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-rounded');
    }),
    // Antes de la primera navegación intenta continuar la sesión con la cookie de refresh.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
