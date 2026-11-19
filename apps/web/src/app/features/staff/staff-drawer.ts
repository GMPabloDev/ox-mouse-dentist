import { CdkTrapFocus } from '@angular/cdk/a11y';
import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

/**
 * Panel lateral de la variante A: el detalle y el formulario se abren a la
 * derecha en escritorio y a ancho completo en móvil. Bloquea el resto de la
 * página, atrapa el foco y se cierra con Escape o con el fondo.
 */
@Component({
  selector: 'app-staff-drawer',
  imports: [CdkTrapFocus],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'onEscape()' },
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-[70] flex justify-end bg-brand-ink/45 backdrop-blur-[2px]"
        [attr.inert]="blocked() ? '' : null"
        (click)="closed.emit()"
      >
        <aside
          #panel
          cdkTrapFocus
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="labelledBy()"
          tabindex="-1"
          class="flex h-dvh w-full flex-col overflow-hidden bg-card shadow-xl outline-none sm:max-w-[530px]"
          (click)="$event.stopPropagation()"
        >
          <ng-content />
        </aside>
      </div>
    }
  `,
})
export class StaffDrawer {
  readonly open = input(false);
  readonly labelledBy = input.required<string>();
  /** Una confirmación abierta sobre el panel lo deja fuera del foco. */
  readonly blocked = input(false);
  readonly closed = output<void>();

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private readonly document = inject(DOCUMENT);
  private previousFocus: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (this.open()) {
        this.previousFocus = this.document.activeElement as HTMLElement | null;
        this.panel()?.nativeElement.focus({ preventScroll: true });
        return;
      }

      // Al cerrar, devuelve el foco a quien abrió el panel.
      if (this.previousFocus?.isConnected) {
        this.previousFocus.focus({ preventScroll: true });
      }
      this.previousFocus = null;
    });
  }

  protected onEscape(): void {
    if (this.open() && !this.blocked()) {
      this.closed.emit();
    }
  }
}
