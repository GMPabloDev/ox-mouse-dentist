import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { StaffIcon } from './staff-icon';

export type StaffNoticeTone = 'neutral' | 'warn' | 'error';

const TONES: Readonly<Record<StaffNoticeTone, string>> = {
  neutral: 'border-border bg-muted/60 text-foreground',
  warn: 'border-warning bg-warning/40 text-warning-foreground',
  error: 'border-destructive/40 bg-destructive/10 text-destructive',
};

/**
 * Aviso dentro del detalle y del formulario. `error` se anuncia como alerta; los
 * avisos informativos acompañan al contenido sin interrumpir.
 */
@Component({
  selector: 'app-staff-notice',
  imports: [StaffIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="flex gap-2.5 rounded-[11px] border px-3.5 py-3 text-[13px]"
      [class]="classes()"
      [attr.role]="tone() === 'error' ? 'alert' : null"
    >
      <app-staff-icon [name]="tone() === 'neutral' ? 'info' : 'alert'" class="mt-0.5 size-[18px]" />
      <div class="min-w-0">
        <strong class="block text-[13px]">{{ title() }}</strong>
        <div class="mt-0.5 text-[13px]"><ng-content /></div>
      </div>
    </div>
  `,
})
export class StaffNotice {
  readonly tone = input<StaffNoticeTone>('neutral');
  readonly title = input.required<string>();

  protected readonly classes = computed(() => TONES[this.tone()]);
}
