import { TestBed } from '@angular/core/testing';
import { AUTH_API, type AccountCheck, type AuthApi, type LoginAttempt } from './auth-api';
import {
  ADMINISTRATOR_ROLE,
  SESSION_EXPIRED_MESSAGE,
  SessionService,
  type SessionRecord,
} from './session';

const ADMINISTRATOR: SessionRecord = {
  accessToken: 'token-administrador',
  expiresAt: expiresIn(60 * 60 * 1000),
  user: {
    id: '6f1b1f2e-0000-4000-8000-000000000001',
    email: 'admin@mousedent.local',
    displayName: 'Administrador',
    roles: [ADMINISTRATOR_ROLE],
  },
};

function expiresIn(offsetMs: number): string {
  return new Date(Date.now() + offsetMs).toISOString();
}

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

function configure(api: AuthApi): void {
  TestBed.configureTestingModule({ providers: [{ provide: AUTH_API, useValue: api }] });
}

/** Simula recargar la página: injector nuevo, mismo localStorage. */
function reload(api: AuthApi): SessionService {
  TestBed.resetTestingModule();
  configure(api);
  return TestBed.inject(SessionService);
}

describe('SessionService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('autentica al administrador y conserva la sesión al recargar', async () => {
    const api = authApi();
    configure(api);

    const session = TestBed.inject(SessionService);
    const attempt = await session.login('admin@mousedent.local', 'Admin123!');

    expect(attempt.ok).toBe(true);
    expect(api.login).toHaveBeenCalledWith('admin@mousedent.local', 'Admin123!');
    expect(session.isSessionActive()).toBe(true);
    expect(session.user()?.email).toBe('admin@mousedent.local');
    expect(session.isAdministrator()).toBe(true);

    const reloaded = reload(api);
    expect(reloaded.hasSession()).toBe(false);

    await reloaded.restore();

    expect(reloaded.isSessionActive()).toBe(true);
    expect(reloaded.accessToken()).toBe('token-administrador');
    expect(api.currentAccount).toHaveBeenCalledWith('token-administrador');
    expect(reloaded.isAdministrator()).toBe(true);
  });

  it('no autentica con credenciales incorrectas y conserva el mensaje de la API', async () => {
    const message = 'El correo o la contraseña no son correctos.';
    configure(
      authApi({
        login: vi.fn(async () => ({
          ok: false as const,
          failure: 'credentials' as const,
          serverMessage: message,
        })),
      }),
    );

    const session = TestBed.inject(SessionService);
    const attempt = await session.login('admin@mousedent.local', 'otra-clave');

    expect(attempt).toEqual({ ok: false, failure: 'credentials', serverMessage: message });
    expect(session.hasSession()).toBe(false);
    expect(session.isAdministrator()).toBe(false);
  });

  it('no autentica cuando la API no responde', async () => {
    configure(
      authApi({
        login: vi.fn(async () => ({ ok: false as const, failure: 'unavailable' as const })),
      }),
    );

    const session = TestBed.inject(SessionService);
    const attempt = await session.login('admin@mousedent.local', 'Admin123!');

    expect(attempt.ok).toBe(false);
    expect(session.hasSession()).toBe(false);
  });

  it('reconoce la sesión de otro perfil sin darle acceso de administrador', async () => {
    const receptionist: SessionRecord = {
      ...ADMINISTRATOR,
      accessToken: 'token-recepcion',
      user: { ...ADMINISTRATOR.user, displayName: 'Recepción', roles: ['Recepcion'] },
    };
    configure(
      authApi({ login: vi.fn(async () => ({ ok: true as const, session: receptionist })) }),
    );

    const session = TestBed.inject(SessionService);
    await session.login('recepcion@mousedent.local', 'Recepcion123!');

    expect(session.isSessionActive()).toBe(true);
    expect(session.isAdministrator()).toBe(false);
  });

  it('descarta un token vencido al recargar y pide iniciar sesión de nuevo', async () => {
    const expired: SessionRecord = { ...ADMINISTRATOR, expiresAt: expiresIn(-60 * 1000) };
    const api = authApi({ login: vi.fn(async () => ({ ok: true as const, session: expired })) });

    configure(api);
    await TestBed.inject(SessionService).login('admin@mousedent.local', 'Admin123!');

    const reloaded = reload(api);
    await reloaded.restore();

    expect(reloaded.hasSession()).toBe(false);
    expect(reloaded.endNotice()).toBe(SESSION_EXPIRED_MESSAGE);
    expect(api.currentAccount).not.toHaveBeenCalled();
  });

  it('descarta la sesión cuando la API ya no reconoce el token', async () => {
    const api = authApi({
      currentAccount: vi.fn(async () => ({ ok: false as const, failure: 'unauthorized' as const })),
    });

    configure(api);
    await TestBed.inject(SessionService).login('admin@mousedent.local', 'Admin123!');

    const reloaded = reload(api);
    await reloaded.restore();

    expect(reloaded.hasSession()).toBe(false);
    expect(reloaded.endNotice()).toBe(SESSION_EXPIRED_MESSAGE);
  });

  it('mantiene la sesión guardada cuando la revalidación no puede conectarse', async () => {
    const api = authApi({
      currentAccount: vi.fn(async () => ({ ok: false as const, failure: 'unavailable' as const })),
    });

    configure(api);
    await TestBed.inject(SessionService).login('admin@mousedent.local', 'Admin123!');

    const reloaded = reload(api);
    await reloaded.restore();

    expect(reloaded.isSessionActive()).toBe(true);
    expect(reloaded.endNotice()).toBeNull();
  });

  it('cierra la sesión por decisión de la persona y la olvida para la siguiente carga', async () => {
    const api = authApi();
    configure(api);

    const session = TestBed.inject(SessionService);
    await session.login('admin@mousedent.local', 'Admin123!');
    session.logout();

    expect(session.hasSession()).toBe(false);
    expect(session.isAdministrator()).toBe(false);
    expect(session.endNotice()).toBeNull();

    const reloaded = reload(api);
    await reloaded.restore();

    expect(reloaded.hasSession()).toBe(false);
  });

  it('anuncia la caducidad solo si había una sesión abierta', async () => {
    configure(authApi());

    const session = TestBed.inject(SessionService);
    session.expire();

    expect(session.endNotice()).toBeNull();

    await session.login('admin@mousedent.local', 'Admin123!');
    session.expire();

    expect(session.hasSession()).toBe(false);
    expect(session.endNotice()).toBe(SESSION_EXPIRED_MESSAGE);
  });

  it('no se autentica con una sesión guardada ilegible', async () => {
    const api = authApi();
    configure(api);

    await TestBed.inject(SessionService).login('admin@mousedent.local', 'Admin123!');
    for (let index = 0; index < localStorage.length; index += 1) {
      const key = localStorage.key(index);
      if (key) {
        localStorage.setItem(key, '{ esto no es json');
      }
    }

    const reloaded = reload(api);
    await reloaded.restore();

    expect(reloaded.hasSession()).toBe(false);
  });
});
