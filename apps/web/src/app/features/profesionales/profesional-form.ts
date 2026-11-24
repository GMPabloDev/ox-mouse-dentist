import type {
  CreateProfesionalForm,
  ProfesionalResponse,
  StaffAccountState,
  UpdateProfesionalForm,
} from '@mousedent/api-client';
import type { ProfesionalFailure } from '../../core/profesionales/profesionales-api';
import type { Assignment } from './profesional-list';

/**
 * Borrador de la ficha. El nombre, el correo y el teléfono pertenecen a la cuenta:
 * solo se editan en el alta y, después, en el módulo de personal. La colegiatura,
 * las especialidades, los locales y el estado viven en la ficha.
 */
export interface ProfesionalDraft {
  /** `null` al crear: la ficha todavía no existe. */
  readonly id: string | null;
  readonly displayName: string;
  readonly email: string;
  readonly phone: string;
  readonly colegiatura: string;
  readonly especialidadIds: readonly string[];
  readonly localIds: readonly string[];
  /** Asignaciones guardadas, para conservar las de registros ahora inactivos. */
  readonly originalEspecialidadIds: readonly string[];
  readonly originalLocalIds: readonly string[];
  /** Estado de la ficha: decide el mínimo de asignaciones activas. */
  readonly isActive: boolean;
  readonly accountStatus: StaffAccountState | null;
  readonly invitationFailed: boolean;
}

export const EMPTY_DRAFT: ProfesionalDraft = {
  id: null,
  displayName: '',
  email: '',
  phone: '',
  colegiatura: '',
  especialidadIds: [],
  localIds: [],
  originalEspecialidadIds: [],
  originalLocalIds: [],
  isActive: true,
  accountStatus: null,
  invitationFailed: false,
};

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Longitud máxima del número de colegiatura después de quitar espacios exteriores. */
export const COLEGIATURA_MAX_LENGTH = 30;

export type ProfesionalProblemField =
  'displayName' | 'email' | 'colegiatura' | 'especialidadIds' | 'localIds' | 'form';

export type ProfesionalProblems = Partial<Record<ProfesionalProblemField, string>>;

export function draftFrom(profesional: ProfesionalResponse): ProfesionalDraft {
  return {
    id: profesional.id,
    displayName: profesional.displayName,
    email: profesional.email,
    phone: profesional.phone ?? '',
    colegiatura: profesional.colegiatura ?? '',
    especialidadIds: profesional.especialidades.map((especialidad) => especialidad.id),
    localIds: profesional.locales.map((local) => local.id),
    originalEspecialidadIds: profesional.especialidades.map((especialidad) => especialidad.id),
    originalLocalIds: profesional.locales.map((local) => local.id),
    isActive: profesional.isActive,
    accountStatus: profesional.accountStatus,
    invitationFailed: profesional.invitationFailed,
  };
}

export function isDirty(draft: ProfesionalDraft, initial: ProfesionalDraft): boolean {
  return (
    draft.displayName !== initial.displayName ||
    draft.email !== initial.email ||
    draft.colegiatura !== initial.colegiatura ||
    !sameIds(draft.especialidadIds, initial.especialidadIds) ||
    !sameIds(draft.localIds, initial.localIds)
  );
}

/** Una especialidad inactiva solo se conserva si ya estaba asignada. */
export function canAssignEspecialidad(draft: ProfesionalDraft, especialidad: Assignment): boolean {
  return especialidad.isActive || draft.originalEspecialidadIds.includes(especialidad.id);
}

/** Un local inactivo solo se conserva si ya estaba asignado a la ficha. */
export function canAssignLocal(draft: ProfesionalDraft, local: Assignment): boolean {
  return local.isActive || draft.originalLocalIds.includes(local.id);
}

/**
 * Reglas de forma antes de llamar a la API. Repiten las del servidor para dar el
 * error en el campo y no perder lo escrito; la API sigue siendo la autoridad.
 */
