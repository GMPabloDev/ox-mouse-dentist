import type { ProfesionalResponse, StaffAccountState } from '@mousedent/api-client';

/** Estado de la ficha en el filtro. `all` no filtra. */
export type FichaFilter = 'all' | 'active' | 'inactive';

/** Estado de la cuenta en el filtro. `all` no filtra. */
export type AccountFilter = 'all' | StaffAccountState;

/** Filtros de la lista. Se combinan entre sí y con la búsqueda. */
export interface ProfesionalFilters {
  readonly search: string;
  readonly especialidadId: string;
  readonly localId: string;
  readonly ficha: FichaFilter;
  readonly account: AccountFilter;
}

export const EMPTY_FILTERS: ProfesionalFilters = {
  search: '',
  especialidadId: '',
  localId: '',
  ficha: 'all',
  account: 'all',
};

export interface ProfesionalCounts {
  readonly total: number;
  readonly activeFichas: number;
  readonly inactiveFichas: number;
  readonly pendingAccounts: number;
  /** Invitaciones que no salieron. El resumen las conserva aunque la cuenta siga pendiente. */
  readonly failedInvitations: number;
}

/** Elemento del catálogo o del local que el formulario puede asignar. */
export interface Assignment {
  readonly id: string;
  readonly name: string;
  readonly isActive: boolean;
}

export function hasFilters(filters: ProfesionalFilters): boolean {
  return Boolean(
    filters.search.trim() ||
    filters.especialidadId ||
    filters.localId ||
    filters.ficha !== 'all' ||
    filters.account !== 'all',
  );
}

/**
 * Aplica búsqueda y filtros sobre la lista ya cargada. La búsqueda ignora tildes
 * y mayúsculas en el nombre; el correo se compara en minúsculas. El resultado
 * conserva el orden por nombre que muestra la tabla.
 */
export function filterProfesionales(
  list: readonly ProfesionalResponse[],
  filters: ProfesionalFilters,
): readonly ProfesionalResponse[] {
  const term = normalize(filters.search);
  const email = filters.search.trim().toLowerCase();

  return list
    .filter((profesional) => {
      if (term || email) {
        const byName = Boolean(term) && normalize(profesional.displayName).includes(term);
        const byEmail = Boolean(email) && profesional.email.toLowerCase().includes(email);

        if (!byName && !byEmail) {
          return false;
        }
      }

      if (
        filters.especialidadId &&
        !profesional.especialidades.some((item) => item.id === filters.especialidadId)
      ) {
        return false;
      }

      if (filters.localId && !profesional.locales.some((item) => item.id === filters.localId)) {
        return false;
      }

      if (filters.ficha === 'active' && !profesional.isActive) {
        return false;
      }

      if (filters.ficha === 'inactive' && profesional.isActive) {
        return false;
      }

      return filters.account === 'all' || profesional.accountStatus === filters.account;
    })
    .sort((left, right) => left.displayName.localeCompare(right.displayName, 'es'));
}

export function countProfesionales(list: readonly ProfesionalResponse[]): ProfesionalCounts {
  return {
    total: list.length,
    activeFichas: list.filter((profesional) => profesional.isActive).length,
    inactiveFichas: list.filter((profesional) => !profesional.isActive).length,
    pendingAccounts: list.filter((profesional) => profesional.accountStatus === 'Pendiente').length,
    failedInvitations: list.filter(
      (profesional) => profesional.accountStatus === 'Pendiente' && profesional.invitationFailed,
    ).length,
  };
}

/** Recuento que muestra la tabla: total sin filtros y «N de M» con ellos. */
export function resultsLabel(visible: number, total: number, filtered: boolean): string {
  if (!filtered) {
    return `${visible} ${visible === 1 ? 'profesional' : 'profesionales'}`;
  }

  return `${visible} de ${total} profesionales`;
}

/** Número de asignaciones que hoy están activas en el catálogo o en el local. */
export function activeAssignmentCount(items: readonly { readonly isActive: boolean }[]): number {
  return items.filter((item) => item.isActive).length;
}

/**
 * Requisitos que faltan para reactivar una ficha. Una asignación inactiva no
 * cuenta: el profesional necesita hoy una especialidad y un local activos.
 */
