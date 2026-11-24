import type { ProfesionalResponse } from '@mousedent/api-client';
import type { Assignment } from './profesional-list';
import {
  canAssignEspecialidad,
  canAssignLocal,
  COLEGIATURA_MAX_LENGTH,
  draftFrom,
  EMPTY_DRAFT,
  isDirty,
  problemsFromFailure,
  toCreateForm,
  toUpdateForm,
  validateDraft,
  type ProfesionalDraft,
} from './profesional-form';

const ESPECIALIDADES: readonly Assignment[] = [
  { id: 'general', name: 'Odontología general', isActive: true },
  { id: 'orto', name: 'Ortodoncia', isActive: true },
  { id: 'perio', name: 'Periodoncia', isActive: false },
];

const LOCALES: readonly Assignment[] = [
  { id: 'miraflores', name: 'Miraflores', isActive: true },
  { id: 'surco', name: 'Surco', isActive: false },
];

function profesional(overrides: Partial<ProfesionalResponse> = {}): ProfesionalResponse {
  return {
    id: 'ficha-camila',
    staffAccountId: 'cuenta-camila',
    displayName: 'Camila Mendoza',
    email: 'camila.mendoza@example.test',
    phone: '987 345 678',
    colegiatura: 'COP 12345',
    isActive: true,
    accountStatus: 'Pendiente',
    invitationFailed: false,
    especialidades: [{ id: 'general', name: 'Odontología general', isActive: true }],
    locales: [{ id: 'miraflores', name: 'Miraflores', isActive: true }],
    ...overrides,
  };
}

function draft(overrides: Partial<ProfesionalDraft> = {}): ProfesionalDraft {
  return {
    ...EMPTY_DRAFT,
    displayName: 'Lucía Rojas',
    email: 'lucia.rojas@example.test',
    especialidadIds: ['general'],
    localIds: ['miraflores'],
    ...overrides,
  };
}

