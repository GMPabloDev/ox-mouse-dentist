import type { ProfesionalResponse } from '@mousedent/api-client';
import {
  accessChecks,
  accessSentence,
  activationMissing,
  activeAssignmentCount,
  accountStatusLabel,
  colegiaturaLabel,
  countProfesionales,
  EMPTY_FILTERS,
  filterProfesionales,
  hasFilters,
  homonyms,
  initialsOf,
  resultsLabel,
  type ProfesionalFilters,
} from './profesional-list';

function profesional(overrides: Partial<ProfesionalResponse> = {}): ProfesionalResponse {
  return {
    id: 'ficha-valeria',
    staffAccountId: 'cuenta-valeria',
    displayName: 'Valeria Torres',
    email: 'valeria.torres@example.test',
    phone: '987 123 456',
    colegiatura: 'COP 12345',
    isActive: true,
    accountStatus: 'Activa',
    invitationFailed: false,
    especialidades: [
      { id: 'general', name: 'Odontología general', isActive: true },
      { id: 'orto', name: 'Ortodoncia', isActive: true },
    ],
    locales: [
      { id: 'miraflores', name: 'Miraflores', isActive: true },
      { id: 'surco', name: 'Surco', isActive: false },
    ],
    ...overrides,
  };
}

const CAMILA = profesional({
  id: 'ficha-camila',
  displayName: 'Camila Mendoza',
  email: 'camila.mendoza@example.test',
  phone: null,
  colegiatura: null,
  accountStatus: 'Pendiente',
  invitationFailed: true,
  especialidades: [
    { id: 'orto', name: 'Ortodoncia', isActive: true },
    { id: 'perio', name: 'Periodoncia', isActive: false },
  ],
  locales: [{ id: 'miraflores', name: 'Miraflores', isActive: true }],
});

const LUCIA = profesional({
  id: 'ficha-lucia',
  displayName: 'Lucía Rojas',
  email: 'lucia.rojas@example.test',
  isActive: false,
  especialidades: [{ id: 'perio', name: 'Periodoncia', isActive: false }],
  locales: [{ id: 'surco', name: 'Surco', isActive: false }],
});

const MARTIN = profesional({
  id: 'ficha-martin',
  displayName: 'Martín Paredes',
  email: 'martin.paredes@example.test',
  accountStatus: 'Inactiva',
  especialidades: [{ id: 'endo', name: 'Endodoncia', isActive: true }],
  locales: [{ id: 'la-molina', name: 'La Molina', isActive: true }],
});

const VALERIA = profesional();
const ALL = [VALERIA, CAMILA, LUCIA, MARTIN];

function filters(overrides: Partial<ProfesionalFilters> = {}): ProfesionalFilters {
  return { ...EMPTY_FILTERS, ...overrides };
}

