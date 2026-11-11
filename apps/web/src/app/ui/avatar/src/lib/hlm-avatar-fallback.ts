import { Directive } from '@angular/core';
import { BrnAvatarFallback } from '@spartan-ng/brain/avatar';
import { classes } from '@mousedent/ui/utils';

@Directive({
  selector: '[hlmAvatarFallback]',
  exportAs: 'hlmAvatarFallback',
  hostDirectives: [BrnAvatarFallback],
  host: {
    'data-slot': 'avatar-fallback',
  },
})
export class HlmAvatarFallback {
  constructor() {
    classes(
      () =>
        'flex size-full items-center justify-center text-sm group-data-[size=sm]/avatar:text-xs',
    );
  }
}
