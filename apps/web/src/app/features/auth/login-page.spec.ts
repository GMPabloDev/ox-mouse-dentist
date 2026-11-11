import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AUTH_API,
  type AccountCheck,
  type AuthApi,
  type LoginAttempt,
} from '../../core/auth/auth-api';
import { SessionService, type SessionRecord } from '../../core/auth/session';
import { LoginPage } from './login-page';

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

function authApi(overrides: Partial<AuthApi> = {}): AuthApi {
  return {
    login: vi.fn(async (): Promise<LoginAttempt> => ({ ok: true, session: ADMINISTRATOR })),
    currentAccount: vi.fn(async (): Promise<AccountCheck> => ({
      ok: true,
      user: ADMINISTRATOR.user,
    })),
    ...overrides,
  };
}

function configure(api: AuthApi): { navigate: ReturnType<typeof vi.fn> } {
  const navigate = vi.fn(async () => true);
  TestBed.configureTestingModule({
    providers: [
      { provide: AUTH_API, useValue: api },
      { provide: Router, useValue: { navigateByUrl: navigate } },
    ],
  });
  return { navigate };
}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

function element<T extends Element>(fixture: ComponentFixture<unknown>, selector: string): T {
  const found = (fixture.nativeElement as HTMLElement).querySelector<T>(selector);
  if (!found) {
    throw new Error(`No se encontró ${selector}`);
  }
  return found;
}

async function render(api: AuthApi): Promise<ComponentFixture<LoginPage>> {
  configure(api);
  const fixture = TestBed.createComponent(LoginPage);
  await flush(fixture);
  return fixture;
}

async function submitCredentials(
  fixture: ComponentFixture<LoginPage>,
  email: string,
  password: string,
): Promise<void> {
  const emailInput = element<HTMLInputElement>(fixture, '#email');
  emailInput.value = email;
  emailInput.dispatchEvent(new Event('input'));

  const passwordInput = element<HTMLInputElement>(fixture, '#password');
  passwordInput.value = password;
  passwordInput.dispatchEvent(new Event('input'));

  element<HTMLFormElement>(fixture, 'form').dispatchEvent(new Event('submit'));
  await flush(fixture);
}

describe('LoginPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('entra con la cuenta de Development y abre la gestión de locales', async () => {
    const api = authApi();
    const { navigate } = configure(api);
    const fixture = TestBed.createComponent(LoginPage);
    await flush(fixture);

    await submitCredentials(fixture, 'admin@mousedent.local', 'Admin123!');

    expect(api.login).toHaveBeenCalledWith('admin@mousedent.local', 'Admin123!');
    expect(navigate).toHaveBeenCalledWith('/locales');
    expect(TestBed.inject(SessionService).isAdministrator()).toBe(true);
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).toBeNull();
  });

  it('muestra el rechazo de credenciales sin decir si la cuenta existe y conserva lo escrito', async () => {
    const message = 'El correo o la contraseña no son correctos.';
    const { navigate } = configure(
      authApi({
        login: vi.fn(async () => ({
          ok: false as const,
          failure: 'credentials' as const,
          serverMessage: message,
        })),
      }),
    );
    const fixture = TestBed.createComponent(LoginPage);
    await flush(fixture);

    await submitCredentials(fixture, 'admin@mousedent.local', 'clave-equivocada');

    expect(element<HTMLElement>(fixture, '[role="alert"]').textContent).toContain(message);
    expect(element<HTMLInputElement>(fixture, '#email').value).toBe('admin@mousedent.local');
    expect(navigate).not.toHaveBeenCalled();
    expect(TestBed.inject(SessionService).hasSession()).toBe(false);
  });

  it('avisa cuando la API no está disponible y no da la operación por buena', async () => {
    const { navigate } = configure(
      authApi({
        login: vi.fn(async () => ({ ok: false as const, failure: 'unavailable' as const })),
      }),
    );
    const fixture = TestBed.createComponent(LoginPage);
    await flush(fixture);

    await submitCredentials(fixture, 'admin@mousedent.local', 'Admin123!');

    expect(element<HTMLElement>(fixture, '[role="alert"]').textContent).toContain(
      'No se pudo iniciar sesión',
    );
    expect(navigate).not.toHaveBeenCalled();
    expect(TestBed.inject(SessionService).hasSession()).toBe(false);
  });

  it('no llama a la API si faltan el correo o la contraseña', async () => {
    const api = authApi();
    const { navigate } = configure(api);
    const fixture = TestBed.createComponent(LoginPage);
    await flush(fixture);

    await submitCredentials(fixture, '', '');

    expect(api.login).not.toHaveBeenCalled();
    expect(element<HTMLElement>(fixture, '[role="alert"]').textContent).toContain(
      'Escribe tu correo y tu contraseña.',
    );
    expect(navigate).not.toHaveBeenCalled();
  });

  it('recuerda que la sesión anterior caducó', async () => {
    const expired: SessionRecord = {
      ...ADMINISTRATOR,
      expiresAt: new Date(Date.now() - 60 * 1000).toISOString(),
    };
    configure(authApi({ login: vi.fn(async () => ({ ok: true as const, session: expired })) }));

    const session = TestBed.inject(SessionService);
    await session.login('admin@mousedent.local', 'Admin123!');
    await session.restore();

    const fixture = TestBed.createComponent(LoginPage);
    await flush(fixture);

    expect(element<HTMLElement>(fixture, '[role="status"]').textContent).toContain(
      'Tu sesión caducó',
    );
  });
});
