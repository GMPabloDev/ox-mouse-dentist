import type { LocalScheduleDay } from '@mousedent/api-client';
import {
  applyRange,
  barPosition,
  closeDays,
  closedWeek,
  dayProblem,
  hoursLabel,
  normalizeWeek,
  openDayCount,
  rangeProblem,
  scheduleProblem,
  WEEK_DAYS,
} from './schedule-model';

function weekWithFriday(): LocalScheduleDay[] {
  return applyRange(closedWeek(), new Set(['Friday']), '08:00', '13:00');
}

function hours(week: readonly LocalScheduleDay[]): Record<string, string> {
  return Object.fromEntries(week.map((day) => [day.day, hoursLabel(day)]));
}

describe('horario por grupos', () => {
  it('aplica el tramo solo a los días elegidos y no guarda nada', () => {
    const week = applyRange(closedWeek(), new Set(['Monday', 'Wednesday']), '09:00', '18:00');

    expect(hours(week)).toEqual({
      Monday: '09:00 a 18:00',
      Tuesday: 'Cerrado',
      Wednesday: '09:00 a 18:00',
      Thursday: 'Cerrado',
      Friday: 'Cerrado',
      Saturday: 'Cerrado',
      Sunday: 'Cerrado',
    });
    expect(openDayCount(week)).toBe(2);
  });

  it('conserva el horario de los días que no están seleccionados', () => {
    const before = weekWithFriday();
    const after = applyRange(before, new Set(['Monday']), '10:00', '16:00');

    expect(after.find((day) => day.day === 'Friday')).toEqual(
      before.find((day) => day.day === 'Friday'),
    );
    expect(hours(after)['Monday']).toBe('10:00 a 16:00');
  });

  it('marca cerrados los días elegidos y borra sus horas', () => {
    const before = applyRange(closedWeek(), new Set(WEEK_DAYS.slice(0, 5)), '09:00', '18:00');
    const after = closeDays(before, new Set(['Saturday', 'Sunday']));
    const closedSunday = after.find((day) => day.day === 'Sunday')!;

    expect(closedSunday.isClosed).toBe(true);
    expect(closedSunday.opensAt).toBeNull();
    expect(closedSunday.closesAt).toBeNull();
    expect(hours(after)['Monday']).toBe('09:00 a 18:00');
    expect(openDayCount(after)).toBe(5);
  });

  it('exige al menos un día abierto para guardar la semana', () => {
    expect(scheduleProblem(closedWeek())).toBe('El horario debe tener al menos un día abierto.');
    expect(scheduleProblem(weekWithFriday())).toBeNull();
  });

  it('exige que la apertura preceda al cierre dentro del mismo día', () => {
    expect(rangeProblem('18:00', '09:00')).toBe(
      'Indica una apertura anterior al cierre dentro del mismo día.',
    );
    expect(rangeProblem('09:00', '09:00')).toBe(
      'Indica una apertura anterior al cierre dentro del mismo día.',
    );
    expect(rangeProblem('09:00', '18:00')).toBeNull();
    expect(rangeProblem('', '18:00')).toBe('Indica la hora de apertura y la de cierre.');
  });

  it('describe el problema de un día abierto sin horas válidas', () => {
    expect(dayProblem({ day: 'Monday', isClosed: true, opensAt: null, closesAt: null })).toBeNull();
    expect(
      dayProblem({ day: 'Monday', isClosed: false, opensAt: '20:00', closesAt: '08:00' }),
    ).toBe('La hora de cierre debe ser posterior a la hora de apertura.');
    expect(dayProblem({ day: 'Monday', isClosed: false, opensAt: null, closesAt: null })).toBe(
      'Indica la hora de apertura y la de cierre.',
    );
  });

  it('ordena los siete días de lunes a domingo y completa los ausentes como cerrados', () => {
    const week = normalizeWeek([
      { day: 'Sunday', isClosed: false, opensAt: '10:00', closesAt: '14:00' },
    ]);

    expect(week.map((day) => day.day)).toEqual([...WEEK_DAYS]);
    expect(week[6]).toEqual({
      day: 'Sunday',
      isClosed: false,
      opensAt: '10:00',
      closesAt: '14:00',
    });
    expect(week.slice(0, 6).every((day) => day.isClosed)).toBe(true);
  });

  it('sitúa el tramo en la barra de 24 horas del resumen', () => {
    expect(barPosition('09:00', '18:00')).toEqual({ left: 37.5, width: 37.5 });
    expect(barPosition('06:00', '12:00')).toEqual({ left: 25, width: 25 });
    expect(barPosition(null, null)).toEqual({ left: 0, width: 0 });
  });
});
