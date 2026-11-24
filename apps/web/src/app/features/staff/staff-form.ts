import type { StaffAccountResponse, StaffAccountState } from '@mousedent/api-client';
import type { StaffFailure } from '../../core/staff/staff-api';

/** Opciones del perfil único. El orden y los textos vienen del prototipo A. */
export const PROFILE_OPTIONS = [
  {
    value: 'Recepcion',
    label: 'Recepción',
    description: 'Trabaja solo con los locales asignados y activos.',
  },
  {
    value: 'Administrador',
    label: 'Administración',
    description: 'Gestiona el personal y todos los locales.',
  },
] as const;

/**
 * El módulo de personal ya no crea ni asigna el perfil `Profesional`: nace en el
 * alta de profesionales, junto con su ficha. Se conserva para leer las cuentas
 * que ya lo tienen y mostrar su etiqueta.
 */
export const PROFESSIONAL_PROFILE = {
  value: 'Profesional',
} as const;

/** Perfiles que puede tener una cuenta, incluido el que solo se muestra. */
export const ACCOUNT_PROFILES = [...PROFILE_OPTIONS, PROFESSIONAL_PROFILE] as const;

export type StaffProfile = (typeof ACCOUNT_PROFILES)[number]['value'];

/** Perfiles que el módulo de personal puede asignar. */
export type AssignableStaffProfile = (typeof PROFILE_OPTIONS)[number]['value'];

export function isProfile(value: string): value is StaffProfile {
  return ACCOUNT_PROFILES.some((option) => option.value === value);
}

/** Local tal como lo necesita la asignación de recepción. */
export interface StaffLocal {
  readonly id: string;
  readonly name: string;
  readonly isActive: boolean;
}

export interface StaffDraft {
  /** `null` al crear: la cuenta todavía no existe. */
  readonly id: string | null;
  readonly displayName: string;
  readonly email: string;
  readonly phone: string;
  readonly profile: StaffProfile;
  readonly localIds: readonly string[];
  /** Asignaciones guardadas, para conservar las de locales inactivos. */
  readonly originalLocalIds: readonly string[];
}

export const EMPTY_DRAFT: StaffDraft = {
  id: null,
  displayName: '',
  email: '',
  phone: '',
  profile: 'Recepcion',
  localIds: [],
  originalLocalIds: [],
};

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type StaffProblemField = 'displayName' | 'email' | 'phone' | 'profile' | 'localIds' | 'form';

export type StaffProblems = Partial<Record<StaffProblemField, string>>;

export function draftFrom(account: StaffAccountResponse): StaffDraft {
  return {
    id: account.id,
    displayName: account.displayName,
    email: account.email,
    phone: account.phone ?? '',
    profile: isProfile(account.profile) ? account.profile : 'Recepcion',
    localIds: [...account.assignedLocalIds],
    originalLocalIds: [...account.assignedLocalIds],
  };
}

export function isDirty(draft: StaffDraft, initial: StaffDraft): boolean {
  return (
    draft.displayName !== initial.displayName ||
    draft.email !== initial.email ||
    draft.phone !== initial.phone ||
    draft.profile !== initial.profile ||
    draft.localIds.length !== initial.localIds.length ||
    draft.localIds.some((id) => !initial.localIds.includes(id))
  );
}

/** Un local inactivo solo se puede mantener si ya estaba asignado a la cuenta. */
export function canAssignLocal(draft: StaffDraft, local: StaffLocal): boolean {
  return local.isActive || draft.originalLocalIds.includes(local.id);
}

/**
 * El perfil `Profesional` se asigna en el alta de profesionales, así que aquí no se
 * ofrece el selector. El contrato de la cuenta no dice si tiene ficha: la interfaz
 * deduce el caso por el perfil, el único que puede tenerla, y por eso tampoco deja
 * cambiar el de una cuenta `Profesional` sin ficha, que la API todavía aceptaría.
 * Editar sus datos sigue disponible y el aviso del formulario explica ambos casos.
 */
export function profileIsEditable(draft: StaffDraft): boolean {
  return draft.id === null || draft.profile !== PROFESSIONAL_PROFILE.value;
}

/**
 * Reglas de forma antes de llamar a la API. Repiten las del servidor para dar
 * el error en el campo y no perder lo escrito; la API sigue siendo la autoridad.
 */
