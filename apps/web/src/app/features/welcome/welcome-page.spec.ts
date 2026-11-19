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
import { WelcomePage } from './welcome-page';

const PROFESSIONAL: SessionRecord = {
  accessToken: 'token-profesional',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: 'profesional-1',
    email: 'diego.salazar@example.test',
    displayName: 'Diego Salazar',
    roles: ['Profesional'],
  },
};

const api: AuthApi = {
  login: vi.fn(async (): Promise<LoginAttempt> => ({ ok: true, session: PROFESSIONAL })),
  currentAccount: vi.fn(async (): Promise<AccountCheck> => ({
    ok: true,
    user: PROFESSIONAL.user,
  })),
};

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

async function render(): Promise<{
  fixture: ComponentFixture<WelcomePage>;
  session: SessionService;
  navigate: ReturnType<typeof vi.fn>;
}> {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AUTH_API, useValue: api }],
  });

  const session = TestBed.inject(SessionService);
  await session.login('diego.salazar@example.test', 'Demo123!');

  const navigate = vi
    .spyOn(TestBed.inject(Router), 'navigateByUrl')
    .mockResolvedValue(true) as unknown as ReturnType<typeof vi.fn>;

  const fixture = TestBed.createComponent(WelcomePage);
  await flush(fixture);

  return { fixture, session, navigate };
}

describe('WelcomePage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('muestra la identidad del perfil y una sola salida: cerrar sesión', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;
    const text = host.textContent ?? '';

    expect(text).toContain('Hola, Diego');
    expect(text).toContain('Diego Salazar');
    expect(text).toContain('diego.salazar@example.test');
    expect(text).toContain('Profesional');
    expect(text).toContain('Todavía no hay módulos de trabajo disponibles');
    expect(text).not.toContain('Locales');
    expect(text).not.toContain('Personal');
    expect(host.querySelectorAll('a').length).toBe(0);
    expect(host.querySelectorAll('button').length).toBe(1);
  });

  it('cierra la sesión y vuelve al acceso', async () => {
    const { fixture, session, navigate } = await render();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button')!.click();
    await flush(fixture);

    expect(session.hasSession()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/acceso');
  });
});
