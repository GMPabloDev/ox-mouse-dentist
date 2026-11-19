import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { StaffAccountResponse } from '@mousedent/api-client';
import { statusLabel } from './staff-form';
import { StaffIcon } from './staff-icon';

/**
 * Estado visible de una cuenta. Un fallo de correo conocido en esta visita
 * sustituye al estado: la cuenta sigue pendiente, pero administración necesita
 * verlo para reenviar la invitación.
 */
@Component({
  selector: 'app-staff-status',
  imports: [StaffIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0' },
  template: `
    <span
      class="inline-flex items-center gap-1.5 rounded-[7px] px-2 py-1 text-[11px] font-extrabold whitespace-nowrap"
      [class]="classes()"
    >
      @if (mailFailed()) {
        <app-staff-icon name="mail" class="size-3.5" />
        Correo no enviado
      } @else {
        <span class="size-1.5 rounded-full bg-current" aria-hidden="true"></span>
        {{ label() }}
      }
    </span>
  `,
})
export class StaffStatus {
  readonly account = input.required<StaffAccountResponse>();
  readonly mailFailed = input(false);

  protected readonly label = computed(() => statusLabel(this.account().status));

  protected readonly classes = computed(() => {
    if (this.mailFailed()) {
      return 'border border-destructive/30 bg-destructive/10 text-destructive';
    }

    switch (this.account().status) {
      case 'Activa':
        return 'bg-secondary text-secondary-foreground';
      case 'Pendiente':
        return 'bg-warning text-warning-foreground';
      case 'Inactiva':
        return 'bg-muted text-muted-foreground';
    }
  });
}
