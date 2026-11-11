import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { client, getCurrentUser } from '@mousedent/api-client';
import { AUTH_API, type AccountCheck, type AuthApi, type LoginAttempt } from '../auth/auth-api';
import { SESSION_EXPIRED_MESSAGE, SessionService, type SessionRecord } from '../auth/session';
import {
  endSessionOnUnauthorized,
  installSessionExpiryInterceptor,
} from './session-expiry.interceptor';

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

function routerSpy(): Router & { navigateByUrl: ReturnType<typeof vi.fn> } {
  return { navigateByUrl: vi.fn(async () => true) } as unknown as Router & {
    navigateByUrl: ReturnType<typeof vi.fn>;
  };
}

describe('session expiry interceptor', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AUTH_API, useValue: api }],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('cierra la sesión y vuelve al acceso cuando una petición autenticada recibe 401', async () => {
    const session = TestBed.inject(SessionService);
    await session.login('admin@mousedent.local', 'Admin123!');
    const router = routerSpy();
    const unauthorized = { detail: 'No autorizado' };

    const result = endSessionOnUnauthorized(session, router)(
      unauthorized,
      new Response('{}', { status: 401 }),
      new Request('http://example.test', {
        headers: { Authorization: 'Bearer token-administrador' },
      }),
    );

    expect(result).toBe(unauthorized);
    expect(session.hasSession()).toBe(false);
    expect(session.endNotice()).toBe(SESSION_EXPIRED_MESSAGE);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/acceso');
  });

  it('no toca la sesión con errores que no son 401', async () => {
    const session = TestBed.inject(SessionService);
    await session.login('admin@mousedent.local', 'Admin123!');
    const router = routerSpy();

    endSessionOnUnauthorized(session, router)(
      new Error('fallo'),
      new Response('{}', { status: 500 }),
    );

    expect(session.isSessionActive()).toBe(true);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('no navega cuando el 401 llega sin sesión abierta', () => {
    const session = TestBed.inject(SessionService);
    const router = routerSpy();

    endSessionOnUnauthorized(session, router)(
      { detail: 'No autorizado' },
      new Response('{}', { status: 401 }),
    );

    expect(session.hasSession()).toBe(false);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('no cierra una sesión nueva por el 401 tardío de otra', async () => {
    const session = TestBed.inject(SessionService);
    await session.login('admin@mousedent.local', 'Admin123!');
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const previousConfig = client.getConfig();
    let finish!: (response: Response) => void;
    client.setConfig({
      baseUrl: 'http://example.test',
      fetch: () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    });
    const interceptorId = TestBed.runInInjectionContext(() => installSessionExpiryInterceptor());
    try {
      const request = getCurrentUser({ headers: { Authorization: 'Bearer token-administrador' } });
      await vi.waitFor(() => expect(finish).toBeDefined());
      session.logout();
      vi.mocked(api.login).mockResolvedValueOnce({
        ok: true,
        session: { ...ADMINISTRATOR, accessToken: 'token-nuevo' },
      });
      await session.login('nuevo@mousedent.local', 'clave');
      finish(new Response('{}', { status: 401 }));
      await request;
      expect(session.accessToken()).toBe('token-nuevo');
      expect(session.endNotice()).toBeNull();
      expect(navigate).not.toHaveBeenCalled();
    } finally {
      client.interceptors.error.eject(interceptorId);
      client.setConfig(previousConfig);
    }
  });

  it('está conectado al cliente generado, que es quien hace las peticiones', async () => {
    const session = TestBed.inject(SessionService);
    await session.login('admin@mousedent.local', 'Admin123!');
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const previousConfig = client.getConfig();
    client.setConfig({
      baseUrl: 'http://localhost:5080',
      fetch: async () =>
        new Response(JSON.stringify({ detail: 'No autorizado' }), {
          status: 401,
          headers: { 'Content-Type': 'application/json' },
        }),
    });

    const interceptorId = TestBed.runInInjectionContext(() => installSessionExpiryInterceptor());

    try {
      const result = await getCurrentUser({
        headers: { Authorization: 'Bearer token-administrador' },
      });

      expect(result.response?.status).toBe(401);
      expect(session.hasSession()).toBe(false);
      expect(navigate).toHaveBeenCalledWith('/acceso');
    } finally {
      client.interceptors.error.eject(interceptorId);
      client.setConfig(previousConfig);
    }
  });
});
