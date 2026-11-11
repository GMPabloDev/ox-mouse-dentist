import { Directive, input, signal } from '@angular/core';
import { BrnButton } from '@spartan-ng/brain/button';
import { classes } from '@mousedent/ui/utils';
import { cva, type VariantProps } from 'class-variance-authority';
import type { ClassValue } from 'clsx';
import { injectBrnButtonConfig } from './hlm-button.token';

// Medidas de la referencia: 44 px de alto mínimo, padding 10 px 15 px, radio 10 px y peso 700.
export const buttonVariants = cva(
  'focus-visible:ring-ring focus-visible:ring-offset-background group/button inline-flex shrink-0 items-center justify-center gap-2 rounded-[10px] border font-bold whitespace-nowrap transition-colors outline-none select-none focus-visible:ring-2 focus-visible:ring-offset-2 data-disabled:pointer-events-none data-disabled:opacity-60 disabled:cursor-not-allowed',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground border-transparent hover:bg-primary/90',
        soft: 'bg-secondary text-secondary-foreground border-transparent hover:bg-secondary/70',
        outline: 'border-border bg-card text-foreground hover:bg-muted',
        ghost: 'border-transparent bg-transparent text-foreground hover:bg-muted',
        text: 'border-transparent bg-transparent px-1 text-primary hover:bg-secondary',
        destructive: 'border-transparent bg-transparent text-destructive hover:bg-destructive/10',
        link: 'border-transparent bg-transparent text-primary underline-offset-4 hover:underline',
      },
      size: {
        default: 'min-h-11 px-[15px] py-[10px] text-sm',
        sm: 'min-h-11 px-[10px] py-[9px] text-xs',
        xs: 'min-h-[30px] px-[6px] py-[5px] text-[11px]',
        link: 'min-h-7 px-0 py-0 text-[13px] font-semibold text-muted-foreground',
        chip: 'min-h-[38px] px-[10px] py-[8px] text-xs',
        icon: 'size-11 p-0',
        'icon-sm': 'size-9 p-0',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
);

export type ButtonVariants = VariantProps<typeof buttonVariants>;

@Directive({
  selector: 'button[hlmBtn], a[hlmBtn]',
  exportAs: 'hlmBtn',
  hostDirectives: [{ directive: BrnButton, inputs: ['disabled'] }],
  host: { 'data-slot': 'button' },
})
export class HlmButton {
  private readonly _config = injectBrnButtonConfig();

  private readonly _additionalClasses = signal<ClassValue>('');

  public readonly variant = input<ButtonVariants['variant']>(this._config.variant);

  public readonly size = input<ButtonVariants['size']>(this._config.size);

  constructor() {
    classes(() => [
      buttonVariants({ variant: this.variant(), size: this.size() }),
      this._additionalClasses(),
    ]);
  }

  setClass(classes: string): void {
    this._additionalClasses.set(classes);
  }
}
