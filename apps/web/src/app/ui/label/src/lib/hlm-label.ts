import { Directive } from '@angular/core';
import { BrnLabel } from '@spartan-ng/brain/label';
import { classes } from '@mousedent/ui/utils';

@Directive({
  selector: '[hlmLabel]',
  exportAs: 'hlmLabel',
  hostDirectives: [{ directive: BrnLabel, inputs: ['id', 'for'] }],
  host: { 'data-slot': 'label' },
})
export class HlmLabel {
  constructor() {
    classes(
      () =>
        'text-foreground block text-[13px] leading-none font-bold group-data-[disabled=true]:opacity-50 peer-disabled:opacity-50 select-none group-data-[disabled=true]:pointer-events-none peer-disabled:cursor-not-allowed',
    );
  }
}
