import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Assignment } from './profesional-list';

/**
 * Especialidades o locales de atención como chips. Una asignación inactiva se
 * conserva y se marca: el catálogo o el local se desactivaron después de asignarla.
 */
@Component({
  selector: 'app-profesional-chips',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="flex flex-wrap gap-1.5">
      @for (item of items(); track item.id) {
        <span
          class="inline-flex max-w-full items-center gap-1.5 rounded-[6px] border px-1.5 py-[3px] text-[11px] font-semibold [overflow-wrap:anywhere]"
          [class]="
            item.isActive
              ? 'border-border-soft bg-surface-green text-foreground'
              : 'border-dashed border-border-strong bg-muted text-muted-foreground'
          "
        >
          {{ item.name }}
          @if (!item.isActive) {
            <span
              class="rounded-[4px] bg-warning px-1 py-px text-[9px] font-extrabold tracking-wide text-warning-foreground uppercase"
            >
              {{ inactiveFlag() }}
            </span>
          }
        </span>
      } @empty {
        <span class="text-[11px] text-muted-foreground italic">Sin asignar</span>
      }
    </span>
  `,
})
export class ProfesionalChips {
  readonly items = input.required<readonly Assignment[]>();
  readonly kind = input.required<'especialidad' | 'local'>();

  protected readonly inactiveFlag = computed(() =>
    this.kind() === 'especialidad' ? 'Inactiva' : 'Inactivo',
  );
}
