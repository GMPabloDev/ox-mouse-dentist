import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BrnSelectScrollDown } from '@spartan-ng/brain/select';
import { classes } from '@mousedent/ui/utils';

@Component({
  selector: 'hlm-select-scroll-down',
  exportAs: 'hlmSelectScrollDown',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  hostDirectives: [BrnSelectScrollDown],
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
      <path d="M6 9l6 6 6-6" />
    </svg>
  `,
})
export class HlmSelectScrollDown {
  constructor() {
    classes(
      () =>
        'bg-popover text-muted-foreground sticky bottom-0 z-10 flex w-full cursor-default items-center justify-center py-1 data-hidden:hidden',
    );
  }
}
