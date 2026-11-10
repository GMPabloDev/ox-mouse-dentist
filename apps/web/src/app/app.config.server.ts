import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_BASE_URL } from './core/api/api-base-url';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),

    // Las peticiones que ocurren durante el SSR necesitan una URL absoluta.
    // En producción define API_ORIGIN con la dirección interna de la API.
    { provide: API_BASE_URL, useValue: process.env['API_ORIGIN'] ?? 'http://localhost:5080' },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
