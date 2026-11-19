import { CdkTrapFocus } from '@angular/cdk/a11y';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { HlmButton } from '@mousedent/ui/button';

/**
 * Confirmación de las acciones del personal que cambian datos o acceso. También
 * sirve para explicar una acción bloqueada: en ese caso solo muestra el botón de
 * cierre.
 */
@Component({
  selector: 'app-staff-confirm',
  imports: [CdkTrapFocus, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'onEscape()' },
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-[80] grid place-items-center overflow-y-auto bg-brand-ink/45 p-4"
        (click)="cancelled.emit()"
      >
        <div
          cdkTrapFocus
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="staff-dialog-title"
          aria-describedby="staff-dialog-message"
          class="w-full max-w-[440px] rounded-[18px] border border-border bg-card p-7 shadow-xl"
          (click)="$event.stopPropagation()"
        >
          <h2
            id="staff-dialog-title"
            class="font-display text-[21px] font-extrabold tracking-[-0.4px] text-foreground"
          >
            {{ title() }}
          </h2>
          <p id="staff-dialog-message" class="mt-3 mb-[22px] text-sm text-muted-foreground">
            {{ message() }}
          </p>
          <div class="flex flex-wrap justify-end gap-2">
            <button #cancel hlmBtn variant="outline" type="button" (click)="cancelled.emit()">
              {{ acknowledgeOnly() ? 'Entendido' : cancelLabel() }}
            </button>
            @if (!acknowledgeOnly()) {
              <button
                hlmBtn
                type="button"
                [variant]="danger() ? 'outline' : 'default'"
                [class]="
                  danger() ? 'border-destructive/50 text-destructive hover:bg-destructive/10' : ''
                "
                (click)="confirmed.emit()"
              >
                {{ confirmLabel() }}
              </button>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class StaffConfirm {
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly message = input('');
  readonly confirmLabel = input('Confirmar');
  readonly cancelLabel = input('Cancelar');
  /** Un aviso bloqueante solo se cierra: no ofrece confirmar. */
  readonly acknowledgeOnly = input(false);
  readonly danger = input(false);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancel');

  constructor() {
    effect(() => {
      if (this.open() && this.cancelButton()) {
        this.cancelButton()!.nativeElement.focus({ preventScroll: true });
      }
    });
  }

  protected onEscape(): void {
    if (this.open()) {
      this.cancelled.emit();
    }
  }
}
