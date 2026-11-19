import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Trazos de la identidad «Abrazo» que usa la gestión del personal. */
const PATHS = {
  alert: 'M12 3l10 18H2L12 3 M12 9v5 M12 17h.01',
  check: 'M5 12l4 4L19 6',
  chevron: 'M9 5l7 7-7 7',
  close: 'M6 6l12 12 M18 6L6 18',
  edit: 'M15 4l5 5 M4 16l-1 5 5-1L21 7l-5-5z',
  info: 'M12 11v6 M12 7h.01 M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
  lock: 'M6 10h12v11H6z M8 10V6a4 4 0 0 1 8 0v4',
  logout: 'M9 4H3v16h6 M10 12h11 M16 7l5 5-5 5',
  mail: 'M3 5h18v14H3z M3 5l9 8 9-8',
  pin: 'M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0 M15 10a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  plus: 'M12 5v14 M5 12h14',
  refresh: 'M20 7a8 8 0 1 0 1 8 M20 3v5h-5',
  search: 'M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
  shield: 'M12 3l8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3 M9 12l2 2 4-4',
  users:
    'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8 M22 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75',
} as const;

export type StaffIconName = keyof typeof PATHS;

/** Icono decorativo: siempre acompaña a un texto visible o a una etiqueta accesible. */
@Component({
  selector: 'app-staff-icon',
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
export class StaffIcon {
  readonly name = input.required<StaffIconName>();

  protected readonly path = computed(() => PATHS[this.name()]);
}