export function activationMissing(profesional: ProfesionalResponse): readonly string[] {
  const missing: string[] = [];

  if (activeAssignmentCount(profesional.especialidades) === 0) {
    missing.push('Una especialidad activa asignada');
  }

  if (activeAssignmentCount(profesional.locales) === 0) {
    missing.push('Un local activo asignado');
  }

  return missing;
}

/** Comprobación de la regla de acceso vigente que resume la ficha. */
export interface AccessCheck {
  readonly ok: boolean;
  readonly label: string;
  readonly detail: string | null;
}

export function accessChecks(profesional: ProfesionalResponse): readonly AccessCheck[] {
  const activeLocales = profesional.locales.filter((local) => local.isActive);

  return [
    {
      ok: profesional.accountStatus === 'Activa',
      label: 'Cuenta activa y con contraseña',
      detail:
        profesional.accountStatus === 'Pendiente'
          ? 'La cuenta está pendiente de invitación.'
          : profesional.accountStatus === 'Inactiva'
            ? 'La cuenta está inactiva.'
            : null,
    },
    {
      ok: profesional.isActive,
      label: 'Ficha activa',
      detail: profesional.isActive ? null : 'La ficha está inactiva.',
    },
    {
      ok: activeLocales.length > 0,
      label: 'Local activo asignado',
      detail:
        activeLocales.length > 0
          ? null
          : profesional.locales.length > 0
            ? 'Todos los locales asignados están inactivos.'
            : 'No tiene locales asignados.',
    },
  ];
}

/** Frase que traduce las comprobaciones de acceso a un resultado legible. */
export function accessSentence(profesional: ProfesionalResponse): string {
  const reasons: string[] = [];

  if (profesional.accountStatus === 'Pendiente') {
    reasons.push('la cuenta está pendiente');
  } else if (profesional.accountStatus === 'Inactiva') {
    reasons.push('la cuenta está inactiva');
  }

  if (!profesional.isActive) {
    reasons.push('la ficha está inactiva');
  }

  if (activeAssignmentCount(profesional.locales) === 0) {
    reasons.push('no tiene un local activo asignado');
  }

  if (reasons.length > 0) {
    return `No puede operar en ningún local porque ${joinReasons(reasons)}.`;
  }

  const names = profesional.locales.filter((local) => local.isActive).map((local) => local.name);

  return `Puede operar en ${listNames(names)}.`;
}

/**
 * Nombres ya registrados iguales o contenidos en el nombre escrito. La ficha no
 * es única por nombre: la interfaz avisa y deja continuar.
 */
export function homonyms(name: string, existing: readonly string[]): readonly string[] {
  const written = normalize(name);
  const tokens = written.split(/\s+/).filter(Boolean);

  if (tokens.length < 2) {
    return [];
  }

  return existing
    .filter((candidate) => {
      const current = normalize(candidate);

      return (
        current === written ||
        current.startsWith(`${written} `) ||
        written.startsWith(`${current} `)
      );
    })
    .slice(0, 2);
}

export function colegiaturaLabel(profesional: ProfesionalResponse): string {
  return profesional.colegiatura
    ? `Colegiatura ${profesional.colegiatura}`
    : 'Sin colegiatura registrada';
}

const ACCOUNT_STATUS_LABELS: Readonly<Record<StaffAccountState, string>> = {
  Activa: 'Cuenta activa',
  Pendiente: 'Cuenta pendiente',
  Inactiva: 'Cuenta inactiva',
};

export function accountStatusLabel(status: StaffAccountState): string {
  return ACCOUNT_STATUS_LABELS[status];
}

export function initialsOf(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0))
    .join('')
    .toUpperCase();
}

function normalize(value: string): string {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();
}

function joinReasons(reasons: readonly string[]): string {
  if (reasons.length === 1) {
    return reasons[0]!;
  }

  if (reasons.length === 2) {
    return `${reasons[0]} y ${reasons[1]}`;
  }

  return `${reasons.slice(0, -1).join(', ')} y ${reasons[reasons.length - 1]}`;
}

function listNames(names: readonly string[]): string {
  if (names.length === 1) {
    return names[0]!;
  }

  if (names.length === 2) {
    return `${names[0]} y ${names[1]}`;
  }

  return `${names.slice(0, -1).join(', ')} y ${names[names.length - 1]}`;
}
