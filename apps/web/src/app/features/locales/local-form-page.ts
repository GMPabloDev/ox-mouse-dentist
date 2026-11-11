import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { FormField, FormRoot, form, type Field } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import type { LocalResponse, LocalScheduleDay, LocalWeekDay } from '@mousedent/api-client';
import { HlmAlert } from '@mousedent/ui/alert';
import { HlmBadge } from '@mousedent/ui/badge';
import { HlmButton } from '@mousedent/ui/button';
import { HlmField, HlmFieldDescription, HlmFieldError, HlmFieldLabel } from '@mousedent/ui/field';
import { HlmInput } from '@mousedent/ui/input';
import { OBJECT_URLS } from '../../core/api/object-urls';
import { LOCALES_API, type LocalesFailure, type PhotoAction } from '../../core/locales/locales-api';
import { ConfirmDialog } from './confirm-dialog';
import { LocalIcon } from './local-icon';
import {
  DUPLICATE_NAME_MESSAGE,
  checkPhotoFile,
  draftFrom,
  emptyDraft,
  hasProblems,
  mapValidationErrors,
  toCreateForm,
  toUpdateForm,
  type LocalDraft,
  type LocalFormProblems,
} from './local-form';
import { localFormSchema } from './local-form-schema';
import { LocalesNotice } from './locales-notice';
import { PhotoField, type PhotoFieldState } from './photo-field';
import { PhotoViewer } from './photo-viewer';
import { ScheduleEditor } from './schedule-editor';
import { WEEK_DAYS } from './schedule-model';

type FormState = 'loading' | 'ready' | 'error' | 'not-found';

/** Estado de la foto antes del último cambio, para poder deshacerlo. */
interface PhotoSnapshot {
  readonly action: 'change' | 'remove';
  readonly file: File | null;
  readonly removed: boolean;
}

/**
 * Formulario de la versión 2 de la referencia: datos y fotografía en una
 * sección, horario por grupos en otra y guardado conjunto. Sirve para crear y
 * editar; la foto solo cambia en el servidor al guardar, así que cancelar o un
 * fallo conserva la versión anterior.
 *
 * Los datos y el horario son un modelo de Signal Forms: el esquema valida los
 * campos y el horario, y el envío multipart se arma en la acción del formulario.
 */
