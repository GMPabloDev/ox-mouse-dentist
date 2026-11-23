import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { HlmButton } from '@mousedent/ui/button';
import { HlmInput } from '@mousedent/ui/input';
import {
  canAssignLocal,
  isDirty,
  PROFILE_OPTIONS,
  profileIsEditable,
  validateDraft,
  type AssignableStaffProfile,
  type StaffDraft,
  type StaffLocal,
  type StaffProblemField,
  type StaffProblems,
} from './staff-form';
import { StaffIcon } from './staff-icon';
import { StaffNotice } from './staff-notice';

/**
 * Formulario de la variante A dentro del panel lateral. Sirve para invitar y
 * para editar; conserva lo escrito cuando la validación o la API rechazan el
 * guardado. Las asignaciones de recepción se eligen sobre los locales vigentes.
 */
@Component({
  selector: 'app-staff-editor',
  imports: [HlmButton, HlmInput, StaffIcon, StaffNotice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' },
  templateUrl: './staff-editor.html',
})
export class StaffEditor {
  readonly initial = input.required<StaffDraft>();
  readonly locals = input.required<readonly StaffLocal[]>();
  readonly saving = input(false);
  readonly stale = input(false);
  readonly serverProblems = input<StaffProblems>({});

  readonly save = output<StaffDraft>();
  readonly cancel = output<void>();
  /** Avisa a la página de que el dato cambió para retirar el error del servidor. */
  readonly edited = output<StaffProblemField>();

  protected readonly draft = linkedSignal<StaffDraft>(() => this.initial());
  protected readonly localProblems = signal<StaffProblems>({});
  protected readonly profiles = PROFILE_OPTIONS;

  protected readonly isNew = computed(() => this.draft().id === null);
  protected readonly dirty = computed(() => isDirty(this.draft(), this.initial()));
  protected readonly profileEditable = computed(() => profileIsEditable(this.draft()));
  protected readonly problems = computed<StaffProblems>(() => ({
    ...this.localProblems(),
    ...this.serverProblems(),
  }));
  protected readonly activeLocals = computed(() => this.locals().filter((local) => local.isActive));

  /** Lo consulta la página antes de cerrar el panel para no perder cambios. */
  isDirtyNow(): boolean {
    return this.dirty();
  }

  protected fieldProblem(field: StaffProblemField): string | null {
    return this.problems()[field] ?? null;
  }

  protected localSelected(id: string): boolean {
    return this.draft().localIds.includes(id);
  }

  protected localDisabled(local: StaffLocal): boolean {
    return !canAssignLocal(this.draft(), local);
  }

  protected onFieldInput(field: 'displayName' | 'email' | 'phone', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.draft.update((draft) => ({ ...draft, [field]: value }));
    this.clearProblem(field);
    this.edited.emit(field);
  }

  protected chooseProfile(profile: AssignableStaffProfile): void {
    this.draft.update((draft) => ({ ...draft, profile }));
    this.clearProblem('profile');
    this.clearProblem('localIds');
    this.edited.emit('profile');
  }

  protected toggleLocal(id: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.draft.update((draft) => ({
      ...draft,
      localIds: checked
        ? [...draft.localIds, id]
        : draft.localIds.filter((localId) => localId !== id),
    }));
    this.clearProblem('localIds');
    this.edited.emit('localIds');
  }

  protected profileSelected(profile: AssignableStaffProfile): boolean {
    return this.draft().profile === profile;
  }

  protected submit(event: Event): void {
    event.preventDefault();

    if (this.saving() || this.stale()) {
      return;
    }

    const draft = {
      ...this.draft(),
      displayName: this.draft().displayName.trim(),
      email: this.draft().email.trim(),
    };
    const problems = validateDraft(draft, this.locals());

    if (Object.keys(problems).length > 0) {
      this.localProblems.set(problems);
      return;
    }

    this.localProblems.set({});
    this.draft.set(draft);
    this.save.emit(draft);
  }

  protected requestCancel(): void {
    this.cancel.emit();
  }

  /** El botón de cerrar descarta o pide confirmación desde la página. */
  protected requestClose(): void {
    this.cancel.emit();
  }

  private clearProblem(field: StaffProblemField): void {
    if (this.localProblems()[field] === undefined) {
      return;
    }

    this.localProblems.update((problems) => ({ ...problems, [field]: undefined }));
  }
}
