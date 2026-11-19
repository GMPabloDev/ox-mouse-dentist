import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PASSWORD_API, type PasswordApi } from '../../core/auth/password-api';
import { RecoveryPage } from './recovery-page';

function passwordApi(overrides: Partial<PasswordApi> = {}): PasswordApi {
  return {
    requestRecovery: vi.fn(async () => ({ ok: true as const })),
    resetPassword: vi.fn(async () => ({ ok: true as const })),
    ...overrides,
  };
}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

async function render(api: PasswordApi): Promise<ComponentFixture<RecoveryPage>> {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: PASSWORD_API, useValue: api }],
  });

  const fixture = TestBed.createComponent(RecoveryPage);
  await flush(fixture);

  return fixture;
}

function host(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function text(fixture: ComponentFixture<unknown>): string {
  return host(fixture).textContent ?? '';
}

function emailInput(fixture: ComponentFixture<unknown>): HTMLInputElement {
  return host(fixture).querySelector<HTMLInputElement>('#recovery-email')!;
}

async function submitEmail(fixture: ComponentFixture<RecoveryPage>, email: string): Promise<void> {
  const input = emailInput(fixture);
  input.value = email;
  input.dispatchEvent(new Event('input'));
  host(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));
  await flush(fixture);
}

describe('RecoveryPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('pide el enlace y confirma de forma genérica sin revelar la cuenta', async () => {
    const api = passwordApi();
    const fixture = await render(api);

    await submitEmail(fixture, 'persona@example.test');

    expect(api.requestRecovery).toHaveBeenCalledWith('persona@example.test');
    expect(text(fixture)).toContain('Revisa tu correo');
    expect(text(fixture)).toContain('Si la cuenta puede recuperar el acceso');
    expect(text(fixture)).toContain('Por seguridad, no confirmamos si una cuenta existe');
    expect(text(fixture)).not.toContain('persona@example.test');
    expect(host(fixture).querySelector('form')).toBeNull();
  });

  it('mantiene el relato de acceso exclusivo en la solicitud y en su confirmación', async () => {
    const fixture = await render(passwordApi());

    expect(text(fixture)).toContain('Volvamos a tu cuenta.');
    expect(text(fixture)).toContain('Acceso exclusivo para el personal de MouseDent');

    await submitEmail(fixture, 'persona@example.test');

    expect(text(fixture)).toContain('Revisa tu correo');
    expect(text(fixture)).toContain('Volvamos a tu cuenta.');
  });

  it('explica el límite de solicitudes y conserva el correo para reintentar', async () => {
    const fixture = await render(
      passwordApi({
        requestRecovery: vi.fn(async () => ({
          ok: false as const,
          failure: 'rate-limit' as const,
        })),
      }),
    );

    await submitEmail(fixture, 'persona@example.test');

    expect(host(fixture).querySelector('[role="alert"]')?.textContent).toContain(
      'Has realizado varias solicitudes',
    );
    expect(emailInput(fixture).value).toBe('persona@example.test');
    expect(text(fixture)).not.toContain('Revisa tu correo');
  });

  it('no presenta un fallo de conexión como solicitud exitosa', async () => {
    const fixture = await render(
      passwordApi({
        requestRecovery: vi.fn(async () => ({
          ok: false as const,
          failure: 'unavailable' as const,
        })),
      }),
    );

    await submitEmail(fixture, 'persona@example.test');

    expect(host(fixture).querySelector('[role="alert"]')?.textContent).toContain(
      'No pudimos conectar',
    );
    expect(text(fixture)).not.toContain('Revisa tu correo');
    expect(emailInput(fixture).value).toBe('persona@example.test');
  });

  it('no llama a la API con un correo inválido', async () => {
    const api = passwordApi();
    const fixture = await render(api);

    await submitEmail(fixture, 'sin-arroba');

    expect(api.requestRecovery).not.toHaveBeenCalled();
    expect(text(fixture)).toContain('Escribe un correo electrónico válido.');
  });
});
