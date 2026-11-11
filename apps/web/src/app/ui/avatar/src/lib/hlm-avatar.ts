import { Directive, input } from '@angular/core';
import { BrnAvatar } from '@spartan-ng/brain/avatar';
import { classes } from '@mousedent/ui/utils';
import { cva, type VariantProps } from 'class-variance-authority';

const avatarVariants = cva(
  'group/avatar after:border-border relative flex shrink-0 select-none after:absolute after:inset-0 after:border after:mix-blend-darken dark:after:mix-blend-lighten',
  {
    variants: {
      // `photo` reproduce la miniatura de la referencia: 86×92 en móvil, 112×90 y 132×96.
      size: {
        default: 'size-8 rounded-full after:rounded-full',
        sm: 'size-6 rounded-full after:rounded-full',
        lg: 'size-10 rounded-full after:rounded-full',
        photo:
          'h-[92px] w-[86px] rounded-lg after:hidden md:h-[90px] md:w-[105px] xl:h-24 xl:w-[132px]',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  },
);

export type AvatarVariants = VariantProps<typeof avatarVariants>;

@Directive({
  selector: '[hlmAvatar],hlm-avatar',
  exportAs: 'hlmAvatar',
  hostDirectives: [BrnAvatar],
  host: {
    'data-slot': 'avatar',
    '[attr.data-size]': 'size()',
  },
})
export class HlmAvatar {
  public readonly size = input<AvatarVariants['size']>('default');

  constructor() {
    classes(() => avatarVariants({ size: this.size() }));
  }
}
