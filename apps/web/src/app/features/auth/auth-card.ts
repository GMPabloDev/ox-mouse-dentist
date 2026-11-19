import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BrandMark } from '../../core/brand/brand-mark';

/**
 * Tarjeta de las pantallas públicas de acceso. Mantiene la identidad «Abrazo» y
 * deja el contenido de cada recorrido al componente que la usa.
 */
@Component({
  selector: 'app-auth-card',
  imports: [BrandMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="grid min-h-dvh place-items-center bg-background px-4 py-10">
      <section
        class="w-full max-w-[420px] rounded-2xl border border-border bg-card px-6 py-8 shadow-sm sm:px-9 sm:py-9"
      >
        <div class="flex items-center gap-2.5 pb-7">
          <app-brand-mark class="h-10" />
          <span class="font-display text-2xl font-extrabold tracking-tight">
            <span class="text-brand-ink">Mouse</span><span class="text-primary">Dent</span>
          </span>
        </div>
        <ng-content />
      </section>
    </main>
  `,
})
export class AuthCard {}
