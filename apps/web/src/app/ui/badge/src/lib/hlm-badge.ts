import { Directive, input } from '@angular/core';
import { classes } from '@mousedent/ui/utils';
import { type VariantProps, cva } from 'class-variance-authority';

// Medidas de la referencia: radio 7 px, texto de 12 px con peso 700 y padding 6 px 10 px.
const badgeVariants = cva(
  'group/badge focus-visible:ring-ring inline-flex w-fit shrink-0 items-center justify-center gap-1.5 overflow-hidden rounded-[7px] px-2.5 py-1.5 text-xs font-bold whitespace-nowrap transition-all focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none',
  {
    variants: {
      variant: {
        default: 'bg-secondary text-secondary-foreground [a]:hover:bg-secondary/70',
        neutral: 'bg-muted text-muted-foreground [a]:hover:bg-muted/70',
        primary: 'bg-primary text-primary-foreground [a]:hover:bg-primary/90',
        destructive: 'bg-destructive/10 text-destructive [a]:hover:bg-destructive/20',
        outline: 'border-border text-foreground border [a]:hover:bg-muted',
        ghost: 'hover:bg-muted hover:text-muted-foreground',
        link: 'text-primary underline-offset-4 hover:underline',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
);

export type BadgeVariants = VariantProps<typeof badgeVariants>;

@Directive({
  selector: '[hlmBadge],hlm-badge',
  exportAs: 'hlmBadge',
  host: {
    'data-slot': 'badge',
    '[attr.data-variant]': 'variant()',
  },
})
export class HlmBadge {
  public readonly variant = input<BadgeVariants['variant']>('default');

  constructor() {
    classes(() => badgeVariants({ variant: this.variant() }));
  }
}
