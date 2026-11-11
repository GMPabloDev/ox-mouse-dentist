import type { BooleanInput } from '@angular/cdk/coercion';
import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { BrnFieldControlDescribedBy } from '@spartan-ng/brain/field';
import { BrnSelectTrigger } from '@spartan-ng/brain/select';
import { hlm } from '@mousedent/ui/utils';
import type { ClassValue } from 'clsx';

@Component({
  selector: 'hlm-select-trigger',
  exportAs: 'hlmSelectTrigger',
  imports: [BrnSelectTrigger, BrnFieldControlDescribedBy],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      brnSelectTrigger
      brnFieldControlDescribedBy
      [forceInvalid]="forceInvalid()"
      [id]="buttonId()"
      [class]="_computedClass()"
      [aria-invalid]="ariaInvalidInput()"
      [attr.data-size]="size()"
      data-slot="select-trigger"
    >
      <ng-content />
      <svg
        class="text-muted-foreground ms-auto size-4 shrink-0"
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
    </button>
  `,
})
export class HlmSelectTrigger {
  private static _id = 0;

  public readonly userClass = input<ClassValue>('', { alias: 'class' });
  // Medidas de la referencia: 40 px de alto, radio 8 px y borde #a3b4aa.
  protected readonly _computedClass = computed(() =>
    hlm(
      'border-border-strong bg-card text-foreground focus-visible:border-ring focus-visible:ring-ring/40 data-[matches-spartan-invalid=true]:border-destructive data-[matches-spartan-invalid=true]:ring-destructive/30 flex h-10 min-h-10 w-fit items-center justify-between gap-1.5 rounded-lg border px-3 py-[7px] text-[13px] whitespace-nowrap transition-colors outline-none focus-visible:ring-2 disabled:cursor-not-allowed disabled:opacity-50 *:data-[slot=select-value]:line-clamp-1 *:data-[slot=select-value]:flex *:data-[slot=select-value]:items-center *:data-[slot=select-value]:gap-1.5 **:data-[slot=select-value]:pointer-events-none',
      this.userClass(),
    ),
  );

  public readonly buttonId = input<string>(`hlm-select-trigger-${HlmSelectTrigger._id++}`);

  public readonly size = input<'default' | 'sm'>('default');

  /** Whether to force the trigger into an invalid state. */
  public readonly forceInvalid = input<boolean, BooleanInput>(false, {
    transform: booleanAttribute,
  });

  /** Manual override for aria-invalid. When not set, auto-detects from the parent autocomplete error state. */
  public readonly ariaInvalidInput = input<boolean | undefined, BooleanInput>(undefined, {
    transform: (v: BooleanInput) => (v === '' || v === undefined ? undefined : booleanAttribute(v)),
    alias: 'aria-invalid',
  });
}
