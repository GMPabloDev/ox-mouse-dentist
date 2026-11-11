import { Directive } from '@angular/core';
import { HlmLabel } from '@mousedent/ui/label';
import { classes } from '@mousedent/ui/utils';

@Directive({
  selector: '[hlmFieldLabel],hlm-field-label',
  exportAs: 'hlmFieldLabel',
  hostDirectives: [HlmLabel],
  host: { 'data-slot': 'field-label' },
})
export class HlmFieldLabel {
  constructor() {
    classes(() => [
      'group-data-[disabled=true]/field:opacity-50 group/field-label peer/field-label w-fit',
      'has-[>[data-slot=field]]:flex-col',
    ]);
  }
}