describe('lista de profesionales', () => {
  it('reconoce cuándo hay búsqueda o algún filtro activo', () => {
    expect(hasFilters(filters())).toBe(false);
    expect(hasFilters(filters({ search: '  ' }))).toBe(false);
    expect(hasFilters(filters({ search: 'valeria' }))).toBe(true);
    expect(hasFilters(filters({ especialidadId: 'orto' }))).toBe(true);
    expect(hasFilters(filters({ localId: 'surco' }))).toBe(true);
    expect(hasFilters(filters({ ficha: 'inactive' }))).toBe(true);
    expect(hasFilters(filters({ account: 'Pendiente' }))).toBe(true);
  });

  it('busca por nombre sin distinguir tildes ni mayúsculas y ordena por nombre', () => {
    expect(filterProfesionales(ALL, filters({ search: 'LUCÍA' }))).toEqual([LUCIA]);
    expect(filterProfesionales(ALL, filters({ search: 'torres' }))).toEqual([VALERIA]);
    expect(filterProfesionales(ALL, filters()).map((item) => item.displayName)).toEqual([
      'Camila Mendoza',
      'Lucía Rojas',
      'Martín Paredes',
      'Valeria Torres',
    ]);
  });

  it('busca por correo aunque el nombre no coincida', () => {
    expect(filterProfesionales(ALL, filters({ search: 'martin.paredes@' }))).toEqual([MARTIN]);
    expect(filterProfesionales(ALL, filters({ search: 'example.test' }))).toHaveLength(4);
    expect(filterProfesionales(ALL, filters({ search: 'nadie@example.test' }))).toEqual([]);
  });

  it('filtra por especialidad asignada, incluidas las inactivas conservadas', () => {
    expect(filterProfesionales(ALL, filters({ especialidadId: 'perio' }))).toEqual([CAMILA, LUCIA]);
    expect(filterProfesionales(ALL, filters({ especialidadId: 'endo' }))).toEqual([MARTIN]);
  });

  it('filtra por local asignado, incluidas las asignaciones inactivas', () => {
    expect(filterProfesionales(ALL, filters({ localId: 'surco' }))).toEqual([LUCIA, VALERIA]);
    expect(filterProfesionales(ALL, filters({ localId: 'miraflores' }))).toEqual([CAMILA, VALERIA]);
  });

  it('filtra por estado de la ficha, independiente del estado de la cuenta', () => {
    expect(filterProfesionales(ALL, filters({ ficha: 'active' }))).toEqual([
      CAMILA,
      MARTIN,
      VALERIA,
    ]);
    expect(filterProfesionales(ALL, filters({ ficha: 'inactive' }))).toEqual([LUCIA]);
  });

  it('filtra por estado de la cuenta', () => {
    expect(filterProfesionales(ALL, filters({ account: 'Pendiente' }))).toEqual([CAMILA]);
    expect(filterProfesionales(ALL, filters({ account: 'Activa' }))).toEqual([LUCIA, VALERIA]);
    expect(filterProfesionales(ALL, filters({ account: 'Inactiva' }))).toEqual([MARTIN]);
  });

  it('combina la búsqueda con los cuatro filtros', () => {
    expect(
      filterProfesionales(
        ALL,
        filters({ search: 'a', localId: 'miraflores', ficha: 'active', account: 'Pendiente' }),
      ),
    ).toEqual([CAMILA]);

    expect(
      filterProfesionales(
        ALL,
        filters({ especialidadId: 'orto', account: 'Activa', ficha: 'inactive' }),
      ),
    ).toEqual([]);
  });

  it('resume los totales y separa los correos sin enviar', () => {
    const counts = countProfesionales(ALL);

    expect(counts).toEqual({
      total: 4,
      activeFichas: 3,
      inactiveFichas: 1,
      pendingAccounts: 1,
      failedInvitations: 1,
    });

    expect(
      countProfesionales([profesional({ accountStatus: 'Pendiente', invitationFailed: false })])
        .failedInvitations,
    ).toBe(0);
  });

  it('cuenta el total o los resultados con filtros', () => {
    expect(resultsLabel(1, 1, false)).toBe('1 profesional');
    expect(resultsLabel(4, 4, false)).toBe('4 profesionales');
    expect(resultsLabel(1, 4, true)).toBe('1 de 4 profesionales');
    expect(resultsLabel(0, 4, true)).toBe('0 de 4 profesionales');
  });

  it('enumera lo que falta para reactivar una ficha', () => {
    expect(activationMissing(VALERIA)).toEqual([]);
    expect(activationMissing(LUCIA)).toEqual([
      'Una especialidad activa asignada',
      'Un local activo asignado',
    ]);
    expect(
      activationMissing(
        profesional({
          especialidades: [{ id: 'perio', name: 'Periodoncia', isActive: false }],
        }),
      ),
    ).toEqual(['Una especialidad activa asignada']);
    expect(activeAssignmentCount(VALERIA.locales)).toBe(1);
  });

  it('traduce la regla de acceso a comprobaciones y una frase', () => {
    expect(accessChecks(VALERIA).map((check) => check.ok)).toEqual([true, true, true]);
    expect(accessSentence(VALERIA)).toBe('Puede operar en Miraflores.');

    expect(accessChecks(LUCIA).map((check) => check.ok)).toEqual([true, false, false]);
    expect(accessChecks(LUCIA)[2]!.detail).toBe('Todos los locales asignados están inactivos.');
    expect(accessSentence(LUCIA)).toBe(
      'No puede operar en ningún local porque la ficha está inactiva y no tiene un local activo asignado.',
    );

    expect(accessSentence(CAMILA)).toBe(
      'No puede operar en ningún local porque la cuenta está pendiente.',
    );

    expect(accessSentence(profesional({ especialidades: [], locales: [] }))).toContain(
      'no tiene un local activo asignado',
    );
  });

  it('avisa posibles homónimos sin bloquear', () => {
    expect(homonyms('Valeria Torres', ['Valeria Torres', 'Camila Mendoza'])).toEqual([
      'Valeria Torres',
    ]);
    expect(homonyms('Diego Salazar', ['Diego Salazar Ríos'])).toEqual(['Diego Salazar Ríos']);
    expect(homonyms('Valeria', ['Valeria Torres'])).toEqual([]);
    expect(homonyms('Ana Pérez', ['Camila Mendoza'])).toEqual([]);
  });

  it('usa las etiquetas visibles de la ficha', () => {
    expect(colegiaturaLabel(VALERIA)).toBe('Colegiatura COP 12345');
    expect(colegiaturaLabel(CAMILA)).toBe('Sin colegiatura registrada');
    expect(accountStatusLabel('Pendiente')).toBe('Cuenta pendiente');
    expect(initialsOf('Camila Mendoza')).toBe('CM');
  });
});
