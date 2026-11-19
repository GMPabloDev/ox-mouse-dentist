import type { StaffAccountResponse } from '@mousedent/api-client';
import { GeneratedStaffApi, classify } from './generated-staff-api';

const sdk = vi.hoisted(() => ({
  listStaffAccounts: vi.fn(),
  createStaffAccount: vi.fn(),
  updateStaffAccount: vi.fn(),
  setStaffAccountStatus: vi.fn(),
  resendStaffInvitation: vi.fn(),
}));

vi.mock('@mousedent/api-client', () => sdk);

const ACCOUNT: StaffAccountResponse = {
  id: '6f1b1f2e-0000-4000-8000-000000000001',
  displayName: 'Camila Mendoza',
  email: 'camila.mendoza@example.test',
  phone: null,
  profile: 'Recepcion',
  status: 'Pendiente',
  assignedLocalIds: ['local-miraflores'],
};

function response(status: number): Response {
  return { status } as Response;
}

describe('respuestas de la API de personal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('traduce los errores de validación por campo', () => {
    expect(
      classify(400, {
        title: 'One or more validation errors occurred.',
        errors: { Email: ['Indica un correo válido.'] },
      }),
    ).toEqual({ kind: 'validation', errors: { Email: ['Indica un correo válido.'] } });
  });

  it('distingue el correo repetido de la protección del último administrador', () => {
    expect(
      classify(409, {
        title: 'Correo ya registrado',
        detail: 'Ese correo ya pertenece a otra cuenta.',
      }),
    ).toEqual({ kind: 'duplicate-email', detail: 'Ese correo ya pertenece a otra cuenta.' });

    expect(
      classify(409, {
        title: 'Último administrador disponible',
        detail: 'La operación dejaría el sistema sin un administrador activo.',
      }),
    ).toEqual({
      kind: 'last-administrator',
      detail: 'La operación dejaría el sistema sin un administrador activo.',
    });

    expect(classify(409, { title: 'Cuenta modificada', detail: 'La cuenta cambió.' })).toEqual({
      kind: 'concurrent-change',
      detail: 'La cuenta cambió.',
    });

    expect(classify(409, { title: 'Cuenta inactiva' })).toEqual({
      kind: 'conflict',
      detail: undefined,
    });
  });

  it('conserva la cuenta y el detalle cuando el correo no se pudo enviar', () => {
    expect(
      classify(502, {
        title: 'Invitación sin enviar',
        detail: 'La cuenta se guardó, pero el correo no se pudo enviar.',
        accountId: ACCOUNT.id,
      }),
    ).toEqual({
      kind: 'invitation-failed',
      accountId: ACCOUNT.id,
      detail: 'La cuenta se guardó, pero el correo no se pudo enviar.',
    });

    expect(classify(502, 'sin cuerpo')).toEqual({
      kind: 'invitation-failed',
      accountId: undefined,
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

  it('lista las cuentas y traduce un fallo de red', async () => {
    sdk.listStaffAccounts.mockResolvedValueOnce({ data: [ACCOUNT] });
    const api = new GeneratedStaffApi();

    await expect(api.list()).resolves.toEqual({ ok: true, value: [ACCOUNT] });

    sdk.listStaffAccounts.mockRejectedValueOnce(new Error('sin red'));
    await expect(api.list()).resolves.toEqual({ ok: false, failure: { kind: 'unavailable' } });

    sdk.listStaffAccounts.mockResolvedValueOnce({ error: {}, response: response(401) });
    await expect(api.list()).resolves.toEqual({ ok: false, failure: { kind: 'unauthorized' } });
  });

  it('envía el alta y devuelve el fallo de invitación con su identificador', async () => {
    sdk.createStaffAccount.mockResolvedValueOnce({ data: ACCOUNT });
    const api = new GeneratedStaffApi();

    await expect(
      api.create({ displayName: 'Camila', email: 'camila@example.test', profile: 'Recepcion' }),
    ).resolves.toEqual({ ok: true, value: ACCOUNT });

    sdk.createStaffAccount.mockResolvedValueOnce({
      error: { title: 'Invitación sin enviar', accountId: ACCOUNT.id },
      response: response(502),
    });
    const failure = await api.create({
      displayName: 'Camila',
      email: 'camila@example.test',
      profile: 'Recepcion',
    });

    expect(failure).toEqual({
      ok: false,
      failure: { kind: 'invitation-failed', accountId: ACCOUNT.id, detail: undefined },
    });
    expect(sdk.createStaffAccount).toHaveBeenCalledWith({
      body: { displayName: 'Camila', email: 'camila@example.test', profile: 'Recepcion' },
    });
  });

  it('cambia el estado y reenvía la invitación con el identificador de la cuenta', async () => {
    sdk.setStaffAccountStatus.mockResolvedValueOnce({ data: { ...ACCOUNT, status: 'Inactiva' } });
    sdk.resendStaffInvitation.mockResolvedValueOnce({ data: ACCOUNT });
    const api = new GeneratedStaffApi();

    const changed = await api.setStatus(ACCOUNT.id, false);
    expect(changed).toEqual({ ok: true, value: { ...ACCOUNT, status: 'Inactiva' } });
    expect(sdk.setStaffAccountStatus).toHaveBeenCalledWith({
      path: { id: ACCOUNT.id },
      body: { isActive: false },
    });

    await expect(api.resendInvitation(ACCOUNT.id)).resolves.toEqual({ ok: true, value: ACCOUNT });
    expect(sdk.resendStaffInvitation).toHaveBeenCalledWith({ path: { id: ACCOUNT.id } });
  });
});
