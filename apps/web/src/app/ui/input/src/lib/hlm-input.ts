import { Directive } from '@angular/core';
import { BrnFieldControlDescribedBy } from '@spartan-ng/brain/field';
import { BrnInput } from '@spartan-ng/brain/input';
import { classes } from '@mousedent/ui/utils';

@Directive({
  selector: '[hlmInput]',
  exportAs: 'hlmInput',
  hostDirectives: [
    { directive: BrnInput, inputs: ['id', 'forceInvalid'] },
    { directive: BrnFieldControlDescribedBy, inputs: ['aria-describedby'] },
  ],
  host: { 'data-slot': 'input' },
})
export class HlmInput {
  constructor() {
    // Medidas de la referencia: 48 px de alto, radio 9 px y texto de 15 px.
    classes(
      () =>
        'border-input bg-card text-foreground placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/40 h-12 w-full min-w-0 rounded-[9px] border px-[13px] py-[11px] text-[15px] transition-colors outline-none focus-visible:ring-2 disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 data-[matches-spartan-invalid=true]:border-destructive data-[matches-spartan-invalid=true]:ring-destructive/30',
    );
  }
}
