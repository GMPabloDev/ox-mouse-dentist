import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Mascota de la identidad «Abrazo». Es solo decorativa: siempre acompaña a un
 * texto visible, así que se oculta a los lectores de pantalla.
 */
@Component({
  selector: 'app-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0' },
  template: `
    <svg class="h-full w-auto" aria-hidden="true" viewBox="0 0 50 52" focusable="false">
      <path
        d="M10 23C12 14 14 4 26 4c11 0 10 12 15 17 7 8 6 23-4 26-5 2-8-2-12-1-7 3-17 4-19-4-2-6 1-13 4-19"
        fill="#99DAD0"
      />
      <path d="M10 23C3 19 0 24 4 31l6 7M39 22c7-7 12-1 6 6l-5 6" fill="#137F79" />
      <path
        d="M16 23q3-6 6 0M29 22q3-6 6 0"
        fill="none"
        stroke="#215651"
        stroke-width="2.6"
        stroke-linecap="round"
      />
      <path d="M21 29q6 8 11-1" fill="#EF756A" />
      <circle cx="15" cy="28" r="3" fill="#FFE0CA" />
      <circle cx="36" cy="27" r="3" fill="#FFE0CA" />
    </svg>
  `,
})
export class BrandMark {}
