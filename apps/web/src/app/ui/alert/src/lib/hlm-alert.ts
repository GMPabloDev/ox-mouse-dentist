import { Directive, input } from '@angular/core';
import { classes } from '@mousedent/ui/utils';
import { cva, type VariantProps } from 'class-variance-authority';

const alertVariants = cva(
  'group/alert relative grid w-full gap-1 rounded-xl border px-4 py-3 text-start text-sm',
  {
    variants: {
      variant: {
        default: 'border-border bg-card text-card-foreground',
        destructive: 'border-destructive/40 bg-destructive/10 text-destructive',
        success: 'border-success/30 bg-success/10 text-success',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
);

export type AlertVariants = VariantProps<typeof alertVariants>;

@Directive({
  selector: 'hlm-alert,[hlmAlert]',
  exportAs: 'hlmAlert',
  host: {
    'data-slot': 'alert',
    '[attr.role]': 'role()',
  },
})
export class HlmAlert {
  public readonly variant = input<AlertVariants['variant']>('default');

  /** Los errores son `alert`; las confirmaciones breves, `status`. */
  public readonly role = input<'alert' | 'status'>('alert');

  constructor() {
    classes(() => alertVariants({ variant: this.variant() }));
  }
}
