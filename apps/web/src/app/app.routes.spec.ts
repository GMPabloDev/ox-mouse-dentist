import { TestBed } from '@angular/core/testing';
import type { Route } from '@angular/router';
import { homeRedirect, routes } from './app.routes';
import {
  requireAdministrator,
  requireNonAdministrator,
  requireSession,
} from './core/auth/auth.guards';
import { SessionService } from './core/auth/session';

function route(path: string): Route {
  const found = routes.find((candidate) => candidate.path === path);

  if (!found) {
    throw new Error(`No se encontró la ruta «${path}»`);
  }

  return found;
}

function child(path: string): Route {
  const parent = route('');
  const found = parent.children?.find((candidate) => candidate.path === path);

  if (!found) {
    throw new Error(`No se encontró la ruta hija «${path}»`);
  }

  return found;
}

describe('rutas por perfil', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('lleva a administración a locales y al resto al catálogo', () => {
    const session = { isAdministrator: () => true };
    TestBed.configureTestingModule({
      providers: [{ provide: SessionService, useValue: session }],
    });

    expect(TestBed.runInInjectionContext(homeRedirect)).toBe('/locales');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: SessionService, useValue: { isAdministrator: () => false } }],
    });

    expect(TestBed.runInInjectionContext(homeRedirect)).toBe('/catalogo');
  });

  it('reserva personal y locales a administración y deja el catálogo al equipo', () => {
    expect(child('personal').canActivate).toEqual([requireAdministrator]);
    expect(child('locales').canActivate).toEqual([requireAdministrator]);
    expect(child('catalogo').canActivate).toBeUndefined();
    expect(child('catalogo').loadComponent).toBeTypeOf('function');
    expect(child('')?.redirectTo).toBe(homeRedirect);
  });

  it('deja la bienvenida a los perfiles sin módulos y exige sesión', () => {
    expect(route('bienvenida').canActivate).toEqual([requireNonAdministrator]);
    expect(route('sin-acceso').canActivate).toEqual([requireSession]);

    const parent = route('');
    expect(parent.canActivate).toEqual([requireSession]);
    expect(parent.loadComponent).toBeTypeOf('function');
  });

  it('mantiene públicas las pantallas de contraseña', () => {
    for (const path of ['recuperar-acceso', 'establecer-contrasena', 'acceso']) {
      const current = route(path);

      expect(current.canActivate).toBeUndefined();
      expect(current.loadComponent).toBeTypeOf('function');
    }
  });
});
