import type { ProfesionalResponse } from '@mousedent/api-client';
import { GeneratedProfesionalesApi, classify } from './generated-profesionales-api';

const sdk = vi.hoisted(() => ({
  listProfesionales: vi.fn(),
  createProfesional: vi.fn(),
  updateProfesional: vi.fn(),
  setProfesionalStatus: vi.fn(),
  resendProfesionalInvitation: vi.fn(),
}));

vi.mock('@mousedent/api-client', () => sdk);

const PROFESIONAL: ProfesionalResponse = {
  id: '6f1b1f2e-0000-4000-8000-000000000001',
  staffAccountId: '6f1b1f2e-0000-4000-8000-000000000002',
  displayName: 'Camila Mendoza',
  email: 'camila.mendoza@example.test',
  phone: null,
  colegiatura: null,
  isActive: true,
  accountStatus: 'Pendiente',
  invitationFailed: false,
  especialidades: [{ id: 'general', name: 'Odontología general', isActive: true }],
  locales: [{ id: 'miraflores', name: 'Miraflores', isActive: true }],
};

function response(status: number): Response {
  return { status } as Response;
}

describe('respuestas de la API de profesionales', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('traduce los errores de validación por campo', () => {
    expect(
      classify(400, {
        title: 'One or more validation errors occurred.',
        errors: { Colegiatura: ['El número de colegiatura no puede superar los 30 caracteres.'] },
      }),
    ).toEqual({
      kind: 'validation',
      errors: { Colegiatura: ['El número de colegiatura no puede superar los 30 caracteres.'] },
    });
  });

  it('separa los conflictos que corrigen un campo distinto', () => {
    expect(
      classify(409, {
        title: 'Colegiatura duplicada',
        detail: 'Otra ficha ya tiene ese número.',
      }),
    ).toEqual({ kind: 'duplicate-colegiatura', detail: 'Otra ficha ya tiene ese número.' });

    expect(
      classify(409, { title: 'Correo con ficha', detail: 'Ese correo ya tiene una ficha.' }),
    ).toEqual({ kind: 'email-taken', detail: 'Ese correo ya tiene una ficha.' });

    expect(
      classify(409, { title: 'Correo de otra cuenta', detail: 'Es una cuenta de recepción.' }),
    ).toEqual({ kind: 'other-profile', detail: 'Es una cuenta de recepción.' });

    expect(
      classify(409, { title: 'Activación incompleta', detail: 'Falta un local activo.' }),
    ).toEqual({ kind: 'activation-incomplete', detail: 'Falta un local activo.' });

    expect(
      classify(409, { title: 'Cuenta inactiva', detail: 'Reactiva la cuenta en personal.' }),
    ).toEqual({ kind: 'conflict', detail: 'Reactiva la cuenta en personal.' });

    expect(classify(409, { title: 'Contraseña ya establecida' })).toEqual({
      kind: 'conflict',
      detail: undefined,
    });
  });

  it('conserva la ficha y el detalle cuando el correo no se pudo enviar', () => {
    expect(
      classify(502, {
        title: 'Invitación sin enviar',
        detail: 'La ficha quedó guardada, pero el correo no se pudo enviar.',
        profesionalId: PROFESIONAL.id,
      }),
    ).toEqual({
      kind: 'invitation-failed',
      profesionalId: PROFESIONAL.id,
      detail: 'La ficha quedó guardada, pero el correo no se pudo enviar.',
    });

    expect(classify(502, 'sin cuerpo')).toEqual({
      kind: 'invitation-failed',
      profesionalId: undefined,
      detail: undefined,
    });
  });

  it('separa la sesión revocada del perfil sin permiso y de la API caída', () => {
    expect(classify(401, {})).toEqual({ kind: 'unauthorized' });
    expect(classify(403, {})).toEqual({ kind: 'forbidden' });
    expect(classify(404, {})).toEqual({ kind: 'not-found' });
    expect(classify(500, {})).toEqual({ kind: 'unavailable' });
    expect(classify(undefined, undefined)).toEqual({ kind: 'unavailable' });
  });

  it('lista las fichas y traduce los fallos de red y de sesión', async () => {
    sdk.listProfesionales.mockResolvedValueOnce({ data: [PROFESIONAL] });
    const api = new GeneratedProfesionalesApi();

    await expect(api.list()).resolves.toEqual({ ok: true, value: [PROFESIONAL] });

    sdk.listProfesionales.mockRejectedValueOnce(new Error('sin red'));
    await expect(api.list()).resolves.toEqual({ ok: false, failure: { kind: 'unavailable' } });

    sdk.listProfesionales.mockResolvedValueOnce({ error: {}, response: response(401) });
    await expect(api.list()).resolves.toEqual({ ok: false, failure: { kind: 'unauthorized' } });
  });

  it('envía el alta y devuelve el fallo de invitación con el identificador de la ficha', async () => {
    sdk.createProfesional.mockResolvedValueOnce({ data: PROFESIONAL });
    const api = new GeneratedProfesionalesApi();
    const form = {
      displayName: 'Camila Mendoza',
      email: 'camila.mendoza@example.test',
      colegiatura: null,
      especialidadIds: ['general'],
      localIds: ['miraflores'],
    };

    await expect(api.create(form)).resolves.toEqual({ ok: true, value: PROFESIONAL });
    expect(sdk.createProfesional).toHaveBeenCalledWith({ body: form });

    sdk.createProfesional.mockResolvedValueOnce({
      error: { title: 'Invitación sin enviar', profesionalId: PROFESIONAL.id },
      response: response(502),
    });
    const failure = await api.create(form);

    expect(failure).toEqual({
      ok: false,
      failure: { kind: 'invitation-failed', profesionalId: PROFESIONAL.id, detail: undefined },
    });
  });

  it('edita, cambia el estado y reenvía la invitación con el identificador de la ficha', async () => {
    sdk.updateProfesional.mockResolvedValueOnce({ data: PROFESIONAL });
    sdk.setProfesionalStatus.mockResolvedValueOnce({ data: { ...PROFESIONAL, isActive: false } });
    sdk.resendProfesionalInvitation.mockResolvedValueOnce({ data: PROFESIONAL });
    const api = new GeneratedProfesionalesApi();
    const form = {
      colegiatura: 'COP 12345',
      especialidadIds: ['general'],
      localIds: ['miraflores'],
    };

    await expect(api.update(PROFESIONAL.id, form)).resolves.toEqual({
      ok: true,
      value: PROFESIONAL,
    });
    expect(sdk.updateProfesional).toHaveBeenCalledWith({
      path: { id: PROFESIONAL.id },
      body: form,
    });

    await expect(api.setStatus(PROFESIONAL.id, false)).resolves.toEqual({
      ok: true,
      value: { ...PROFESIONAL, isActive: false },
    });
    expect(sdk.setProfesionalStatus).toHaveBeenCalledWith({
      path: { id: PROFESIONAL.id },
      body: { isActive: false },
    });

    await expect(api.resendInvitation(PROFESIONAL.id)).resolves.toEqual({
      ok: true,
      value: PROFESIONAL,
    });
    expect(sdk.resendProfesionalInvitation).toHaveBeenCalledWith({ path: { id: PROFESIONAL.id } });
  });
});
