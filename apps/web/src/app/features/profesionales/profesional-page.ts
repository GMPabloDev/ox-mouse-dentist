import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type {
  EspecialidadResponse,
  LocalResponse,
  ProfesionalResponse,
} from '@mousedent/api-client';
import { HlmAvatar, HlmAvatarFallback } from '@mousedent/ui/avatar';
import { HlmButton } from '@mousedent/ui/button';
import { HlmInput } from '@mousedent/ui/input';
import { CATALOGO_API } from '../../core/catalogo/catalogo-api';
import { LOCALES_API } from '../../core/locales/locales-api';
import {
  PROFESIONALES_API,
  type ProfesionalFailure,
} from '../../core/profesionales/profesionales-api';
import { StaffConfirm } from '../staff/staff-confirm';
import { StaffDrawer } from '../staff/staff-drawer';
import { StaffIcon } from '../staff/staff-icon';
import { StaffNotice } from '../staff/staff-notice';
import { ProfesionalAccountStatus, ProfesionalFichaStatus } from './profesional-badges';
import { ProfesionalChips } from './profesional-chips';
import { ProfesionalEditor } from './profesional-editor';
import {
  draftFrom,
  EMPTY_DRAFT,
  problemsFromFailure,
  toCreateForm,
  toUpdateForm,
  type ProfesionalDraft,
  type ProfesionalProblemField,
  type ProfesionalProblems,
} from './profesional-form';
import {
  accessChecks,
  accessSentence,
  type AccessCheck,
  activationMissing,
  activeAssignmentCount,
  colegiaturaLabel,
  countProfesionales,
  EMPTY_FILTERS,
  filterProfesionales,
  hasFilters,
  initialsOf,
  resultsLabel,
  type AccountFilter,
  type FichaFilter,
  type ProfesionalFilters,
} from './profesional-list';

type LoadState = 'loading' | 'ready' | 'error' | 'ended';

/** Resultado de la última operación, visible dentro del panel que la ejecutó. */
interface PanelOutcome {
  readonly tone: 'success' | 'warn' | 'error';
  readonly title: string;
  readonly detail: string | null;
}

/** Ficha abierta o formulario en el mismo panel lateral. */
type Panel =
  | { readonly kind: 'detail'; readonly id: string }
  | {
      readonly kind: 'editor';
      readonly id: string | null;
      /** Ficha a la que vuelve «Cancelar» cuando la edición salió del detalle. */
      readonly returnTo: string | null;
    };

type Confirmation =
  | { readonly kind: 'deactivate'; readonly id: string }
  | { readonly kind: 'reactivate'; readonly id: string }
  | { readonly kind: 'resend'; readonly id: string }
  | { readonly kind: 'discard' };

/**
 * Administración de profesionales, variante A de la referencia: tabla con el
 * estado de la ficha y de la cuenta, y la ficha o el formulario en un panel
 * lateral de 660 px. La búsqueda y los filtros se aplican sobre la lista cargada.
 * En pantallas de 1160 px o menos la tabla se convierte en fichas apiladas.
 */
