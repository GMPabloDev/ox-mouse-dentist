import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AUTH_API, type AuthApi } from '../../core/auth/auth-api';
import { SessionService, type SessionRecord } from '../../core/auth/session';
import { AccessDeniedPage } from './access-denied-page';

const account: SessionRecord = {
  accessToken: 'token',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: { id: '1', email: 'persona@example.test', displayName: 'Persona', roles: ['Recepcion'] },
};

async function render(): Promise<{
  fixture: ComponentFixture<AccessDeniedPage>;
  session: SessionService;
  navigate: ReturnType<typeof vi.fn>;
}> {
  const api: AuthApi = {
    login: vi.fn(async () => ({ ok: true as const, session: account })),
    currentAccount: vi.fn(async () => ({ ok: true as const, user: account.user })),
  };
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AUTH_API, useValue: api }],
  });
  const session = TestBed.inject(SessionService);
  await session.login(account.user.email, 'Demo123!');
  const navigate = vi
    .spyOn(TestBed.inject(Router), 'navigateByUrl')
    .mockResolvedValue(true) as unknown as ReturnType<typeof vi.fn>;
  const fixture = TestBed.createComponent(AccessDeniedPage);
  await fixture.whenStable();
  return { fixture, session, navigate };
}

describe('AccessDeniedPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('muestra identidad y cierre de sesión en la cabecera, con acceso a la bienvenida', async () => {
    const { fixture, navigate } = await render();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('header')?.textContent).toContain('Cerrar sesión');
    expect(host.textContent).toContain('persona@example.test');
    expect(host.textContent).toContain('Recepción');
    expect(host.textContent).toContain('No tienes permiso para esta página');
    const button = [...host.querySelectorAll('button')].find((item) =>
      item.textContent?.includes('Ir a mi bienvenida'),
    )!;
    button.click();
    expect(navigate).toHaveBeenCalledWith('/bienvenida');
  });

  it('cierra la sesión desde la cabecera y vuelve al acceso', async () => {
    const { fixture, session, navigate } = await render();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('header button')!
      .click();
    expect(session.hasSession()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/acceso');
  });
});
