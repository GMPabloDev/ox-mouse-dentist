import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { PASSWORD_API, type PasswordApi } from '../../core/auth/password-api';
import { ResetPasswordPage } from './reset-password-page';

function passwordApi(overrides: Partial<PasswordApi> = {}): PasswordApi {
  return {
    requestRecovery: vi.fn(async () => ({ ok: true as const })),
    resetPassword: vi.fn(async () => ({ ok: true as const })),
    ...overrides,
  };
}

function routeWith(token: string | null): ActivatedRoute {
  return {
    snapshot: {
      queryParamMap: convertToParamMap(token === null ? {} : { token }),
    },
  } as unknown as ActivatedRoute;
}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

async function render(
  api: PasswordApi,
  token: string | null = 'token-vigente',
): Promise<ComponentFixture<ResetPasswordPage>> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: PASSWORD_API, useValue: api },
      { provide: ActivatedRoute, useValue: routeWith(token) },
    ],
  });

  const fixture = TestBed.createComponent(ResetPasswordPage);
  await flush(fixture);

  return fixture;
}

function host(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function text(fixture: ComponentFixture<unknown>): string {
  return host(fixture).textContent ?? '';
}

function input(fixture: ComponentFixture<unknown>, id: string): HTMLInputElement {
  return host(fixture).querySelector<HTMLInputElement>(`#${id}`)!;
}

async function fill(
  fixture: ComponentFixture<ResetPasswordPage>,
  password: string,
  confirmation: string,
): Promise<void> {
  const passwordInput = input(fixture, 'new-password');
  passwordInput.value = password;
  passwordInput.dispatchEvent(new Event('input'));

  const confirmationInput = input(fixture, 'confirm-password');
  confirmationInput.value = confirmation;
  confirmationInput.dispatchEvent(new Event('input'));
}

async function submit(fixture: ComponentFixture<ResetPasswordPage>): Promise<void> {
  host(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));
  await flush(fixture);
}

describe('ResetPasswordPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('muestra un mensaje genérico cuando el enlace no trae token', async () => {
    const api = passwordApi();
    const fixture = await render(api, null);

    expect(text(fixture)).toContain('Este enlace ya no está disponible');
    expect(text(fixture)).toContain('Solicita recuperar tu acceso o contacta a administración');
    expect(text(fixture)).toContain('Tu acceso, en tus manos.');
    expect(host(fixture).querySelector('a[href="/recuperar-acceso"]')).not.toBeNull();
    expect(host(fixture).querySelector('a[href="/acceso"]')).not.toBeNull();
    expect(host(fixture).querySelector('form')).toBeNull();
    expect(api.resetPassword).not.toHaveBeenCalled();
  });

  it('presenta el formulario dentro del relato de acceso exclusivo', async () => {
    const fixture = await render(passwordApi());

    expect(text(fixture)).toContain('Tu acceso, en tus manos.');
    expect(text(fixture)).toContain('Acceso exclusivo para el personal de MouseDent');
    expect(host(fixture).querySelector('main form')).not.toBeNull();
  });

  it('marca los requisitos de la política mientras se escribe', async () => {
    const fixture = await render(passwordApi());

    await fill(fixture, '', '');
    expect(host(fixture).querySelectorAll('#password-requirements li.text-success').length).toBe(0);

    await fill(fixture, 'Demo123!', '');
    await flush(fixture);

    expect(host(fixture).querySelectorAll('#password-requirements li.text-success').length).toBe(5);
  });

  it('no envía la contraseña si no cumple los requisitos o no coincide', async () => {
    const api = passwordApi();
    const fixture = await render(api);

    await fill(fixture, 'corta', 'corta');
    await submit(fixture);

    expect(text(fixture)).toContain('La contraseña debe cumplir todos los requisitos.');
    expect(api.resetPassword).not.toHaveBeenCalled();

    await fill(fixture, 'Demo123!', 'Demo1234!');
    await submit(fixture);

    expect(text(fixture)).toContain('Las contraseñas no coinciden.');
    expect(api.resetPassword).not.toHaveBeenCalled();
  });

  it('establece la contraseña con el token del enlace', async () => {
    const api = passwordApi();
    const fixture = await render(api, 'token-vigente');

    await fill(fixture, 'Demo123!', 'Demo123!');
    await submit(fixture);

    expect(api.resetPassword).toHaveBeenCalledWith('token-vigente', 'Demo123!');
    expect(text(fixture)).toContain('Contraseña establecida');
    expect(text(fixture)).toContain('Las sesiones anteriores dejarán de servir');
  });

  it('muestra la política del servidor y conserva la contraseña', async () => {
    const fixture = await render(
      passwordApi({
        resetPassword: vi.fn(async () => ({
          ok: false as const,
          failure: 'password' as const,
          passwordProblems: ['La contraseña debe usar al menos 4 caracteres distintos.'],
        })),
      }),
    );

    await fill(fixture, 'Aa1!Aa1!', 'Aa1!Aa1!');
    await submit(fixture);

    expect(text(fixture)).toContain('La contraseña debe usar al menos 4 caracteres distintos.');
    expect(input(fixture, 'new-password').value).toBe('Aa1!Aa1!');
    expect(text(fixture)).not.toContain('Contraseña establecida');
  });

  it('cambia a enlace no disponible cuando el servidor lo rechaza', async () => {
    const fixture = await render(
      passwordApi({
        resetPassword: vi.fn(async () => ({
          ok: false as const,
          failure: 'invalid-link' as const,
        })),
      }),
    );

    await fill(fixture, 'Demo123!', 'Demo123!');
    await submit(fixture);

    expect(text(fixture)).toContain('Este enlace ya no está disponible');
    expect(text(fixture)).not.toContain('Contraseña establecida');
  });

  it('no da por guardada la contraseña si la API no responde', async () => {
    const fixture = await render(
      passwordApi({
        resetPassword: vi.fn(async () => ({
          ok: false as const,
          failure: 'unavailable' as const,
        })),
      }),
    );

    await fill(fixture, 'Demo123!', 'Demo123!');
    await submit(fixture);

    expect(host(fixture).querySelector('[role="alert"]')?.textContent).toContain(
      'No pudimos guardar la contraseña',
    );
    expect(input(fixture, 'new-password').value).toBe('Demo123!');
  });
});
