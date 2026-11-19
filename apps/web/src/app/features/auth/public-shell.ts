import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { BrandMark } from '../../core/brand/brand-mark';

/**
 * Armazón de las pantallas públicas de recuperación y contraseña. Reproduce la
 * composición del prototipo de acceso: panel narrativo con la marca, la mascota
 * y el pie de acceso exclusivo, junto a un formulario centrado. Por debajo de
 * 741 px el relato se oculta y queda la banda de marca, igual que en la
 * referencia.
 */
@Component({
  selector: 'app-public-shell',
  imports: [BrandMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="grid grid-cols-1 bg-background min-[741px]:grid-cols-2">
      <aside
        class="relative flex flex-col overflow-hidden bg-secondary px-6 py-7 min-[741px]:min-h-dvh min-[741px]:px-[54px] min-[741px]:py-[42px]"
      >
        <span
          aria-hidden="true"
          class="pointer-events-none absolute -bottom-[220px] -left-[160px] size-[500px] rotate-[25deg] rounded-[45%_55%_60%_40%] border border-border-mint"
        ></span>
        <span
          aria-hidden="true"
          class="pointer-events-none absolute -top-[200px] -right-[100px] size-[360px] rounded-full border border-border-mint"
        ></span>

        <div class="relative flex items-center gap-2.5">
          <app-brand-mark class="h-10" />
          <span class="font-display text-2xl font-extrabold tracking-tight">
            <span class="text-brand-ink">Mouse</span><span class="text-primary">Dent</span>
            <small class="block font-sans text-[11px] font-semibold text-muted-foreground">
              Panel del personal
            </small>
          </span>
        </div>

        <div class="relative my-auto hidden max-w-[400px] py-10 min-[741px]:block">
          <div class="mb-6 -ml-3">
            <app-brand-mark class="h-[190px]" />
          </div>
          <h2
            class="max-w-[340px] font-display text-[30px] leading-[1.2] font-extrabold tracking-[-0.7px] text-secondary-foreground min-[951px]:text-[36px]"
          >
            {{ title() }}
          </h2>
          <p class="mt-[18px] max-w-[345px] text-[15px] text-muted-foreground">{{ subtitle() }}</p>
        </div>

        <p
          class="relative hidden items-center gap-2 text-xs text-muted-foreground min-[741px]:flex"
        >
          <svg
            class="size-4"
            aria-hidden="true"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <path d="M6 10h12v11H6z M8 10V7a4 4 0 0 1 8 0v3" />
          </svg>
          Acceso exclusivo para el personal de MouseDent
        </p>
      </aside>

      <section
        class="flex items-start justify-center bg-background px-6 pt-9 pb-12 min-[741px]:min-h-dvh min-[741px]:items-center min-[741px]:px-10 min-[741px]:py-14"
      >
        <div class="w-full max-w-[410px]">
          <ng-content />
        </div>
      </section>
    </main>
  `,
})
export class PublicShell {
  readonly title = input.required<string>();
  readonly subtitle = input.required<string>();
}
