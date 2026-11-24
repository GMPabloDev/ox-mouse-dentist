import { isPlatformBrowser } from '@angular/common';
import {
  ApplicationConfig,
  inject,
  PLATFORM_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideClientHydration } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { configureApiClient } from '@mousedent/api-client';
import { routes } from './app.routes';
import { API_BASE_URL, provideApiBaseUrl } from './core/api/api-base-url';
import { installSessionExpiryInterceptor } from './core/api/session-expiry.interceptor';
import { AUTH_API } from './core/auth/auth-api';
import { GeneratedAuthApi } from './core/auth/generated-auth-api';
import { GeneratedPasswordApi } from './core/auth/generated-password-api';
import { PASSWORD_API } from './core/auth/password-api';
import { SessionService } from './core/auth/session';
import { GeneratedCatalogoApi } from './core/catalogo/generated-catalogo-api';
import { CATALOGO_API } from './core/catalogo/catalogo-api';
import { GeneratedLocalesApi } from './core/locales/generated-locales-api';
import { LOCALES_API } from './core/locales/locales-api';
import { GeneratedProfesionalesApi } from './core/profesionales/generated-profesionales-api';
import { PROFESIONALES_API } from './core/profesionales/profesionales-api';
import { GeneratedStaffApi } from './core/staff/generated-staff-api';
import { STAFF_API } from './core/staff/staff-api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideClientHydration(),

    // El navegador habla con el mismo origen; el proxy de desarrollo y el
    // reverse proxy de producción enrutan /api hacia la API.
    provideApiBaseUrl(),

    { provide: AUTH_API, useClass: GeneratedAuthApi },
    { provide: PASSWORD_API, useClass: GeneratedPasswordApi },
    { provide: LOCALES_API, useClass: GeneratedLocalesApi },
    { provide: CATALOGO_API, useClass: GeneratedCatalogoApi },
    { provide: STAFF_API, useClass: GeneratedStaffApi },
    { provide: PROFESIONALES_API, useClass: GeneratedProfesionalesApi },

    provideAppInitializer(() => {
      const browser = isPlatformBrowser(inject(PLATFORM_ID));
      const session = inject(SessionService);

      configureApiClient({
        baseUrl: inject(API_BASE_URL),
        // El cliente generado es un singleton del proceso: el bearer solo se
        // registra en el navegador, donde la sesión pertenece a esta pestaña.
        accessToken: browser ? () => session.accessToken() : undefined,
      });

      if (!browser) {
        return;
      }

      installSessionExpiryInterceptor();

      // Revalida el token guardado antes de mostrar el panel.
      return session.restore();
    }),
  ],
};