@Component({
  selector: 'app-profesional-page',
  imports: [
    RouterLink,
    HlmAvatar,
    HlmAvatarFallback,
    HlmButton,
    HlmInput,
    ProfesionalAccountStatus,
    ProfesionalChips,
    ProfesionalEditor,
    ProfesionalFichaStatus,
    StaffConfirm,
    StaffDrawer,
    StaffIcon,
    StaffNotice,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profesional-page.html',
})
export class ProfesionalPage {
  private readonly api = inject(PROFESIONALES_API);
  private readonly catalogo = inject(CATALOGO_API);
  private readonly localesApi = inject(LOCALES_API);

  protected readonly state = signal<LoadState>('loading');
  protected readonly professionals = signal<readonly ProfesionalResponse[]>([]);
  protected readonly especialidades = signal<readonly EspecialidadResponse[]>([]);
  protected readonly locales = signal<readonly LocalResponse[]>([]);
  protected readonly loadError = signal<string | null>(null);
  protected readonly endedReason = signal<'session' | 'profile' | null>(null);

  protected readonly search = signal('');
  protected readonly especialidadFilter = signal('');
  protected readonly localFilter = signal('');
  protected readonly fichaFilter = signal<FichaFilter>('all');
  protected readonly accountFilter = signal<AccountFilter>('all');

  protected readonly panel = signal<Panel | null>(null);
  protected readonly editorInitial = signal<ProfesionalDraft>(EMPTY_DRAFT);
  protected readonly serverProblems = signal<ProfesionalProblems>({});
  protected readonly saving = signal(false);
  protected readonly outcome = signal<PanelOutcome | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly confirmation = signal<Confirmation | null>(null);
  /** Ficha a la que se le reenvió la invitación en esta sesión de la pantalla. */
  private readonly resentId = signal<string | null>(null);

  private readonly editor = viewChild(ProfesionalEditor);

  protected readonly filters = computed<ProfesionalFilters>(() => ({
    search: this.search(),
    especialidadId: this.especialidadFilter(),
    localId: this.localFilter(),
    ficha: this.fichaFilter(),
    account: this.accountFilter(),
  }));

  protected readonly hasActiveFilters = computed(() => hasFilters(this.filters()));
  protected readonly visible = computed(() =>
    filterProfesionales(this.professionals(), this.filters()),
  );
  protected readonly counts = computed(() => countProfesionales(this.professionals()));
  protected readonly knownNames = computed(() =>
    this.professionals().map((profesional) => profesional.displayName),
  );
  protected readonly resultLabel = computed(() =>
    resultsLabel(this.visible().length, this.professionals().length, this.hasActiveFilters()),
  );

  protected readonly selected = computed<ProfesionalResponse | null>(() => {
    const panel = this.panel();

    return panel?.kind === 'detail' ? (this.profesionalById(panel.id) ?? null) : null;
  });

  protected readonly confirmTitle = computed(() => {
    const confirmation = this.confirmation();

    switch (confirmation?.kind) {
      case 'deactivate': {
        const profesional = this.profesionalById(confirmation.id);
        return profesional ? `¿Desactivar la ficha de ${profesional.displayName}?` : '';
      }
      case 'reactivate': {
        const profesional = this.profesionalById(confirmation.id);
        return profesional ? `¿Reactivar la ficha de ${profesional.displayName}?` : '';
      }
      case 'resend':
        return '¿Enviar una invitación nueva?';
      case 'discard':
        return '¿Salir sin guardar?';
      default:
        return '';
    }
  });

  protected readonly confirmMessage = computed(() => {
    const confirmation = this.confirmation();

    switch (confirmation?.kind) {
      case 'deactivate':
        return 'Dejará de operar en todos los locales en la siguiente solicitud. Conserva sus especialidades, sus locales de atención y su cuenta.';
      case 'reactivate':
        return 'Volverá a estar disponible para la agenda. Recupera sus especialidades y sus locales guardados; su cuenta no cambia.';
      case 'resend':
        return 'El enlace anterior dejará de servir, incluso si falla este envío. El nuevo enlace dura 24 horas.';
      case 'discard':
        return 'Los cambios de este formulario no se guardarán.';
      default:
        return '';
    }
  });

  protected readonly confirmLabel = computed(() => {
    switch (this.confirmation()?.kind) {
      case 'deactivate':
        return 'Desactivar ficha';
      case 'reactivate':
        return 'Reactivar ficha';
      case 'resend':
        return 'Enviar invitación';
      case 'discard':
        return 'Descartar cambios';
      default:
        return 'Confirmar';
    }
  });

  protected readonly confirmDanger = computed(
    () => this.confirmation()?.kind === 'deactivate' || this.confirmation()?.kind === 'discard',
  );

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.loadError.set(null);
    this.endedReason.set(null);
    this.outcome.set(null);

    const [profesionales, especialidades, locales] = await Promise.all([
      this.api.list(),
      this.catalogo.especialidades(),
      this.localesApi.list(),
    ]);

    if (!profesionales.ok) {
      if (profesionales.failure.kind === 'unauthorized') {
        this.end('session');
        return;
      }

      if (profesionales.failure.kind === 'forbidden') {
        this.end('profile');
        return;
      }

      this.loadError.set(
        'No pudimos cargar los profesionales. Revisa tu conexión y vuelve a intentarlo.',
      );
      this.state.set('error');
      return;
    }

    if (!especialidades.ok) {
      this.loadError.set(
        'No pudimos cargar el catálogo de especialidades para la lista y las asignaciones. Revisa tu conexión y vuelve a intentarlo.',
      );
      this.state.set('error');
      return;
    }

    if (!locales.ok) {
      this.loadError.set(
        'No pudimos cargar los locales de atención para la lista y las asignaciones. Revisa tu conexión y vuelve a intentarlo.',
      );
      this.state.set('error');
      return;
    }

    this.professionals.set(profesionales.value);
    this.especialidades.set(especialidades.value);
    this.locales.set(locales.value);
    this.state.set('ready');
  }

  protected setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  protected setEspecialidadFilter(event: Event): void {
    this.especialidadFilter.set((event.target as HTMLSelectElement).value);
  }

  protected setLocalFilter(event: Event): void {
    this.localFilter.set((event.target as HTMLSelectElement).value);
  }

  protected setFichaFilter(event: Event): void {
    this.fichaFilter.set((event.target as HTMLSelectElement).value as FichaFilter);
  }

  protected setAccountFilter(event: Event): void {
    this.accountFilter.set((event.target as HTMLSelectElement).value as AccountFilter);
  }

  protected clearFilters(): void {
    this.search.set(EMPTY_FILTERS.search);
    this.especialidadFilter.set(EMPTY_FILTERS.especialidadId);
    this.localFilter.set(EMPTY_FILTERS.localId);
    this.fichaFilter.set(EMPTY_FILTERS.ficha);
    this.accountFilter.set(EMPTY_FILTERS.account);
  }

  protected openDetail(id: string): void {
    this.outcome.set(null);
    this.resentId.set(null);
    this.serverProblems.set({});
    this.panel.set({ kind: 'detail', id });
  }

  protected openCreate(): void {
    this.openEditor(EMPTY_DRAFT, null);
  }

  protected openEdit(profesional: ProfesionalResponse, returnToDetail = false): void {
    this.openEditor(draftFrom(profesional), returnToDetail ? profesional.id : null);
  }

  protected requestClosePanel(): void {
    if (this.panel()?.kind === 'editor' && this.editor()?.isDirtyNow()) {
      this.confirmation.set({ kind: 'discard' });
      return;
    }

    this.closePanel();
  }

  /** El formulario cambió: el error del servidor deja de ser vigente. */
  protected clearServerProblem(field: ProfesionalProblemField): void {
    const problems = this.serverProblems();

    if (!(field in problems)) {
      return;
    }

    const next = { ...problems };
    delete next[field];
    this.serverProblems.set(next);
  }

  protected requestStatusChange(profesional: ProfesionalResponse): void {
    if (!profesional.isActive && activationMissing(profesional).length > 0) {
      return;
    }

    this.confirmation.set({
      kind: profesional.isActive ? 'deactivate' : 'reactivate',
      id: profesional.id,
    });
  }

  protected requestResend(profesional: ProfesionalResponse): void {
    this.confirmation.set({ kind: 'resend', id: profesional.id });
  }

  protected cancelConfirmation(): void {
    this.confirmation.set(null);
  }

  protected async confirm(): Promise<void> {
    const confirmation = this.confirmation();
    this.confirmation.set(null);

    switch (confirmation?.kind) {
      case 'deactivate':
        await this.commitStatus(confirmation.id, false);
        return;
      case 'reactivate':
        await this.commitStatus(confirmation.id, true);
        return;
      case 'resend':
        await this.commitResend(confirmation.id);
        return;
      case 'discard':
        this.closePanel();
        return;
    }
  }

  /** Recibe el formulario validado del panel lateral. */
  protected async requestSave(draft: ProfesionalDraft): Promise<void> {
    await this.commitSave(draft);
  }

  protected profesionalById(id: string): ProfesionalResponse | undefined {
    return this.professionals().find((profesional) => profesional.id === id);
  }

  protected missingFor(profesional: ProfesionalResponse): readonly string[] {
    return activationMissing(profesional);
  }

  protected checksFor(profesional: ProfesionalResponse): readonly AccessCheck[] {
    return accessChecks(profesional);
  }

  protected sentenceFor(profesional: ProfesionalResponse): string {
    return accessSentence(profesional);
  }

  protected activeEspecialidades(profesional: ProfesionalResponse): number {
    return activeAssignmentCount(profesional.especialidades);
  }

  protected activeLocales(profesional: ProfesionalResponse): number {
    return activeAssignmentCount(profesional.locales);
  }

  protected invitationCopy(profesional: ProfesionalResponse): string {
    if (profesional.invitationFailed) {
      return 'La cuenta sigue pendiente y el último envío de la invitación falló. Puedes reintentarlo desde esta ficha.';
    }

    if (this.resentId() === profesional.id) {
      return 'La invitación se reenvió. La cuenta seguirá pendiente hasta que la persona cree su contraseña.';
    }

    return 'La cuenta queda pendiente de que la persona establezca su contraseña. Si no recibió la invitación, puedes reenviarla desde esta ficha.';
  }

  /** Una asignación conservada que hoy está inactiva se explica aunque haya otras activas. */
  protected hasInactiveEspecialidades(profesional: ProfesionalResponse): boolean {
    return profesional.especialidades.some((especialidad) => !especialidad.isActive);
  }

  protected hasInactiveLocales(profesional: ProfesionalResponse): boolean {
    return profesional.locales.some((local) => !local.isActive);
  }

  protected initials(profesional: ProfesionalResponse): string {
    return initialsOf(profesional.displayName);
  }

  protected colegiatura(profesional: ProfesionalResponse): string {
    return colegiaturaLabel(profesional);
  }

  protected panelLabelId(): string {
    return this.panel()?.kind === 'editor'
      ? 'profesional-editor-title'
      : 'profesional-detail-title';
  }

  protected endedTitle(): string {
    return this.endedReason() === 'profile'
      ? 'Tu perfil ya no permite gestionar profesionales'
      : 'Tu sesión ya no está disponible';
  }

  protected endedMessage(): string {
    return this.endedReason() === 'profile'
      ? 'Los permisos vigentes se comprueban en cada solicitud. Vuelve a tu espacio para continuar.'
      : 'Vuelve a iniciar sesión para comprobar tu acceso vigente.';
  }

  private openEditor(draft: ProfesionalDraft, returnTo: string | null): void {
    this.outcome.set(null);
    this.resentId.set(null);
    this.serverProblems.set({});
    this.editorInitial.set(draft);
    this.panel.set({ kind: 'editor', id: draft.id, returnTo });
  }

  private closePanel(): void {
    const panel = this.panel();

    if (panel?.kind === 'editor' && panel.returnTo) {
      this.panel.set({ kind: 'detail', id: panel.returnTo });
      this.serverProblems.set({});
      this.outcome.set(null);
      return;
    }

    this.panel.set(null);
    this.serverProblems.set({});
    this.outcome.set(null);
    this.resentId.set(null);
    this.saving.set(false);
  }

  private end(reason: 'session' | 'profile'): void {
    this.professionals.set([]);
    this.especialidades.set([]);
    this.locales.set([]);
    this.panel.set(null);
    this.confirmation.set(null);
    this.outcome.set(null);
    this.resentId.set(null);
    this.endedReason.set(reason);
    this.state.set('ended');
  }

  private async commitSave(draft: ProfesionalDraft): Promise<void> {
    this.saving.set(true);
    this.serverProblems.set({});
    this.outcome.set(null);

    const result = draft.id
      ? await this.api.update(draft.id, toUpdateForm(draft))
      : await this.api.create(toCreateForm(draft));

    this.saving.set(false);

    if (!result.ok) {
      await this.handleSaveFailure(result.failure);
      return;
    }

    this.upsert(result.value);
    this.serverProblems.set({});
    this.panel.set({ kind: 'detail', id: result.value.id });
    this.outcome.set({ tone: 'success', title: saveNotice(draft, result.value), detail: null });
  }

  private async handleSaveFailure(failure: ProfesionalFailure): Promise<void> {
    if (failure.kind === 'unauthorized') {
      this.end('session');
      return;
    }

    if (failure.kind === 'forbidden') {
      this.end('profile');
      return;
    }

    if (failure.kind === 'invitation-failed') {
      const id = failure.profesionalId;
      const reloaded = await this.reload();

      if (this.state() !== 'ready') {
        return;
      }

      if (reloaded && id && this.profesionalById(id)) {
        this.serverProblems.set({});
        this.panel.set({ kind: 'detail', id });
        this.outcome.set({
          tone: 'warn',
          title: 'La ficha quedó guardada, la invitación no salió',
          detail: null,
        });
        return;
      }

      // Sin lista no hay ficha que abrir: la pantalla muestra su error con reintento.
      this.closePanel();
      this.loadError.set(
        'La ficha quedó guardada y la invitación no salió, pero no pudimos actualizar la lista. Revisa tu conexión y vuelve a intentarlo.',
      );
      this.state.set('error');
      return;
    }

    if (failure.kind === 'not-found') {
      void this.reload();
    }

    this.serverProblems.set(problemsFromFailure(failure));
  }

  private async commitStatus(id: string, isActive: boolean): Promise<void> {
    this.busyId.set(id);
    this.outcome.set(null);

    const result = await this.api.setStatus(id, isActive);

    this.busyId.set(null);

    if (!result.ok) {
      this.handleActionFailure(result.failure);
      return;
    }

    this.upsert(result.value);
    this.outcome.set({
      tone: 'success',
      title: result.value.isActive
        ? 'Ficha reactivada. Recuperó sus especialidades y sus locales guardados.'
        : 'Ficha desactivada. Se conservaron sus especialidades, sus locales y su cuenta.',
      detail: null,
    });
  }

  private async commitResend(id: string): Promise<void> {
    this.busyId.set(id);
    this.outcome.set(null);
    this.resentId.set(null);

    const result = await this.api.resendInvitation(id);

    this.busyId.set(null);

    if (!result.ok) {
      this.handleActionFailure(result.failure);
      return;
    }

    this.upsert(result.value);
    this.resentId.set(result.value.id);
    this.outcome.set({
      tone: 'success',
      title: `Invitación reenviada a ${result.value.email}.`,
      detail: null,
    });
  }

  private handleActionFailure(failure: ProfesionalFailure): void {
    const failed = (detail: string): PanelOutcome => ({
      tone: 'error',
      title: 'La operación no se completó',
      detail,
    });

    switch (failure.kind) {
      case 'unauthorized':
        this.end('session');
        return;
      case 'forbidden':
        this.end('profile');
        return;
      case 'not-found':
        this.outcome.set(
          failed('La ficha ya no existe. Actualiza la lista y vuelve a intentarlo.'),
        );
        void this.reload();
        return;
      case 'invitation-failed':
        this.outcome.set({
          tone: 'error',
          title: 'No pudimos reenviar la invitación',
          detail:
            'El enlace anterior ya no sirve. Revisa la configuración del correo y vuelve a intentarlo.',
        });
        void this.reload();
        return;
      case 'validation': {
        const first = Object.values(failure.errors)[0]?.[0];
        this.outcome.set(failed(first ?? 'Revisa los datos de la ficha y vuelve a intentarlo.'));
        return;
      }
      case 'activation-incomplete':
        this.outcome.set(
          failed(
            failure.detail ??
              'La ficha necesita al menos una especialidad activa y un local de atención activo.',
          ),
        );
        return;
      case 'conflict':
        this.outcome.set(
          failed(failure.detail ?? 'La operación no se completó. Vuelve a intentarlo.'),
        );
        return;
      case 'unavailable':
        this.outcome.set(
          failed('La operación no se completó. Revisa tu conexión y vuelve a intentarlo.'),
        );
        return;
      default:
        this.outcome.set(
          failed(failure.detail ?? 'La operación no se completó. Vuelve a intentarlo.'),
        );
    }
  }

  private async reload(): Promise<boolean> {
    const result = await this.api.list();

    if (result.ok) {
      this.professionals.set(result.value);
      this.closeMissingSelection(result.value);
      return true;
    }

    if (result.failure.kind === 'unauthorized') {
      this.end('session');
      return false;
    }

    if (result.failure.kind === 'forbidden') {
      this.end('profile');
      return false;
    }

    return false;
  }

  /** Si la ficha abierta desapareció de la lista, el panel no puede seguir mostrándola. */
  private closeMissingSelection(professionals: readonly ProfesionalResponse[]): void {
    const panel = this.panel();

    if (
      panel?.kind === 'detail' &&
      !professionals.some((profesional) => profesional.id === panel.id)
    ) {
      this.closePanel();
    }
  }

  private upsert(profesional: ProfesionalResponse): void {
    this.professionals.update((professionals) => {
      const exists = professionals.some((candidate) => candidate.id === profesional.id);

      return exists
        ? professionals.map((candidate) =>
            candidate.id === profesional.id ? profesional : candidate,
          )
        : [...professionals, profesional];
    });
  }
}

/**
 * Mensaje posterior al guardado. El contrato no distingue un alta nueva de un
 * vínculo con una cuenta pendiente, así que el caso pendiente se describe sin
 * afirmar si el correo salió; el estado de la cuenta lo confirma.
 */
function saveNotice(draft: ProfesionalDraft, saved: ProfesionalResponse): string {
  if (draft.id !== null) {
    return 'Cambios guardados.';
  }

  switch (saved.accountStatus) {
    case 'Activa':
      return `Cuenta vinculada. ${saved.displayName} ya puede acceder con su contraseña.`;
    case 'Inactiva':
      return 'Cuenta vinculada. El acceso está inactivo y se gestiona en Personal.';
    default:
      return 'Ficha creada. La cuenta queda pendiente hasta que la persona establezca su contraseña.';
  }
}
