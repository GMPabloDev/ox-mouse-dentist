import { GeneratedPasswordApi } from './generated-password-api';

const sdk = vi.hoisted(() => ({
  requestPasswordRecovery: vi.fn(),
  resetPassword: vi.fn(),
}));

vi.mock('@mousedent/api-client', () => sdk);

function response(status: number): Response {
  return { status } as Response;
}

describe('respuestas públicas de contraseña', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('da la solicitud de recuperación por aceptada sin mirar si la cuenta existe', async () => {
    sdk.requestPasswordRecovery.mockResolvedValueOnce({ response: response(202) });

    await expect(
      new GeneratedPasswordApi().requestRecovery('persona@example.test'),
    ).resolves.toEqual({ ok: true });
    expect(sdk.requestPasswordRecovery).toHaveBeenCalledWith({
      body: { email: 'persona@example.test' },
    });
  });

  it('distingue el límite de solicitudes del correo inválido y de la API caída', async () => {
    const api = new GeneratedPasswordApi();

    sdk.requestPasswordRecovery.mockResolvedValueOnce({ error: {}, response: response(429) });
    await expect(api.requestRecovery('persona@example.test')).resolves.toEqual({
      ok: false,
      failure: 'rate-limit',
    });

    sdk.requestPasswordRecovery.mockResolvedValueOnce({
      error: { errors: { Email: ['Indica un correo válido.'] } },
      response: response(400),
    });
    await expect(api.requestRecovery('persona@example.test')).resolves.toEqual({
      ok: false,
      failure: 'invalid-email',
      message: 'Indica un correo válido.',
    });

    sdk.requestPasswordRecovery.mockRejectedValueOnce(new Error('sin red'));
    await expect(api.requestRecovery('persona@example.test')).resolves.toEqual({
      ok: false,
      failure: 'unavailable',
    });
  });

  it('establece la contraseña con el token y acepta la respuesta sin contenido', async () => {
    sdk.resetPassword.mockResolvedValueOnce({ response: response(204) });

    await expect(new GeneratedPasswordApi().resetPassword('token-1', 'Demo123!')).resolves.toEqual({
      ok: true,
    });
    expect(sdk.resetPassword).toHaveBeenCalledWith({
      body: { token: 'token-1', password: 'Demo123!' },
    });
  });

  it('separa la política de contraseña de un enlace inservible', async () => {
    const api = new GeneratedPasswordApi();

    sdk.resetPassword.mockResolvedValueOnce({
      error: { errors: { Password: ['La contraseña debe incluir al menos un número.'] } },
      response: response(400),
    });
    await expect(api.resetPassword('token-1', 'sin-numero')).resolves.toEqual({
      ok: false,
      failure: 'password',
      passwordProblems: ['La contraseña debe incluir al menos un número.'],
    });

    sdk.resetPassword.mockResolvedValueOnce({
      error: {
        title: 'Enlace no disponible',
        detail: 'Este enlace ya no está disponible. Solicita recuperar tu acceso.',
      },
      response: response(400),
    });
    await expect(api.resetPassword('token-usado', 'Demo123!')).resolves.toEqual({
      ok: false,
      failure: 'invalid-link',
      message: 'Este enlace ya no está disponible. Solicita recuperar tu acceso.',
    });
  });

  it('no presenta como exitoso un fallo de servidor', async () => {
    sdk.resetPassword.mockResolvedValueOnce({ error: {}, response: response(500) });

    await expect(new GeneratedPasswordApi().resetPassword('token-1', 'Demo123!')).resolves.toEqual({
      ok: false,
      failure: 'unavailable',
    });
  });
});
