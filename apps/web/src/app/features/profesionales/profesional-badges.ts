import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { StaffAccountState } from '@mousedent/api-client';
import { StaffIcon } from '../staff/staff-icon';
import { accountStatusLabel } from './profesional-list';

/**
 * Estado de la ficha. Vive en la tabla y en la ficha, con el contexto en el texto
 * para que se lea sin depender del encabezado de la columna.
 */
@Component({
  selector: 'app-profesional-ficha-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0' },
  template: `
    <span
      class="inline-flex items-center gap-1.5 rounded-[7px] px-2 py-1 text-[11px] font-extrabold whitespace-nowrap"
      [class]="
        active() ? 'bg-secondary text-secondary-foreground' : 'bg-muted text-muted-foreground'
      "
    >
      <span class="size-1.5 rounded-full bg-current" aria-hidden="true"></span>
      {{ active() ? 'Ficha activa' : 'Ficha inactiva' }}
    </span>
  `,
})
export class ProfesionalFichaStatus {
  readonly active = input.required<boolean>();
}

/**
 * Estado de la cuenta en la lista y en la ficha. Una cuenta pendiente conserva un
 * solo estado: ámbar normal y rosa con icono cuando el correo de invitación falló.
 * El resumen de la lista cuenta los correos sin enviar y la ficha ofrece reenviar.
 */
@Component({
  selector: 'app-profesional-account-status',
  imports: [StaffIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0' },
  template: `
    <span
      class="inline-flex items-center gap-1.5 rounded-[7px] px-2 py-1 text-[11px] font-extrabold whitespace-nowrap"
      [class]="classes()"
      [attr.title]="mailFailed() ? 'Correo de invitación no enviado' : null"
    >
      @if (mailFailed()) {
        <app-staff-icon name="alert" class="size-3.5" />
        {{ label() }}<span class="sr-only">. Correo no enviado.</span>
      } @else {
        <span class="size-1.5 rounded-full bg-current" aria-hidden="true"></span>
        {{ label() }}
      }
    </span>
  `,
})
export class ProfesionalAccountStatus {
  readonly status = input.required<StaffAccountState>();
  readonly invitationFailed = input(false);

  protected readonly label = computed(() => accountStatusLabel(this.status()));

  /** La marca de correo fallido solo tiene sentido mientras la cuenta siga pendiente. */
  protected readonly mailFailed = computed(
    () => this.status() === 'Pendiente' && this.invitationFailed(),
  );

  protected readonly classes = computed(() => {
    if (this.mailFailed()) {
      return 'border border-invitation-failed-border bg-invitation-failed-soft text-invitation-failed';
    }

    switch (this.status()) {
      case 'Activa':
        return 'bg-secondary text-secondary-foreground';
      case 'Pendiente':
        return 'bg-warning text-warning-foreground';
      case 'Inactiva':
        return 'bg-muted text-muted-foreground';
    }
  });
}
