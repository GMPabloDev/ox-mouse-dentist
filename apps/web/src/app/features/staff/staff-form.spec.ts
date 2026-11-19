import type { StaffAccountResponse } from '@mousedent/api-client';
import { profileLabel } from '../../core/auth/session';
import {
  EMPTY_DRAFT,
  accessSummary,
  draftFrom,
  hasActiveLocal,
  isDirty,
  problemsFromFailure,
  statusLabel,
  toForm,
  validateDraft,
  type StaffDraft,
  type StaffLocal,
} from './staff-form';

const LOCALS: readonly StaffLocal[] = [
  { id: 'local-miraflores', name: 'Miraflores', isActive: true },
  { id: 'local-barranco', name: 'Barranco', isActive: false },
];

function draft(overrides: Partial<StaffDraft> = {}): StaffDraft {
  return {
    ...EMPTY_DRAFT,
    displayName: 'Camila Mendoza',
    email: 'camila.mendoza@example.test',
    profile: 'Recepcion',
    localIds: ['local-miraflores'],
    ...overrides,
  };
}

function account(overrides: Partial<StaffAccountResponse> = {}): StaffAccountResponse {
  return {
    id: 'cuenta-1',
    displayName: 'Valeria Torres',
    email: 'valeria.torres@example.test',
    phone: '987 123 456',
    profile: 'Recepcion',
    status: 'Activa',
    assignedLocalIds: ['local-miraflores'],
    ...overrides,
  };
}

describe('modelo del formulario de personal', () => {
  it('acepta un alta de recepción con un local activo y teléfono opcional', () => {
    expect(validateDraft(draft(), LOCALS)).toEqual({});
  });

  it('exige nombre y correo válidos y conserva el teléfono como opcional', () => {
    const problems = validateDraft(draft({ displayName: '  ', email: 'sin-arroba' }), LOCALS);

    expect(problems.displayName).toBe('Escribe el nombre de la persona.');
    expect(problems.email).toBe('Escribe un correo electrónico válido.');
    expect(problems.phone).toBeUndefined();
  });

  it('limita la longitud del nombre, el correo y el teléfono', () => {
    const problems = validateDraft(
      draft({
        displayName: 'a'.repeat(121),
        email: `${'a'.repeat(250)}@example.test`,
        phone: '9'.repeat(41),
      }),
      LOCALS,
    );

    expect(problems.displayName).toContain('120');
    expect(problems.email).toContain('256');
    expect(problems.phone).toContain('40');
  });

  it('no permite crear recepción sin locales activos', () => {
    expect(validateDraft(draft({ localIds: [] }), LOCALS).localIds).toBe(
      'Selecciona al menos un local activo.',
    );
    expect(validateDraft(draft({ localIds: ['local-barranco'] }), LOCALS).localIds).toBe(
      'Solo puedes asignar locales activos.',
    );
  });

  it('conserva una asignación a un local inactivo al editar otros datos', () => {
    const editing = draft({
      id: 'cuenta-2',
      localIds: ['local-barranco'],
      originalLocalIds: ['local-barranco'],
    });

    expect(validateDraft(editing, LOCALS)).toEqual({});
  });

  it('impide quitar todas las asignaciones de una cuenta de recepción existente', () => {
    const editing = draft({
      id: 'cuenta-2',
      localIds: [],
      originalLocalIds: ['local-barranco'],
    });

    expect(validateDraft(editing, LOCALS).localIds).toBe('Selecciona al menos un local activo.');
  });

  it('no exige locales a administración ni a profesionales', () => {
    expect(validateDraft(draft({ profile: 'Administrador', localIds: [] }), LOCALS)).toEqual({});
    expect(validateDraft(draft({ profile: 'Profesional', localIds: [] }), LOCALS)).toEqual({});
  });

  it('arma el cuerpo sin espacios y sin asignaciones para los perfiles que no son recepción', () => {
    expect(
      toForm(
        draft({
          displayName: '  Rosa Castillo ',
          email: ' rosa@example.test ',
          phone: '   ',
          profile: 'Profesional',
          localIds: ['local-miraflores'],
        }),
      ),
    ).toEqual({
      displayName: 'Rosa Castillo',
      email: 'rosa@example.test',
      phone: null,
      profile: 'Profesional',
      assignedLocalIds: [],
    });
  });

  it('recupera el borrador y detecta cambios', () => {
    const original = draftFrom(
      account({ assignedLocalIds: ['local-miraflores', 'local-barranco'] }),
    );

    expect(original.originalLocalIds).toEqual(['local-miraflores', 'local-barranco']);
    expect(isDirty(original, original)).toBe(false);
    expect(isDirty({ ...original, phone: '999 999 999' }, original)).toBe(true);
  });

  it('traduce los fallos del servidor a campos del formulario', () => {
    expect(
      problemsFromFailure({
        kind: 'validation',
        errors: { DisplayName: ['El nombre es obligatorio.'], AssignedLocalIds: ['Solo activos.'] },
      }),
    ).toEqual({ displayName: 'El nombre es obligatorio.', localIds: 'Solo activos.' });

    expect(
      problemsFromFailure({
        kind: 'duplicate-email',
        detail: 'Ese correo ya pertenece a otra cuenta.',
      }),
    ).toEqual({ email: 'Ese correo ya pertenece a otra cuenta.' });

    expect(problemsFromFailure({ kind: 'last-administrator' }).form).toContain(
      'sin administración disponible',
    );
    expect(problemsFromFailure({ kind: 'unavailable' }).form).toContain('conexión');
    expect(problemsFromFailure({ kind: 'validation', errors: {} }).form).toBe(
      'Revisa los datos de la cuenta.',
    );
  });

  it('resume el acceso por local de cada perfil', () => {
    const locals = new Map(LOCALS.map((local) => [local.id, local]));

    expect(accessSummary(account({ profile: 'Administrador' }), locals)).toBe('Todos los locales');
    expect(accessSummary(account({ profile: 'Profesional' }), locals)).toBe(
      'Sin locales de atención',
    );
    expect(accessSummary(account(), locals)).toBe('Miraflores');
    expect(
      accessSummary(account({ assignedLocalIds: ['local-miraflores', 'local-barranco'] }), locals),
    ).toBe('Miraflores · Barranco');
    expect(accessSummary(account({ assignedLocalIds: ['local-barranco'] }), locals)).toBe(
      'Sin locales activos',
    );
    expect(hasActiveLocal(account({ assignedLocalIds: ['local-barranco'] }), locals)).toBe(false);
    expect(hasActiveLocal(account(), locals)).toBe(true);
  });

  it('usa los nombres visibles del dominio', () => {
    expect(profileLabel('Administrador')).toBe('Administración');
    expect(profileLabel('Recepcion')).toBe('Recepción');
    expect(profileLabel('Profesional')).toBe('Profesional');
    expect(statusLabel('Pendiente')).toBe('Pendiente');
  });
});
