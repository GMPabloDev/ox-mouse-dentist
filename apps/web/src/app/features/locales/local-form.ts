import type {
  CreateLocalForm,
  LocalResponse,
  LocalScheduleDay,
  LocalWeekDay,
  UpdateLocalForm,
} from '@mousedent/api-client';
import type { PhotoAction } from '../../core/locales/locales-api';
import { WEEK_DAYS, closedWeek, normalizeWeek } from './schedule-model';

/** Límites que aplica la API a la foto y que la web comprueba antes de enviarla. */
export const MAX_PHOTO_BYTES = 5 * 1024 * 1024;
export const PHOTO_CONTENT_TYPES: readonly string[] = ['image/jpeg', 'image/png', 'image/webp'];

export const DUPLICATE_NAME_MESSAGE = 'Ya existe un local con ese nombre.';

/** Datos del formulario que se guardan juntos: contacto y horario semanal. */
export interface LocalDraft {
  readonly name: string;
  readonly address: string;
  readonly phone: string;
  readonly schedule: readonly LocalScheduleDay[];
}

/** Problemas del formulario, ligados al campo o al día que hay que corregir. */
export interface LocalFormProblems {
  name?: string;
  address?: string;
  phone?: string;
  schedule?: string;
  photo?: string;
  days?: Partial<Record<LocalWeekDay, string>>;
}

export function emptyDraft(): LocalDraft {
  return { name: '', address: '', phone: '', schedule: closedWeek() };
}

export function draftFrom(local: LocalResponse): LocalDraft {
  return {
    name: local.name,
    address: local.address,
    phone: local.phone,
    schedule: normalizeWeek(local.schedule),
  };
}

export function hasProblems(problems: LocalFormProblems): boolean {
  return Boolean(
    problems.name ||
    problems.address ||
    problems.phone ||
    problems.schedule ||
    problems.photo ||
    (problems.days && Object.keys(problems.days).length > 0),
  );
}

/** Comprueba el archivo antes de preparar la vista previa. */
export function checkPhotoFile(file: File): string | null {
  if (!PHOTO_CONTENT_TYPES.includes(file.type)) {
    return 'Solo se aceptan imágenes JPG, PNG o WebP.';
  }

  if (file.size > MAX_PHOTO_BYTES) {
    return 'La foto no puede superar 5 MB.';
  }

  return file.size <= 0 ? 'La foto está vacía.' : null;
}

/** Cuerpo del alta: la foto es opcional y el horario viaja como texto JSON. */
export function toCreateForm(draft: LocalDraft, photo: File | null): CreateLocalForm {
  return {
    name: draft.name.trim(),
    address: draft.address.trim(),
    phone: draft.phone.trim(),
    schedule: scheduleJson(draft.schedule),
    ...(photo ? { photo } : {}),
  };
}

/** Cuerpo de la edición: la indicación sobre la foto es siempre explícita. */
export function toUpdateForm(
  draft: LocalDraft,
  photoAction: PhotoAction,
  photo: File | null,
): UpdateLocalForm {
  return {
    photoAction,
    name: draft.name.trim(),
    address: draft.address.trim(),
    phone: draft.phone.trim(),
    schedule: scheduleJson(draft.schedule),
    ...(photoAction === 'replace' && photo ? { photo } : {}),
  };
}

/** Los siete días en el orden de la API, con las horas en HH:mm. */
export function scheduleJson(days: readonly LocalScheduleDay[]): string {
  return JSON.stringify(
    normalizeWeek(days).map((day) => ({
      day: day.day,
      isClosed: day.isClosed,
      opensAt: day.isClosed ? null : day.opensAt,
      closesAt: day.isClosed ? null : day.closesAt,
    })),
  );
}

/** Traduce los errores del servidor a los campos y días del formulario. */
export function mapValidationErrors(
  errors: Readonly<Record<string, readonly string[]>>,
): LocalFormProblems {
  const problems: LocalFormProblems = {};
  const days: Partial<Record<LocalWeekDay, string>> = {};

  for (const [field, messages] of Object.entries(errors)) {
    const message = messages[0];
    if (!message) {
      continue;
    }

    switch (field) {
      case 'Name':
        problems.name ??= message;
        break;
      case 'Address':
        problems.address ??= message;
        break;
      case 'Phone':
        problems.phone ??= message;
        break;
      case 'Schedule':
        problems.schedule ??= message;
        break;
      case 'Photo':
      case 'PhotoAction':
        problems.photo ??= message;
        break;
      default: {
        const match = /^Schedule\[(\d+)\]/.exec(field);
        const day = match ? WEEK_DAYS[Number(match[1])] : undefined;
        if (day) {
          days[day] ??= message;
        }
      }
    }
  }

  if (Object.keys(days).length > 0) {
    problems.days = days;
    problems.schedule ??= 'Revisa los días del horario.';
  }

  return problems;
}
