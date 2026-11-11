import type { LocalScheduleDay, LocalWeekDay } from '@mousedent/api-client';

/** Días del horario en el orden en que la web y la API los presentan. */
export const WEEK_DAYS: readonly LocalWeekDay[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

export const DAY_NAMES: Readonly<Record<LocalWeekDay, string>> = {
  Monday: 'Lunes',
  Tuesday: 'Martes',
  Wednesday: 'Miércoles',
  Thursday: 'Jueves',
  Friday: 'Viernes',
  Saturday: 'Sábado',
  Sunday: 'Domingo',
};

export const DAY_SHORT: Readonly<Record<LocalWeekDay, string>> = {
  Monday: 'Lun',
  Tuesday: 'Mar',
  Wednesday: 'Mié',
  Thursday: 'Jue',
  Friday: 'Vie',
  Saturday: 'Sáb',
  Sunday: 'Dom',
};

export const DAY_LETTER: Readonly<Record<LocalWeekDay, string>> = {
  Monday: 'L',
  Tuesday: 'M',
  Wednesday: 'M',
  Thursday: 'J',
  Friday: 'V',
  Saturday: 'S',
  Sunday: 'D',
};

export interface SchedulePreset {
  readonly id: 'full' | 'morning' | 'afternoon';
  readonly title: string;
  readonly opensAt: string;
  readonly closesAt: string;
}

/** Sugerencias de tramo del editor por grupos. */
export const SCHEDULE_PRESETS: readonly SchedulePreset[] = [
  { id: 'full', title: 'Día completo', opensAt: '09:00', closesAt: '18:00' },
  { id: 'morning', title: 'Solo mañana', opensAt: '08:00', closesAt: '13:00' },
  { id: 'afternoon', title: 'Solo tarde', opensAt: '14:00', closesAt: '20:00' },
];

/** Semana inicial: todos los días cerrados, sin horas. */
export function closedWeek(): LocalScheduleDay[] {
  return WEEK_DAYS.map((day) => ({ day, isClosed: true, opensAt: null, closesAt: null }));
}

/** Copia la semana con los siete días en orden, completando los ausentes como cerrados. */
export function normalizeWeek(days: readonly LocalScheduleDay[]): LocalScheduleDay[] {
  return WEEK_DAYS.map((day) => {
    const found = days.find((candidate) => candidate.day === day);

    return found ? { ...found } : { day, isClosed: true, opensAt: null, closesAt: null };
  });
}

/** Devuelve una semana nueva con el mismo tramo en los días elegidos. No guarda nada. */
export function applyRange(
  week: readonly LocalScheduleDay[],
  days: ReadonlySet<LocalWeekDay>,
  opensAt: string,
  closesAt: string,
): LocalScheduleDay[] {
  return normalizeWeek(week).map((day) =>
    days.has(day.day) ? { day: day.day, isClosed: false, opensAt, closesAt } : day,
  );
}

/** Devuelve una semana nueva con los días elegidos cerrados. No guarda nada. */
export function closeDays(
  week: readonly LocalScheduleDay[],
  days: ReadonlySet<LocalWeekDay>,
): LocalScheduleDay[] {
  return normalizeWeek(week).map((day) =>
    days.has(day.day) ? { day: day.day, isClosed: true, opensAt: null, closesAt: null } : day,
  );
}

export function openDayCount(week: readonly LocalScheduleDay[]): number {
  return week.filter((day) => !day.isClosed).length;
}

/** Minutos desde medianoche de una hora HH:mm, o `null` si no es una hora válida. */
export function minutesOf(time: string | null): number | null {
  if (!time) {
    return null;
  }

  const match = /^(\d{2}):(\d{2})$/.exec(time);
  if (!match) {
    return null;
  }

  const hours = Number(match[1]);
  const minutes = Number(match[2]);

  return hours > 23 || minutes > 59 ? null : hours * 60 + minutes;
}

/** Valida un tramo antes de aplicarlo a los días elegidos. */
export function rangeProblem(opensAt: string | null, closesAt: string | null): string | null {
  const start = minutesOf(opensAt);
  const end = minutesOf(closesAt);

  if (start === null || end === null) {
    return 'Indica la hora de apertura y la de cierre.';
  }

  return start < end ? null : 'Indica una apertura anterior al cierre dentro del mismo día.';
}

/** Valida un día del horario que se va a guardar. */
export function dayProblem(day: LocalScheduleDay): string | null {
  if (day.isClosed) {
    return null;
  }

  const start = minutesOf(day.opensAt);
  const end = minutesOf(day.closesAt);

  if (start === null || end === null) {
    return 'Indica la hora de apertura y la de cierre.';
  }

  return start < end ? null : 'La hora de cierre debe ser posterior a la hora de apertura.';
}

/** Regla de la spec: la semana necesita al menos un día abierto. */
export function scheduleProblem(week: readonly LocalScheduleDay[]): string | null {
  return openDayCount(week) === 0 ? 'El horario debe tener al menos un día abierto.' : null;
}

export function hoursLabel(day: LocalScheduleDay): string {
  return day.isClosed || !day.opensAt || !day.closesAt
    ? 'Cerrado'
    : `${day.opensAt} a ${day.closesAt}`;
}

/** Posición del tramo dentro de las 24 horas, para la barra del resumen. */
export function barPosition(
  opensAt: string | null,
  closesAt: string | null,
): { readonly left: number; readonly width: number } {
  const start = minutesOf(opensAt) ?? 0;
  const end = minutesOf(closesAt) ?? 0;
  const day = 24 * 60;

  return { left: (start / day) * 100, width: (Math.max(0, end - start) / day) * 100 };
}
