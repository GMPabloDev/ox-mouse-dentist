import { Routes } from '@angular/router';
import { requireAdministrator, requireSession } from './core/auth/auth.guards';

/**
 * Las rutas de producto se agregan en las specs. Cada ruta pública necesita
 * una entrada en app.routes.server.ts con RenderMode.Prerender o Server.
 * El acceso y el panel usan RenderMode.Client.
 */
export const routes: Routes = [
  {
    path: 'acceso',
    title: 'Acceso · MouseDent',
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'sin-acceso',
    title: 'Acceso restringido · MouseDent',
    canActivate: [requireSession],
    loadComponent: () =>
      import('./features/auth/access-denied-page').then((m) => m.AccessDeniedPage),
  },
  {
    path: '',
    canActivate: [requireSession],
    loadComponent: () => import('./features/panel/panel-layout').then((m) => m.PanelLayout),
    children: [
      {
        path: 'locales',
        canActivate: [requireAdministrator],
        children: [
          {
            path: '',
            title: 'Locales · MouseDent',
            loadComponent: () =>
              import('./features/locales/locales-page').then((m) => m.LocalesPage),
          },
          {
            path: 'nuevo',
            title: 'Nuevo local · MouseDent',
            loadComponent: () =>
              import('./features/locales/local-form-page').then((m) => m.LocalFormPage),
          },
          {
            path: ':id/editar',
            title: 'Editar local · MouseDent',
            loadComponent: () =>
              import('./features/locales/local-form-page').then((m) => m.LocalFormPage),
          },
        ],
      },
      { path: '', pathMatch: 'full', redirectTo: 'locales' },
    ],
  },
  { path: '**', redirectTo: '' },
];
