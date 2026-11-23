import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import {
  AUTH_API,
  type AccountCheck,
  type AuthApi,
  type LoginAttempt,
} from '../../core/auth/auth-api';
import { SessionService, type SessionRecord } from '../../core/auth/session';
import { PanelLayout } from './panel-layout';

const ADMINISTRATOR: SessionRecord = {
  accessToken: 'token-administrador',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '6f1b1f2e-0000-4000-8000-000000000001',
    email: 'admin@mousedent.local',
    displayName: 'Administrador',
    roles: ['Administrador'],
  },
};

const RECEPTION: SessionRecord = {
  accessToken: 'token-recepcion',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '6f1b1f2e-0000-4000-8000-000000000002',
    email: 'recepcion@mousedent.local',
    displayName: 'Recepción',
    roles: ['Recepcion'],
  },
};

const PROFESSIONAL: SessionRecord = {
  accessToken: 'token-profesional',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '6f1b1f2e-0000-4000-8000-000000000003',
    email: 'profesional@mousedent.local',
    displayName: 'Profesional',
    roles: ['Profesional'],
  },
};

@Component({ template: '' })
class BlankPage {}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

async function renderPanel(
  session: SessionRecord = ADMINISTRATOR,
  path = '/locales',
): Promise<{
  fixture: ComponentFixture<PanelLayout>;
  session: SessionService;
  navigate: ReturnType<typeof vi.fn>;
}> {
  const api: AuthApi = {
    login: vi.fn(async (): Promise<LoginAttempt> => ({ ok: true, session })),
    currentAccount: vi.fn(async (): Promise<AccountCheck> => ({ ok: true, user: session.user })),
  };

  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'locales', component: BlankPage },
        { path: 'catalogo', component: BlankPage },
      ]),
      { provide: AUTH_API, useValue: api },
    ],
  });

  const service = TestBed.inject(SessionService);
  await service.login(session.user.email, 'Admin123!');
  await TestBed.inject(Router).navigateByUrl(path);

  const navigate = vi
    .spyOn(TestBed.inject(Router), 'navigateByUrl')
    .mockResolvedValue(true) as unknown as ReturnType<typeof vi.fn>;

  const fixture = TestBed.createComponent(PanelLayout);
  await flush(fixture);

  return { fixture, session: service, navigate };
}

describe('PanelLayout', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('muestra a administración la cuenta, los tres destinos y el rótulo de administración', async () => {
    const { fixture } = await renderPanel();
    const host = fixture.nativeElement as HTMLElement;
    const sidebar = host.querySelector('aside')!;

    expect(host.textContent).toContain('Administrador');
    expect(sidebar.querySelector('a[href="/locales"]')).not.toBeNull();
    expect(sidebar.querySelector('a[href="/personal"]')).not.toBeNull();
    expect(sidebar.querySelector('a[href="/catalogo"]')).not.toBeNull();
    expect(sidebar.querySelectorAll('nav a').length).toBe(3);
    expect(sidebar.textContent).toContain('Panel de administración');
    expect(sidebar.textContent).toContain('Administración');
    expect(sidebar.getAttribute('aria-label')).toBe('Panel de administración');
    expect(host.querySelector('header')!.textContent).toMatch(/Administración\s*\/\s*Locales/);
  });

  it.each<[string, SessionRecord]>([
    ['Recepción', RECEPTION],
    ['Profesional', PROFESSIONAL],
  ])('deja a %s solo el catálogo y sin rótulos de administración', async (_label, session) => {
    const { fixture } = await renderPanel(session, '/catalogo');
    const host = fixture.nativeElement as HTMLElement;
    const sidebar = host.querySelector('aside')!;

    expect(sidebar.getAttribute('aria-label')).toBe('Panel');
    expect(sidebar.textContent).not.toContain('Administración');
    expect(sidebar.querySelectorAll('nav a').length).toBe(1);
    expect(sidebar.querySelector('a[href="/catalogo"]')).not.toBeNull();
    expect(sidebar.querySelector('a[href="/locales"]')).toBeNull();
    expect(sidebar.querySelector('a[href="/personal"]')).toBeNull();
    expect(host.querySelector('header p')!.textContent!.replace(/\s+/g, ' ').trim()).toBe(
      'Catálogo',
    );
  });

  it('cierra la sesión y vuelve al acceso', async () => {
    const { fixture, session, navigate } = await renderPanel();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[aria-label="Cerrar sesión"]')!
      .click();
    await flush(fixture);

    expect(session.hasSession()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/acceso');
  });

  it('abre el menú móvil conservando los estilos del sidebar', async () => {
    const { fixture } = await renderPanel();
    const host = fixture.nativeElement as HTMLElement;
    const sidebar = host.querySelector('aside')!;

    expect(sidebar.classList.contains('-translate-x-full')).toBe(true);

    host.querySelector<HTMLButtonElement>('button[aria-label="Abrir menú"]')!.click();
    await flush(fixture);

    expect(sidebar.classList.contains('translate-x-0')).toBe(true);
    expect(sidebar.classList.contains('-translate-x-full')).toBe(false);
    expect(sidebar.classList.contains('z-50')).toBe(true);
    expect(sidebar.classList.contains('bg-sidebar')).toBe(true);
  });
});