@Component({
  selector: 'app-local-form-page',
  imports: [
    RouterLink,
    ConfirmDialog,
    FormField,
    FormRoot,
    HlmAlert,
    HlmBadge,
    HlmButton,
    HlmField,
    HlmFieldDescription,
    HlmFieldError,
    HlmFieldLabel,
    HlmInput,
    LocalIcon,
    PhotoField,
    PhotoViewer,
    ScheduleEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './local-form-page.html',
})
export class LocalFormPage {
  private readonly api = inject(LOCALES_API);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notice = inject(LocalesNotice);
  private readonly urls = inject(OBJECT_URLS);
  private readonly destroyRef = inject(DestroyRef);

  private destroyed = false;

  protected readonly localId = this.route.snapshot.paramMap.get('id');
  protected readonly isEdit = this.localId !== null;

  protected readonly state = signal<FormState>(this.isEdit ? 'loading' : 'ready');
  protected readonly model = signal<LocalDraft>(emptyDraft());
  protected readonly localForm = form(this.model, localFormSchema, {
    submission: {
      action: (field) => this.save(field().value()),
      onInvalid: () => this.onInvalidSubmit(),
    },
  });

  protected readonly saved = signal<LocalResponse | null>(null);
  protected readonly submitted = signal(false);
  protected readonly serverProblems = signal<LocalFormProblems>({});
  protected readonly formError = signal<string | null>(null);
  protected readonly photoError = signal<string | null>(null);
  protected readonly savedPhotoUrl = signal<string | null>(null);
  protected readonly photoLoading = signal(false);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly selectedFileUrl = signal<string | null>(null);
  protected readonly photoRemoved = signal(false);
  protected readonly photoUndo = signal<PhotoSnapshot | null>(null);
  protected readonly viewerOpen = signal(false);
  protected readonly saving = signal(false);
  protected readonly touchedDraft = signal(false);
  protected readonly discardOpen = signal(false);

  protected readonly previewUrl = computed(
    () => this.selectedFileUrl() ?? (this.photoRemoved() ? null : this.savedPhotoUrl()),
  );

  protected readonly photoState = computed<PhotoFieldState>(() => {
    if (this.selectedFile()) {
      return 'pending';
    }

    if (this.saved()?.photo) {
      return this.photoRemoved() ? 'removed' : 'saved';
    }

    return 'empty';
  });

  /** Lo que se enviará al servidor cuando la foto guardada cambia. */
  protected readonly photoAction = computed<PhotoAction>(() => {
    if (this.selectedFile()) {
      return 'replace';
    }

    return this.saved()?.photo && this.photoRemoved() ? 'remove' : 'keep';
  });

  protected readonly canUndoPhoto = computed(() => this.photoUndo() !== null);

  protected readonly undoLabel = computed(() =>
    this.photoUndo()?.action === 'remove' ? 'Deshacer eliminación' : 'Deshacer cambio',
  );

  protected readonly photoLabel = computed(() => {
    const file = this.selectedFile();
    if (file) {
      return file.name;
    }

    return this.saved()?.photo ? 'Foto guardada' : null;
  });

  protected readonly photoDetail = computed(() => {
    const file = this.selectedFile();
    if (file) {
      return `${formatFileSize(file.size)} · Nueva foto · Pendiente de guardar`;
    }

    const photo = this.saved()?.photo;

    return photo ? `${formatPhotoType(photo.contentType)} · Se conserva al guardar` : null;
  });

  protected readonly statusLabel = computed(() => (this.saved()?.isActive ? 'Activo' : 'Inactivo'));

  protected readonly dirty = computed(() => this.touchedDraft() || this.localForm().dirty());

  protected readonly saveStatus = computed(() =>
    this.saving() ? 'Guardando…' : this.dirty() ? 'Cambios sin guardar' : 'Sin cambios pendientes',
  );

  protected readonly nameError = computed(
    () => this.serverProblems().name ?? this.fieldMessage(this.localForm.name),
  );

  protected readonly addressError = computed(
    () => this.serverProblems().address ?? this.fieldMessage(this.localForm.address),
  );

  protected readonly phoneError = computed(
    () => this.serverProblems().phone ?? this.fieldMessage(this.localForm.phone),
  );

  protected readonly scheduleError = computed(() => {
    const fromServer = this.serverProblems().schedule;

    if (fromServer) {
      return fromServer;
    }

    return this.submitted() ? this.localForm.schedule().errors()[0]?.message : undefined;
  });

  /** Errores por día: los del esquema y los que devuelve la API. */
  protected readonly dayErrors = computed<Partial<Record<LocalWeekDay, string>>>(() => {
    const errors: Partial<Record<LocalWeekDay, string>> = { ...this.serverProblems().days };

    if (!this.submitted()) {
      return errors;
    }

    const days = this.localForm.schedule;

    for (let index = 0; index < days.length; index += 1) {
      const day = WEEK_DAYS[index];
      const message = days[index]?.().errors()[0]?.message;

      if (day && message && !errors[day]) {
        errors[day] = message;
      }
    }

    return errors;
  });

  protected readonly viewerTitle = computed(() =>
    this.isEdit ? 'Foto del local' : 'Foto del nuevo local',
  );

  constructor() {
    if (this.isEdit) {
      void this.load();
    }

    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.releaseUrls();
    });
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.formError.set(null);

    const result = await this.api.list();

    if (!result.ok) {
      this.formError.set('No se pudo cargar el local. Revisa tu conexión e inténtalo de nuevo.');
      this.state.set('error');
      return;
    }

    const local = result.value.find((item) => item.id === this.localId);
    if (!local) {
      this.state.set('not-found');
      return;
    }

    this.saved.set(local);
    this.model.set(draftFrom(local));
    this.submitted.set(false);
    this.serverProblems.set({});
    this.photoUndo.set(null);
    this.photoRemoved.set(false);
    this.touchedDraft.set(false);
    this.state.set('ready');

    if (local.photo) {
      void this.loadSavedPhoto(local);
    }
  }

  protected onWeekChange(week: LocalScheduleDay[]): void {
    this.model.update((draft) => ({ ...draft, schedule: week }));
    this.touchedDraft.set(true);
    this.clearProblems('schedule', 'days');
  }

  protected onFileChosen(file: File): void {
    this.clearProblems('photo');
    const problem = checkPhotoFile(file);

    if (problem) {
      this.photoError.set(problem);
      return;
    }

    this.rememberPhoto('change');
    this.clearSelectedFile();
    this.selectedFile.set(file);
    this.selectedFileUrl.set(this.urls.create(file));
    this.photoRemoved.set(false);
    this.photoError.set(null);
    this.clearProblems('photo');
    this.touchedDraft.set(true);
  }

  protected removePhoto(): void {
    this.rememberPhoto('remove');
    this.clearSelectedFile();
    this.photoRemoved.set(Boolean(this.saved()?.photo));
    this.photoError.set(null);
    this.clearProblems('photo');
    this.touchedDraft.set(true);
  }

  /** Vuelve a la foto que había antes del último cambio o eliminación. */
  protected undoPhoto(): void {
    const snapshot = this.photoUndo();

    if (!snapshot) {
      return;
    }

    this.clearSelectedFile();
    const file = snapshot.file;

    if (file) {
      this.selectedFile.set(file);
      this.selectedFileUrl.set(this.urls.create(file));
    }

    this.photoRemoved.set(snapshot.removed);
    this.photoUndo.set(null);
    this.photoError.set(null);
    this.clearProblems('photo');
    this.touchedDraft.set(true);
  }

  /** Descarta el error que devolvió la API en cuanto se corrige el campo. */
  protected clearProblems(...fields: (keyof LocalFormProblems)[]): void {
    this.serverProblems.update((problems) => {
      const next = { ...problems };

      for (const field of fields) {
        delete next[field];
      }

      return next;
    });
    this.formError.set(null);
  }

  protected leave(): void {
    if (this.saving()) {
      return;
    }

    if (!this.dirty()) {
      void this.router.navigateByUrl('/locales');
      return;
    }

    this.discardOpen.set(true);
  }

  protected discardChanges(): void {
    this.discardOpen.set(false);
    void this.router.navigateByUrl('/locales');
  }

  /** Mensaje de un campo de texto cuando el envío ya se intentó. */
  private fieldMessage(field: Field<string>): string | undefined {
    return this.submitted() ? field().errors()[0]?.message : undefined;
  }

  private onInvalidSubmit(): void {
    this.submitted.set(true);
    this.formError.set('Revisa los campos marcados antes de guardar.');
  }

  /**
   * Acción del formulario: solo se ejecuta con los datos y el horario válidos.
   * La foto se comprueba aquí porque el archivo no forma parte del modelo.
   */
  private async save(draft: LocalDraft): Promise<void> {
    const file = this.selectedFile();
    const photoProblem = file ? checkPhotoFile(file) : null;

    if (photoProblem) {
      this.photoError.set(photoProblem);
      this.formError.set('Revisa los campos marcados antes de guardar.');
      return;
    }

    this.saving.set(true);

    try {
      const result =
        this.isEdit && this.localId
          ? await this.api.update(this.localId, toUpdateForm(draft, this.photoAction(), file))
          : await this.api.create(toCreateForm(draft, file));

      if (!result.ok) {
        this.showFailure(result.failure);
        return;
      }

      this.touchedDraft.set(false);
      this.notice.set(
        this.isEdit
          ? `Se guardaron los cambios de «${result.value.name}».`
          : `Se registró «${result.value.name}».`,
      );
      await this.router.navigateByUrl('/locales');
    } finally {
      this.saving.set(false);
    }
  }

  /** Guarda un único paso atrás de la foto para deshacerlo antes de guardar. */
  private rememberPhoto(action: PhotoSnapshot['action']): void {
    this.photoUndo.set({
      action,
      file: this.selectedFile(),
      removed: this.photoRemoved(),
    });
  }

  private async loadSavedPhoto(local: LocalResponse): Promise<void> {
    this.photoLoading.set(true);
    this.photoError.set(null);

    const result = await this.api.photo(local.id);
    this.photoLoading.set(false);

    if (!result.ok) {
      this.photoError.set('No se pudo cargar la foto guardada. Puedes elegir otra imagen.');
      return;
    }

    const url = this.urls.create(result.value);

    if (this.destroyed) {
      this.urls.revoke(url);
      return;
    }

    this.releaseSavedPhotoUrl();
    this.savedPhotoUrl.set(url);
  }

  private showFailure(failure: LocalesFailure): void {
    switch (failure.kind) {
      case 'validation': {
        const problems = mapValidationErrors(failure.errors);
        this.serverProblems.set(problems);
        this.formError.set(
          hasProblems(problems)
            ? 'Revisa los campos marcados antes de guardar.'
            : 'La API rechazó los datos. Revisa la información e inténtalo de nuevo.',
        );
        return;
      }
      case 'duplicate':
        this.serverProblems.set({ name: failure.detail ?? DUPLICATE_NAME_MESSAGE });
        return;
      case 'not-found':
        this.formError.set('Este local ya no existe. Vuelve a la lista de locales.');
        return;
      case 'unavailable':
        this.formError.set('No se pudo guardar el local. Revisa tu conexión e inténtalo de nuevo.');
    }
  }

  private clearSelectedFile(): void {
    const url = this.selectedFileUrl();

    if (url) {
      this.urls.revoke(url);
    }

    this.selectedFile.set(null);
    this.selectedFileUrl.set(null);
  }

  private releaseSavedPhotoUrl(): void {
    const url = this.savedPhotoUrl();

    if (url) {
      this.urls.revoke(url);
      this.savedPhotoUrl.set(null);
    }
  }

  private releaseUrls(): void {
    this.clearSelectedFile();
    this.releaseSavedPhotoUrl();
  }
}

function formatFileSize(bytes: number): string {
  return bytes >= 1024 * 1024
    ? `${(bytes / (1024 * 1024)).toFixed(1)} MB`
    : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

function formatPhotoType(contentType: string): string {
  switch (contentType) {
    case 'image/jpeg':
      return 'JPG';
    case 'image/png':
      return 'PNG';
    case 'image/webp':
      return 'WebP';
    default:
      return contentType;
  }
}
