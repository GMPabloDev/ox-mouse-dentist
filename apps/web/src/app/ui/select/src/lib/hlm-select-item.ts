import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { BrnSelectItem } from '@spartan-ng/brain/select';
import { classes } from '@mousedent/ui/utils';

@Component({
  selector: 'hlm-select-item',
  exportAs: 'hlmSelectItem',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  hostDirectives: [{ directive: BrnSelectItem, inputs: ['id', 'disabled', 'value'] }],
  host: { 'data-slot': 'select-item' },
  template: `
    <ng-content />
    @if (_active()) {
      <svg
        class="text-primary absolute end-2 flex size-4 items-center justify-center"
        aria-hidden="true"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="M5 12l4 4L19 6" />
      </svg>
    }
  `,
})
export class HlmSelectItem {
  private readonly _brnSelectItem = inject(BrnSelectItem);

  protected readonly _active = this._brnSelectItem.active;

  constructor() {
    classes(
      () =>
        'data-highlighted:bg-secondary data-highlighted:text-secondary-foreground relative flex w-full cursor-default items-center gap-2 rounded-md py-1.5 ps-2 pe-8 text-[13px] outline-none select-none data-disabled:pointer-events-none data-disabled:opacity-50',
    );
  }
}