describe('modelo del formulario de profesionales', () => {
  it('acepta un alta con una especialidad y un local activos', () => {
    expect(validateDraft(draft(), ESPECIALIDADES, LOCALES)).toEqual({});
  });

  it('exige nombre y correo válidos en el alta', () => {
    const problems = validateDraft(
      draft({ displayName: '  ', email: 'sin-arroba' }),
      ESPECIALIDADES,
      LOCALES,
    );

    expect(problems.displayName).toBe('Escribe el nombre completo.');
    expect(problems.email).toBe('Escribe un correo electrónico válido.');
    expect(validateDraft(draft({ email: '' }), ESPECIALIDADES, LOCALES).email).toBe(
      'Escribe el correo de la persona.',
    );
  });

  it('limita el nombre y el correo a la longitud del contrato', () => {
    const problems = validateDraft(
      draft({ displayName: 'a'.repeat(121), email: `${'a'.repeat(250)}@example.test` }),
      ESPECIALIDADES,
      LOCALES,
    );

    expect(problems.displayName).toContain('120');
    expect(problems.email).toContain('256');
  });

  it('admite la colegiatura vacía y rechaza más de 30 caracteres tras quitar espacios', () => {
    expect(validateDraft(draft({ colegiatura: '   ' }), ESPECIALIDADES, LOCALES)).toEqual({});
    expect(
      validateDraft(
        draft({ colegiatura: 'x'.repeat(COLEGIATURA_MAX_LENGTH) }),
        ESPECIALIDADES,
        LOCALES,
      ),
    ).toEqual({});
    expect(
      validateDraft(
        draft({ colegiatura: ` ${'x'.repeat(COLEGIATURA_MAX_LENGTH)} ` }),
        ESPECIALIDADES,
        LOCALES,
      ),
    ).toEqual({});
    expect(
      validateDraft(
        draft({ colegiatura: 'x'.repeat(COLEGIATURA_MAX_LENGTH + 1) }),
        ESPECIALIDADES,
        LOCALES,
      ).colegiatura,
    ).toContain('30');
  });

  it('exige una especialidad y un local activos al crear', () => {
    expect(
      validateDraft(draft({ especialidadIds: [] }), ESPECIALIDADES, LOCALES).especialidadIds,
    ).toBe('Selecciona al menos una especialidad activa.');
    expect(validateDraft(draft({ localIds: [] }), ESPECIALIDADES, LOCALES).localIds).toBe(
      'Selecciona al menos un local activo.',
    );
  });

  it('no permite añadir una especialidad o un local inactivos', () => {
    expect(
      validateDraft(draft({ especialidadIds: ['general', 'perio'] }), ESPECIALIDADES, LOCALES)
        .especialidadIds,
    ).toBe('Solo puedes asignar especialidades activas.');
    expect(
      validateDraft(draft({ localIds: ['miraflores', 'surco'] }), ESPECIALIDADES, LOCALES).localIds,
    ).toBe('Solo puedes asignar locales activos.');
  });

  it('conserva las asignaciones inactivas ya guardadas al editar', () => {
    const editing = draft({
      id: 'ficha-lucia',
      isActive: false,
      especialidadIds: ['perio'],
      localIds: ['surco'],
      originalEspecialidadIds: ['perio'],
      originalLocalIds: ['surco'],
    });

    expect(validateDraft(editing, ESPECIALIDADES, LOCALES)).toEqual({});
    expect(canAssignEspecialidad(editing, ESPECIALIDADES[2]!)).toBe(true);
    expect(canAssignLocal(editing, LOCALES[1]!)).toBe(true);
    expect(canAssignEspecialidad(editing, ESPECIALIDADES[0]!)).toBe(true);
  });

  it('impide que una ficha activa se quede sin una especialidad o un local activos', () => {
    const editing = draft({
      id: 'ficha-camila',
      especialidadIds: ['perio'],
      localIds: ['miraflores'],
      originalEspecialidadIds: ['general', 'perio'],
      originalLocalIds: ['miraflores'],
    });

    expect(validateDraft(editing, ESPECIALIDADES, LOCALES).especialidadIds).toBe(
      'Selecciona al menos una especialidad activa.',
    );
  });

  it('recupera el borrador de la ficha y detecta cambios', () => {
    const original = draftFrom(profesional());

    expect(original).toEqual({
      id: 'ficha-camila',
      displayName: 'Camila Mendoza',
      email: 'camila.mendoza@example.test',
      phone: '987 345 678',
      colegiatura: 'COP 12345',
      especialidadIds: ['general'],
      localIds: ['miraflores'],
      originalEspecialidadIds: ['general'],
      originalLocalIds: ['miraflores'],
      isActive: true,
      accountStatus: 'Pendiente',
      invitationFailed: false,
    });
    expect(isDirty(original, original)).toBe(false);
    expect(isDirty({ ...original, colegiatura: 'COP 99999' }, original)).toBe(true);
    expect(isDirty({ ...original, localIds: [] }, original)).toBe(true);
    expect(isDirty({ ...original, phone: '999 999 999' }, original)).toBe(false);
  });

  it('arma los cuerpos de alta y edición con los espacios exteriores quitados', () => {
    expect(
      toCreateForm(
        draft({
          displayName: '  Lucía Rojas ',
          email: ' lucia.rojas@example.test ',
          colegiatura: '  ',
        }),
      ),
    ).toEqual({
      displayName: 'Lucía Rojas',
      email: 'lucia.rojas@example.test',
      colegiatura: null,
      especialidadIds: ['general'],
      localIds: ['miraflores'],
    });

    expect(
      toUpdateForm(
        draft({
          id: 'ficha-camila',
          displayName: 'Otro nombre',
          email: 'otro@example.test',
          colegiatura: ' COP 00001 ',
        }),
      ),
    ).toEqual({
      colegiatura: 'COP 00001',
      especialidadIds: ['general'],
      localIds: ['miraflores'],
    });
  });

  it('traduce los fallos del servidor al campo que administración corrige', () => {
    expect(
      problemsFromFailure({
        kind: 'validation',
        errors: {
          Email: ['Indica un correo válido.'],
          Colegiatura: ['El número de colegiatura no puede superar los 30 caracteres.'],
          EspecialidadIds: ['La ficha activa necesita al menos una especialidad activa.'],
          LocalIds: ['La ficha activa necesita al menos un local de atención activo.'],
        },
      }),
    ).toEqual({
      email: 'Indica un correo válido.',
      colegiatura: 'El número de colegiatura no puede superar los 30 caracteres.',
      especialidadIds: 'La ficha activa necesita al menos una especialidad activa.',
      localIds: 'La ficha activa necesita al menos un local de atención activo.',
    });

    expect(
      problemsFromFailure({
        kind: 'duplicate-colegiatura',
        detail: 'Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.',
      }),
    ).toEqual({
      colegiatura: 'Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.',
    });

    expect(
      problemsFromFailure({ kind: 'email-taken', detail: 'Ese correo pertenece a una ficha.' })
        .email,
    ).toBe('Ese correo pertenece a una ficha.');
    expect(
      problemsFromFailure({ kind: 'other-profile', detail: 'Ese correo es de administración.' })
        .email,
    ).toBe('Ese correo es de administración.');
    expect(
      problemsFromFailure({ kind: 'activation-incomplete', detail: 'Falta una especialidad.' })
        .form,
    ).toBe('Falta una especialidad.');
    expect(problemsFromFailure({ kind: 'not-found' }).form).toContain('ya no existe');
    expect(problemsFromFailure({ kind: 'forbidden' }).form).toContain('Tu perfil');
    expect(problemsFromFailure({ kind: 'unauthorized' }).form).toContain('sesión');
    expect(problemsFromFailure({ kind: 'unavailable' }).form).toContain('conexión');
    expect(problemsFromFailure({ kind: 'validation', errors: {} }).form).toBe(
      'Revisa los datos de la ficha.',
    );
  });
});
