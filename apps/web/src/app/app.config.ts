import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideClientHydration } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { configureApiClient } from '@mousedent/api-client';
import { routes } from './app.routes';
import { API_BASE_URL, provideApiBaseUrl } from './core/api/api-base-url';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideClientHydration(),

    // El navegador habla con el mismo origen; el proxy de desarrollo y el
    // reverse proxy de producción enrutan /api hacia la API.
    provideApiBaseUrl(),

    provideAppInitializer(() => {
      configureApiClient({ baseUrl: inject(API_BASE_URL) });
    }),
  ],
};
