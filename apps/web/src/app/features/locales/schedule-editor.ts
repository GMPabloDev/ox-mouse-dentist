import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import type { LocalScheduleDay, LocalWeekDay } from '@mousedent/api-client';
import { HlmAlert } from '@mousedent/ui/alert';
import { HlmBadge } from '@mousedent/ui/badge';
import { HlmButton } from '@mousedent/ui/button';
import { LocalIcon } from './local-icon';
import {
  DAY_NAMES,
  DAY_SHORT,
  SCHEDULE_PRESETS,
  WEEK_DAYS,
  applyRange,
  barPosition,
  closeDays,
  hoursLabel,
  openDayCount,
  rangeProblem,
  type SchedulePreset,
} from './schedule-model';

/**
 * Editor de horario por grupos. Selecciona días, aplica un tramo o los cierra y
 * muestra el resumen semanal. Aplicar no guarda el local: solo cambia el
 * borrador que el formulario enviará al guardar.
 */
@Component({
  selector: 'app-schedule-editor',
  imports: [HlmAlert, HlmBadge, HlmButton, LocalIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './schedule-editor.html',
})
export class ScheduleEditor {
  readonly week = input.required<readonly LocalScheduleDay[]>();
  readonly dayErrors = input<Partial<Record<LocalWeekDay, string>>>({});

  readonly weekChange = output<LocalScheduleDay[]>();

  protected readonly days = WEEK_DAYS;
  protected readonly dayNames = DAY_NAMES;
  protected readonly dayShort = DAY_SHORT;
  protected readonly presets = SCHEDULE_PRESETS;

  protected readonly selected = signal<ReadonlySet<LocalWeekDay>>(new Set(WEEK_DAYS.slice(0, 5)));
  protected readonly opensAt = signal('09:00');
  protected readonly closesAt = signal('18:00');
  protected readonly preset = signal<SchedulePreset['id'] | null>('full');
  protected readonly error = signal<string | null>(null);

  protected readonly openDays = computed(() => openDayCount(this.week()));
  protected readonly selectionLabel = computed(() => {
    const count = this.selected().size;

    return count === 1 ? '1 día' : `${count} días`;
  });

  protected toggleDay(day: LocalWeekDay): void {
    const next = new Set(this.selected());

    if (next.has(day)) {
      next.delete(day);
    } else {
      next.add(day);
    }

    this.selected.set(next);
  }

  protected selectDays(days: readonly LocalWeekDay[]): void {
    this.selected.set(new Set(days));
  }

  protected choosePreset(preset: SchedulePreset): void {
    this.opensAt.set(preset.opensAt);
    this.closesAt.set(preset.closesAt);
    this.preset.set(preset.id);
    this.error.set(null);
  }

  protected onTime(which: 'open' | 'close', event: Event): void {
    const value = (event.target as HTMLInputElement).value;

    (which === 'open' ? this.opensAt : this.closesAt).set(value);
    this.preset.set(null);
    this.error.set(null);
  }

  /** Aplica el tramo al borrador; el guardado sigue pendiente. */
  protected applyRange(): void {
    if (this.selected().size === 0) {
      return;
    }

    const problem = rangeProblem(this.opensAt(), this.closesAt());
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.error.set(null);
    this.weekChange.emit(applyRange(this.week(), this.selected(), this.opensAt(), this.closesAt()));
  }

  /** Marca los días elegidos como cerrados; tampoco guarda el local. */
  protected closeSelected(): void {
    if (this.selected().size === 0) {
      return;
    }

    this.error.set(null);
    this.weekChange.emit(closeDays(this.week(), this.selected()));
  }

  protected hoursOf(day: LocalScheduleDay): string {
    return hoursLabel(day);
  }

  protected barOf(day: LocalScheduleDay): { left: number; width: number } {
    return barPosition(day.opensAt, day.closesAt);
  }

  protected errorOf(day: LocalScheduleDay): string | undefined {
    return this.dayErrors()[day.day];
  }
}
