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

const api: AuthApi = {
  login: vi.fn(async (): Promise<LoginAttempt> => ({ ok: true, session: ADMINISTRATOR })),
  currentAccount: vi.fn(async (): Promise<AccountCheck> => ({
    ok: true,
    user: ADMINISTRATOR.user,
  })),
};

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

async function renderPanel(): Promise<{
  fixture: ComponentFixture<PanelLayout>;
  session: SessionService;
  navigate: ReturnType<typeof vi.fn>;
}> {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AUTH_API, useValue: api }],
  });

  const session = TestBed.inject(SessionService);
  await session.login('admin@mousedent.local', 'Admin123!');

  const navigate = vi
    .spyOn(TestBed.inject(Router), 'navigateByUrl')
    .mockResolvedValue(true) as unknown as ReturnType<typeof vi.fn>;

  const fixture = TestBed.createComponent(PanelLayout);
  await flush(fixture);

  return { fixture, session, navigate };
}

describe('PanelLayout', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('muestra la cuenta autenticada y solo el destino de locales', async () => {
    const { fixture } = await renderPanel();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.textContent).toContain('Administrador');
    expect(host.querySelector('a[href="/locales"]')).not.toBeNull();
    expect(host.querySelectorAll('nav a').length).toBe(1);
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
