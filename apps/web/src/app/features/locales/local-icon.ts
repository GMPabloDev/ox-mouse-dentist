import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Trazos de la identidad «Abrazo» que usa la gestión de locales. */
const PATHS = {
  'arrow-left': 'M19 12H5 M11 6l-6 6 6 6',
  building: 'M4 21V5l8-3 8 3v16 M2 21h20 M9 21v-5h6v5 M8 7h1 M15 7h1 M8 11h1 M15 11h1',
  check: 'M5 12l4 4L19 6',
  close: 'M6 6l12 12 M18 6L6 18',
  edit: 'M15 4l5 5 M4 16l-1 5 5-1L21 7l-5-5z',
  expand: 'M8 3H3v5 M16 3h5v5 M3 16v5h5 M21 16v5h-5',
  image: 'M3 3h18v18H3z M3 16l6-6 6 6 3-3 3 3 M15 7h.01',
  info: 'M12 11v6 M12 7h.01 M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
  lock: 'M6 10h12v11H6z M8 10V6a4 4 0 0 1 8 0v4',
  phone: 'M7 3H3c0 10 8 18 18 18v-4l-5-2-2 2a17 17 0 0 1-7-7l2-2z',
  plus: 'M12 5v14 M5 12h14',
  upload: 'M12 16V3 M7 8l5-5 5 5 M4 15v6h16v-6',
} as const;

export type LocalIconName = keyof typeof PATHS;

/** Icono decorativo: siempre acompaña a un texto visible o a una etiqueta accesible. */
@Component({
  selector: 'app-local-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0' },
  template: `
    <svg
      class="size-full"
      aria-hidden="true"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <path [attr.d]="path()" />
    </svg>
  `,
})
export class LocalIcon {
  readonly name = input.required<LocalIconName>();

  protected readonly path = computed(() => PATHS[this.name()]);
}
