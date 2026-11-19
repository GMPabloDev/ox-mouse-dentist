import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Modo de renderizado por ruta. Las pantallas públicas de contraseña se
 * renderizan fuera del panel; el resto usa RenderMode.Client.
 */
export const serverRoutes: ServerRoute[] = [
  {
    path: 'recuperar-acceso',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'establecer-contrasena',
    renderMode: RenderMode.Server,
  },
  {
    path: '**',
    renderMode: RenderMode.Client,
  },
];
