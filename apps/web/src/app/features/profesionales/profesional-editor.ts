import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { EspecialidadResponse, LocalResponse } from '@mousedent/api-client';
import { HlmAvatar, HlmAvatarFallback } from '@mousedent/ui/avatar';
import { HlmButton } from '@mousedent/ui/button';
import { HlmInput } from '@mousedent/ui/input';
import { StaffIcon } from '../staff/staff-icon';
import { StaffNotice } from '../staff/staff-notice';
import { ProfesionalAccountStatus } from './profesional-badges';
import {
  canAssignEspecialidad,
  canAssignLocal,
  isDirty,
  validateDraft,
  type ProfesionalDraft,
  type ProfesionalProblemField,
  type ProfesionalProblems,
} from './profesional-form';
import { homonyms, initialsOf } from './profesional-list';

/**
 * Alta y edición de la ficha dentro del mismo panel lateral. El alta pide los
 * datos de la cuenta y los de la ficha; la edición muestra la cuenta de solo
 * lectura y deja cambiar la colegiatura y las asignaciones. Conserva lo escrito
 * cuando la validación o la API rechazan el guardado.
 */
@Component({
  selector: 'app-profesional-editor',
  imports: [
    RouterLink,
    HlmAvatar,
    HlmAvatarFallback,
    HlmButton,
    HlmInput,
    ProfesionalAccountStatus,
    StaffIcon,
    StaffNotice,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' },
  templateUrl: './profesional-editor.html',
})
export class ProfesionalEditor {
  readonly initial = input.required<ProfesionalDraft>();
  readonly especialidades = input.required<readonly EspecialidadResponse[]>();
  readonly locales = input.required<readonly LocalResponse[]>();
  readonly saving = input(false);
  readonly serverProblems = input<ProfesionalProblems>({});
  /** Nombres de las fichas ya cargadas, para avisar posibles homónimos. */
  readonly knownNames = input<readonly string[]>([]);

  readonly save = output<ProfesionalDraft>();
  readonly cancel = output<void>();
  /** Avisa a la página de que el dato cambió para retirar el error del servidor. */
  readonly edited = output<ProfesionalProblemField>();

  protected readonly draft = linkedSignal<ProfesionalDraft>(() => this.initial());
  protected readonly localProblems = signal<ProfesionalProblems>({});

  protected readonly isNew = computed(() => this.draft().id === null);
  protected readonly dirty = computed(() => isDirty(this.draft(), this.initial()));
  protected readonly problems = computed<ProfesionalProblems>(() => ({
    ...this.serverProblems(),
    ...this.localProblems(),
  }));

  /** Registros activos y, al editar, los ya asignados aunque hoy estén inactivos. */
  protected readonly visibleEspecialidades = computed(() =>
    this.especialidades().filter(
      (especialidad) =>
        especialidad.isActive || this.draft().originalEspecialidadIds.includes(especialidad.id),
    ),
  );
  protected readonly visibleLocales = computed(() =>
    this.locales().filter(
      (local) => local.isActive || this.draft().originalLocalIds.includes(local.id),
    ),
  );

  protected readonly activeEspecialidades = computed(
    () => this.draft().especialidadIds.filter((id) => this.isActiveEspecialidad(id)).length,
  );
  protected readonly activeLocales = computed(
    () => this.draft().localIds.filter((id) => this.isActiveLocal(id)).length,
  );

  protected readonly keepsInactiveEspecialidades = computed(() =>
    this.visibleEspecialidades().some(
      (especialidad) => !especialidad.isActive && this.especialidadSelected(especialidad.id),
    ),
  );
  protected readonly keepsInactiveLocales = computed(() =>
    this.visibleLocales().some((local) => !local.isActive && this.localSelected(local.id)),
  );

  protected readonly homonymNames = computed(() =>
    this.isNew() ? homonyms(this.draft().displayName, this.knownNames()) : [],
  );

  protected readonly homonymMessage = computed(() => {
    const names = this.homonymNames();
    const subject = names.length === 1 ? 'ya tiene' : 'ya tienen';

    return `${listNames(names)} ${subject} una ficha con un nombre igual o parecido. El nombre no es único, así que puedes continuar.`;
  });

  protected readonly especialidadHelp = computed(() =>
    this.requiresMinimum()
      ? 'Una ficha activa necesita al menos una especialidad activa.'
      : 'Puedes guardar sin especialidades activas, pero la ficha no podrá reactivarse.',
  );
  protected readonly localHelp = computed(() =>
    this.requiresMinimum()
      ? 'Una ficha activa necesita al menos un local activo.'
      : 'Puedes guardar sin locales activos, pero la ficha no podrá reactivarse.',
  );

  /** Lo consulta la página antes de cerrar el panel para no perder cambios. */
  isDirtyNow(): boolean {
    return this.dirty();
  }

  protected fieldProblem(field: ProfesionalProblemField): string | null {
    return this.problems()[field] ?? null;
  }

  protected especialidadSelected(id: string): boolean {
    return this.draft().especialidadIds.includes(id);
  }

  protected localSelected(id: string): boolean {
    return this.draft().localIds.includes(id);
  }

  protected especialidadDisabled(especialidad: EspecialidadResponse): boolean {
    return this.saving() || !canAssignEspecialidad(this.draft(), especialidad);
  }

  protected localDisabled(local: LocalResponse): boolean {
    return this.saving() || !canAssignLocal(this.draft(), local);
  }

  protected initials(): string {
    return initialsOf(this.initial().displayName);
  }

  protected onNameInput(event: Event): void {
    this.updateField('displayName', (event.target as HTMLInputElement).value);
  }

  protected onEmailInput(event: Event): void {
    this.updateField('email', (event.target as HTMLInputElement).value);
  }

  protected onColegiaturaInput(event: Event): void {
    this.updateField('colegiatura', (event.target as HTMLInputElement).value);
  }

  protected toggleEspecialidad(id: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;

    this.draft.update((draft) => ({
      ...draft,
      especialidadIds: checked
        ? [...draft.especialidadIds, id]
        : draft.especialidadIds.filter((current) => current !== id),
    }));
    this.clearProblem('especialidadIds');
    this.edited.emit('especialidadIds');
  }

  protected toggleLocal(id: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;

    this.draft.update((draft) => ({
      ...draft,
      localIds: checked
        ? [...draft.localIds, id]
        : draft.localIds.filter((current) => current !== id),
    }));
    this.clearProblem('localIds');
    this.edited.emit('localIds');
  }

  protected submit(event: Event): void {
    event.preventDefault();

    if (this.saving()) {
      return;
    }

    const draft: ProfesionalDraft = {
      ...this.draft(),
      displayName: this.draft().displayName.trim(),
      email: this.draft().email.trim(),
      colegiatura: this.draft().colegiatura.trim(),
    };
    const problems = validateDraft(draft, this.especialidades(), this.locales());

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

  private requiresMinimum(): boolean {
    return this.draft().id === null || this.draft().isActive;
  }

  private isActiveEspecialidad(id: string): boolean {
    return this.especialidades().some(
      (especialidad) => especialidad.id === id && especialidad.isActive,
    );
  }

  private isActiveLocal(id: string): boolean {
    return this.locales().some((local) => local.id === id && local.isActive);
  }

  private updateField(field: 'displayName' | 'email' | 'colegiatura', value: string): void {
    this.draft.update((draft) => ({ ...draft, [field]: value }));
    this.clearProblem(field);
    this.edited.emit(field);
  }

  private clearProblem(field: ProfesionalProblemField): void {
    if (this.localProblems()[field] === undefined) {
      return;
    }

    this.localProblems.update((problems) => ({ ...problems, [field]: undefined }));
  }
}

function listNames(names: readonly string[]): string {
  if (names.length === 1) {
    return names[0]!;
  }

  if (names.length === 2) {
    return `${names[0]} y ${names[1]}`;
  }

  return `${names.slice(0, -1).join(', ')} y ${names[names.length - 1]}`;
}
