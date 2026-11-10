import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Modo de renderizado por ruta. Declara las rutas públicas con
 * RenderMode.Prerender o RenderMode.Server. El panel interno usa
 * RenderMode.Client.
 */
export const serverRoutes: ServerRoute[] = [
  {
    path: '**',
    renderMode: RenderMode.Client,
  },
];