export function validateDraft(
  draft: ProfesionalDraft,
  especialidades: readonly Assignment[],
  locales: readonly Assignment[],
): ProfesionalProblems {
  const problems: ProfesionalProblems = {};

  if (draft.id === null) {
    const displayName = draft.displayName.trim();
    const email = draft.email.trim();

    if (!displayName) {
      problems.displayName = 'Escribe el nombre completo.';
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
  }

  const colegiatura = draft.colegiatura.trim();
  if (colegiatura.length > COLEGIATURA_MAX_LENGTH) {
    problems.colegiatura = `La colegiatura admite hasta ${COLEGIATURA_MAX_LENGTH} caracteres, sin contar los espacios de los extremos.`;
  }

  const especialidadesById = new Map(especialidades.map((item) => [item.id, item]));
  const localesById = new Map(locales.map((item) => [item.id, item]));

  const invalidEspecialidad = draft.especialidadIds.some((id) => {
    const item = especialidadesById.get(id);
    return !item || (!item.isActive && !draft.originalEspecialidadIds.includes(id));
  });
  const invalidLocal = draft.localIds.some((id) => {
    const item = localesById.get(id);
    return !item || (!item.isActive && !draft.originalLocalIds.includes(id));
  });

  const activeEspecialidades = draft.especialidadIds.filter(
    (id) => especialidadesById.get(id)?.isActive,
  ).length;
  const activeLocales = draft.localIds.filter((id) => localesById.get(id)?.isActive).length;

  if (invalidEspecialidad) {
    problems.especialidadIds = 'Solo puedes asignar especialidades activas.';
  } else if ((draft.id === null || draft.isActive) && activeEspecialidades === 0) {
    problems.especialidadIds = 'Selecciona al menos una especialidad activa.';
  }

  if (invalidLocal) {
    problems.localIds = 'Solo puedes asignar locales activos.';
  } else if ((draft.id === null || draft.isActive) && activeLocales === 0) {
    problems.localIds = 'Selecciona al menos un local activo.';
  }

  return problems;
}

export function toCreateForm(draft: ProfesionalDraft): CreateProfesionalForm {
  return {
    email: draft.email.trim(),
    displayName: draft.displayName.trim(),
    colegiatura: draft.colegiatura.trim() || null,
    especialidadIds: [...draft.especialidadIds],
    localIds: [...draft.localIds],
  };
}

export function toUpdateForm(draft: ProfesionalDraft): UpdateProfesionalForm {
  return {
    colegiatura: draft.colegiatura.trim() || null,
    especialidadIds: [...draft.especialidadIds],
    localIds: [...draft.localIds],
  };
}

const FIELD_NAMES: Readonly<Record<string, ProfesionalProblemField>> = {
  DisplayName: 'displayName',
  Email: 'email',
  Colegiatura: 'colegiatura',
  EspecialidadIds: 'especialidadIds',
  LocalIds: 'localIds',
};

const UNAVAILABLE_MESSAGE =
  'No pudimos guardar los cambios. Revisa tu conexión y vuelve a intentarlo.';

/** Traduce el fallo del servidor a errores de campo y un mensaje general. */
export function problemsFromFailure(failure: ProfesionalFailure): ProfesionalProblems {
  switch (failure.kind) {
    case 'validation': {
      const problems: ProfesionalProblems = {};

      for (const [field, messages] of Object.entries(failure.errors)) {
        const mapped = FIELD_NAMES[field];
        const message = messages[0];

        if (mapped && message) {
          problems[mapped] = message;
        }
      }

      return Object.keys(problems).length > 0
        ? problems
        : { form: 'Revisa los datos de la ficha.' };
    }
    case 'duplicate-colegiatura':
      return {
        colegiatura:
          failure.detail ?? 'Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.',
      };
    case 'email-taken':
      return {
        email:
          failure.detail ??
          'Ese correo pertenece a una cuenta que ya tiene una ficha. Edita la ficha existente.',
      };
    case 'other-profile':
      return {
        email:
          failure.detail ??
          'Ese correo pertenece a una cuenta de administración o recepción. Una cuenta no puede tener dos perfiles.',
      };
    case 'activation-incomplete':
      return {
        form:
          failure.detail ??
          'La ficha necesita al menos una especialidad activa y un local de atención activo.',
      };
    case 'not-found':
      return { form: 'La ficha ya no existe. Actualiza la lista y vuelve a intentarlo.' };
    case 'forbidden':
      return { form: 'Tu perfil ya no permite gestionar profesionales.' };
    case 'unauthorized':
      return { form: 'Tu sesión ya no está vigente. Vuelve a iniciar sesión.' };
    case 'conflict':
      return { form: failure.detail ?? 'La operación no se completó. Vuelve a intentarlo.' };
    case 'invitation-failed':
    case 'unavailable':
      return { form: UNAVAILABLE_MESSAGE };
  }
}

function sameIds(left: readonly string[], right: readonly string[]): boolean {
  return left.length === right.length && left.every((id) => right.includes(id));
}