export function validateDraft(draft: StaffDraft, locals: readonly StaffLocal[]): StaffProblems {
  const problems: StaffProblems = {};
  const displayName = draft.displayName.trim();
  const email = draft.email.trim();
  const phone = draft.phone.trim();

  if (!displayName) {
    problems.displayName = 'Escribe el nombre de la persona.';
  } else if (displayName.length > 120) {
    problems.displayName = 'El nombre no puede superar los 120 caracteres.';
  }

  if (!email) {
    problems.email = 'Escribe el correo de la persona.';
  } else if (email.length > 256) {
    problems.email = 'El correo no puede superar los 256 caracteres.';
  } else if (!EMAIL_PATTERN.test(email)) {
    problems.email = 'Escribe un correo electrónico válido.';
  }

  if (phone.length > 40) {
    problems.phone = 'El teléfono no puede superar los 40 caracteres.';
  }

  if (!isProfile(draft.profile)) {
    problems.profile = 'Elige uno de los perfiles disponibles.';
  }

  if (draft.profile === 'Recepcion') {
    const byId = new Map(locals.map((local) => [local.id, local]));
    const invalid = draft.localIds.filter((id) => {
      const local = byId.get(id);
      return !local || (!local.isActive && !draft.originalLocalIds.includes(id));
    });
    const activeCount = draft.localIds.filter((id) => byId.get(id)?.isActive).length;
    const keepsInactive = draft.localIds.some((id) => draft.originalLocalIds.includes(id));

    if (invalid.length > 0) {
      problems.localIds = 'Solo puedes asignar locales activos.';
    } else if (activeCount === 0 && !(draft.id !== null && keepsInactive)) {
      problems.localIds = 'Selecciona al menos un local activo.';
    }
  }

  return problems;
}

/** Cuerpo para crear o editar. Solo recepción envía asignaciones. */
export function toForm(draft: StaffDraft): {
  displayName: string;
  email: string;
  phone: string | null;
  profile: string;
  assignedLocalIds: string[];
} {
  return {
    displayName: draft.displayName.trim(),
    email: draft.email.trim(),
    phone: draft.phone.trim() || null,
    profile: draft.profile,
    assignedLocalIds: draft.profile === 'Recepcion' ? [...draft.localIds] : [],
  };
}

const FIELD_NAMES: Readonly<Record<string, StaffProblemField>> = {
  DisplayName: 'displayName',
  Email: 'email',
  Phone: 'phone',
  Profile: 'profile',
  AssignedLocalIds: 'localIds',
};

const NO_ADMINISTRATOR_MESSAGE =
  'No puedes dejar al equipo sin administración disponible. Activa otra cuenta de administración con contraseña antes de cambiar este correo o perfil.';

const UNAVAILABLE_MESSAGE =
  'No pudimos guardar los cambios. Revisa tu conexión y vuelve a intentarlo.';

/** Traduce el fallo del servidor a errores de campo y un mensaje general. */
export function problemsFromFailure(failure: StaffFailure): StaffProblems {
  switch (failure.kind) {
    case 'validation': {
      const problems: StaffProblems = {};

      for (const [field, messages] of Object.entries(failure.errors)) {
        const mapped = FIELD_NAMES[field];
        const message = messages[0];

        if (mapped && message) {
          problems[mapped] = message;
        }
      }

      return Object.keys(problems).length > 0
        ? problems
        : { form: 'Revisa los datos de la cuenta.' };
    }
    case 'duplicate-email':
      return {
        email:
          failure.detail ??
          'Este correo ya pertenece a una cuenta, incluso si está pendiente o inactiva.',
      };
    case 'last-administrator':
      return { form: failure.detail ?? NO_ADMINISTRATOR_MESSAGE };
    case 'not-found':
      return { form: 'La cuenta ya no existe. Actualiza la lista y vuelve a intentarlo.' };
    case 'forbidden':
      return { form: 'Tu perfil ya no permite gestionar el personal.' };
    case 'unauthorized':
      return { form: 'Tu sesión ya no está vigente. Vuelve a iniciar sesión.' };
    case 'invitation-failed':
    case 'concurrent-change':
    case 'conflict':
      return { form: failure.detail ?? UNAVAILABLE_MESSAGE };
    case 'unavailable':
      return { form: UNAVAILABLE_MESSAGE };
  }
}

const STATE_LABELS: Readonly<Record<StaffAccountState, string>> = {
  Activa: 'Activa',
  Pendiente: 'Pendiente',
  Inactiva: 'Inactiva',
};

export function statusLabel(status: StaffAccountState): string {
  return STATE_LABELS[status];
}

/**
 * Resume el acceso por local que muestra la tabla. Un perfil sin asignaciones no
 * necesita una lista vacía: su alcance ya está definido por el perfil.
 */
export function accessSummary(
  account: StaffAccountResponse,
  locals: ReadonlyMap<string, StaffLocal>,
): string {
  if (account.profile === 'Administrador') {
    return 'Todos los locales';
  }

  if (account.profile !== 'Recepcion') {
    return 'Sin locales de atención';
  }

  const assigned = account.assignedLocalIds
    .map((id) => locals.get(id))
    .filter((local): local is StaffLocal => local !== undefined);

  return assigned.some((local) => local.isActive)
    ? assigned.map((local) => local.name).join(' · ')
    : 'Sin locales activos';
}

/** Indica si una cuenta de recepción conserva alguna asignación activa. */
export function hasActiveLocal(
  account: StaffAccountResponse,
  locals: ReadonlyMap<string, StaffLocal>,
): boolean {
  return account.assignedLocalIds.some((id) => locals.get(id)?.isActive === true);
}
