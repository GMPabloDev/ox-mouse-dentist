import { TestBed } from '@angular/core/testing';
import {
  provideRouter,
  type ActivatedRouteSnapshot,
  type CanActivateFn,
  type RouterStateSnapshot,
  type UrlTree,
} from '@angular/router';
import { requireAdministrator, requireNonAdministrator, requireSession } from './auth.guards';
import { SessionService } from './session';

interface GuardSession {
  isSessionActive: () => boolean;
  isAdministrator: () => boolean;
  expire: () => void;
}

function run(guard: CanActivateFn, session: GuardSession): boolean | UrlTree {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: SessionService, useValue: session }],
  });

  return TestBed.runInInjectionContext(() =>
    guard({} as ActivatedRouteSnapshot, { url: '/locales' } as RouterStateSnapshot),
  ) as boolean | UrlTree;
}

function session(options: { active: boolean; administrator: boolean }): GuardSession & {
  expire: ReturnType<typeof vi.fn>;
} {
  return {
    isSessionActive: () => options.active,
    isAdministrator: () => options.administrator,
    expire: vi.fn(() => undefined),
  };
}

describe('auth guards', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('deja pasar una sesión vigente', () => {
    const current = session({ active: true, administrator: true });

    expect(run(requireSession, current)).toBe(true);
    expect(current.expire).not.toHaveBeenCalled();
  });

  it('manda al acceso cuando no hay sesión', () => {
    const current = session({ active: false, administrator: false });

    expect(String(run(requireSession, current))).toBe('/acceso');
  });

  it('descarta el token vencido que siguiera en memoria', () => {
    const current = session({ active: false, administrator: false });

    run(requireSession, current);

    expect(current.expire).toHaveBeenCalled();
  });

  it('deja entrar al administrador en la gestión de locales', () => {
    expect(run(requireAdministrator, session({ active: true, administrator: true }))).toBe(true);
  });

  it('rechaza a un perfil que no es administrador', () => {
    const current = session({ active: true, administrator: false });

    expect(String(run(requireAdministrator, current))).toBe('/sin-acceso');
    expect(current.expire).not.toHaveBeenCalled();
  });

  it('manda al acceso cuando la sesión vence antes de entrar a locales', () => {
    const current = session({ active: false, administrator: true });

    expect(String(run(requireAdministrator, current))).toBe('/acceso');
    expect(current.expire).toHaveBeenCalled();
  });

  it('deja la bienvenida a recepción y profesionales', () => {
    const current = session({ active: true, administrator: false });

    expect(run(requireNonAdministrator, current)).toBe(true);
    expect(current.expire).not.toHaveBeenCalled();
  });

  it('devuelve administración a su panel desde la bienvenida', () => {
    const current = session({ active: true, administrator: true });

    expect(String(run(requireNonAdministrator, current))).toBe('/locales');
  });

  it('pide iniciar sesión en la bienvenida cuando no hay sesión vigente', () => {
    const current = session({ active: false, administrator: false });

    expect(String(run(requireNonAdministrator, current))).toBe('/acceso');
    expect(current.expire).toHaveBeenCalled();
  });
});
