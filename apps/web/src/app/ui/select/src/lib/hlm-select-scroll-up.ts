import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BrnSelectScrollUp } from '@spartan-ng/brain/select';
import { classes } from '@mousedent/ui/utils';

@Component({
  selector: 'hlm-select-scroll-up',
  exportAs: 'hlmSelectScrollUp',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  hostDirectives: [BrnSelectScrollUp],
  template: `
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
      <path d="M6 15l6-6 6 6" />
    </svg>
  `,
})
export class HlmSelectScrollUp {
  constructor() {
    classes(
      () =>
        'bg-popover text-muted-foreground sticky top-0 z-10 flex w-full cursor-default items-center justify-center py-1 data-hidden:hidden',
    );
  }
}
