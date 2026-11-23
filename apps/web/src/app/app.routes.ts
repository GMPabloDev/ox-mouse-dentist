import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import {
  requireAdministrator,
  requireNonAdministrator,
  requireSession,
} from './core/auth/auth.guards';
import { homePathFor, SessionService } from './core/auth/session';

/**
 * Destino inicial del panel según el perfil. Administración conserva locales;
 * recepción y profesionales entran al catálogo.
 */
export function homeRedirect(): string {
  return homePathFor(inject(SessionService).isAdministrator());
}

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
    path: 'recuperar-acceso',
    title: 'Recuperar acceso · MouseDent',
    loadComponent: () => import('./features/auth/recovery-page').then((m) => m.RecoveryPage),
  },
  {
    path: 'establecer-contrasena',
    title: 'Establecer contraseña · MouseDent',
    loadComponent: () =>
      import('./features/auth/reset-password-page').then((m) => m.ResetPasswordPage),
  },
  {
    path: 'bienvenida',
    title: 'Bienvenida · MouseDent',
    canActivate: [requireNonAdministrator],
    loadComponent: () => import('./features/welcome/welcome-page').then((m) => m.WelcomePage),
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
        path: 'catalogo',
        title: 'Catálogo · MouseDent',
        loadComponent: () =>
          import('./features/catalogo/catalogo-page').then((m) => m.CatalogoPage),
      },
      {
        path: 'personal',
        title: 'Personal · MouseDent',
        canActivate: [requireAdministrator],
        loadComponent: () => import('./features/staff/staff-page').then((m) => m.StaffPage),
      },
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
      { path: '', pathMatch: 'full', redirectTo: homeRedirect },
    ],
  },
  { path: '**', redirectTo: '' },
];
