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
import { LocalIcon } from './local-icon';

/**
 * Vista completa de la foto, sin recorte. Muestra el original que ya está
 * cargado, así que no vuelve a pedirlo a la API.
 */
@Component({
  selector: 'app-photo-viewer',
  imports: [CdkTrapFocus, HlmButton, LocalIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'onEscape()' },
  template: `
    @if (open()) {
      @if (url(); as source) {
        <div
          class="fixed inset-0 z-[70] grid place-items-center bg-brand-ink/60 p-4"
          (click)="closed.emit()"
        >
          <div
            cdkTrapFocus
            role="dialog"
            aria-modal="true"
            aria-labelledby="photo-viewer-title"
            class="w-full max-w-[1000px] rounded-[18px] border border-border bg-card p-4 shadow-xl md:p-5"
            (click)="$event.stopPropagation()"
          >
            <div class="flex items-center justify-between gap-3">
              <h2
                id="photo-viewer-title"
                class="font-display text-[14px] font-extrabold tracking-tight text-foreground md:text-[17px]"
              >
                {{ title() }}
              </h2>
              <button
                #close
                hlmBtn
                variant="ghost"
                size="icon"
                type="button"
                aria-label="Cerrar la foto"
                (click)="closed.emit()"
              >
                <app-local-icon name="close" class="size-5" />
              </button>
            </div>
            <img
              class="mt-2.5 h-[65dvh] w-full rounded-[9px] bg-surface-green object-contain"
              [src]="source"
              [alt]="title()"
            />
          </div>
        </div>
      }
    }
  `,
})
export class PhotoViewer {
  readonly open = input(false);
  readonly url = input<string | null>(null);
  readonly title = input('Foto del local');
  readonly closed = output<void>();

  private readonly closeButton = viewChild<ElementRef<HTMLButtonElement>>('close');

  constructor() {
    effect(() => {
      if (this.open() && this.closeButton()) {
        this.closeButton()!.nativeElement.focus();
      }
    });
  }

  protected onEscape(): void {
    if (this.open()) {
      this.closed.emit();
    }
  }
}
