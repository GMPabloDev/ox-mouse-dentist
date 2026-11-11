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
 * Confirmación para acciones que cambian datos. Se usa antes de desactivar o
 * reactivar un local y antes de descartar un formulario con cambios.
 */
@Component({
  selector: 'app-confirm-dialog',
  imports: [CdkTrapFocus, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'onEscape()' },
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-[70] grid place-items-center overflow-y-auto bg-brand-ink/45 p-4"
        (click)="cancelled.emit()"
      >
        <div
          cdkTrapFocus
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirm-dialog-title"
          aria-describedby="confirm-dialog-message"
          class="w-full max-w-[440px] rounded-[18px] border border-border bg-card p-7 shadow-xl"
          (click)="$event.stopPropagation()"
        >
          <h2
            id="confirm-dialog-title"
            class="font-display text-[21px] font-extrabold tracking-[-0.4px] text-foreground"
          >
            {{ title() }}
          </h2>
          <p id="confirm-dialog-message" class="mt-3 mb-[22px] text-sm text-muted-foreground">
            {{ message() }}
          </p>
          <div class="flex flex-wrap justify-end gap-2">
            <button #cancel hlmBtn variant="outline" type="button" (click)="cancelled.emit()">
              {{ cancelLabel() }}
            </button>
            <button hlmBtn type="button" (click)="confirmed.emit()">
              {{ confirmLabel() }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialog {
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly message = input('');
  readonly confirmLabel = input('Confirmar');
  readonly cancelLabel = input('Cancelar');
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancel');

  constructor() {
    effect(() => {
      if (this.open() && this.cancelButton()) {
        this.cancelButton()!.nativeElement.focus();
      }
    });
  }

  protected onEscape(): void {
    if (this.open()) {
      this.cancelled.emit();
    }
  }
}
