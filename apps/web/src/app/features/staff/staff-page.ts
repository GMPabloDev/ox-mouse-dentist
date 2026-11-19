import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { LocalResponse, StaffAccountResponse } from '@mousedent/api-client';
import { HlmAlert } from '@mousedent/ui/alert';
import { HlmAvatar, HlmAvatarFallback } from '@mousedent/ui/avatar';
import { HlmButton } from '@mousedent/ui/button';
import { HlmInput } from '@mousedent/ui/input';
import { profileLabel, SessionService } from '../../core/auth/session';
import { LOCALES_API } from '../../core/locales/locales-api';
import { STAFF_API, type StaffFailure } from '../../core/staff/staff-api';
import { StaffConfirm } from './staff-confirm';
import { StaffDrawer } from './staff-drawer';
import { StaffEditor } from './staff-editor';
import { StaffIcon } from './staff-icon';
import { StaffNotice } from './staff-notice';
import { StaffStatus } from './staff-status';
import {
  accessSummary,
  draftFrom,
  EMPTY_DRAFT,
  hasActiveLocal,
  problemsFromFailure,
  statusLabel,
  toForm,
  type StaffDraft,
  type StaffLocal,
  type StaffProblemField,
  type StaffProblems,
} from './staff-form';

type LoadState = 'loading' | 'ready' | 'error' | 'ended';
type StatusFilter = 'all' | 'active' | 'pending' | 'inactive';

type Panel = { readonly kind: 'detail'; readonly id: string } | { readonly kind: 'editor' };

type Confirmation =
  | { readonly kind: 'status'; readonly id: string }
  | { readonly kind: 'resend'; readonly id: string }
  | { readonly kind: 'email-change'; readonly draft: StaffDraft }
  | { readonly kind: 'profile-change'; readonly draft: StaffDraft }
  | { readonly kind: 'discard' }
  | { readonly kind: 'last-administrator' };

interface FilterOption {
  readonly value: StatusFilter;
  readonly label: string;
}

const FILTERS: readonly FilterOption[] = [
  { value: 'all', label: 'Todas' },
  { value: 'active', label: 'Activas' },
  { value: 'pending', label: 'Pendientes' },
  { value: 'inactive', label: 'Inactivas' },
];

const LAST_ADMINISTRATOR_MESSAGE =
  'No puedes dejar al equipo sin administración disponible. Activa otra cuenta de administración con contraseña antes de cambiar este correo o perfil.';

/**
 * Gestión de cuentas del personal, variante A del prototipo: tabla con identidad,
 * perfil, acceso y estado, y detalle o formulario en un panel lateral. La
 * búsqueda y los filtros se aplican sobre la lista ya cargada.
 */
@Component({
  selector: 'app-staff-page',
  imports: [
    RouterLink,
    HlmAlert,
    HlmAvatar,
    HlmAvatarFallback,
    HlmButton,
    HlmInput,
    StaffConfirm,
    StaffDrawer,
    StaffEditor,
    StaffIcon,
    StaffNotice,
    StaffStatus,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './staff-page.html',
})
export class StaffPage {
  private readonly api = inject(STAFF_API);
  private readonly localesApi = inject(LOCALES_API);
  private readonly session = inject(SessionService);

  protected readonly filters = FILTERS;
  protected readonly state = signal<LoadState>('loading');
  protected readonly accounts = signal<readonly StaffAccountResponse[]>([]);
  protected readonly locals = signal<readonly StaffLocal[]>([]);
  protected readonly search = signal('');
  protected readonly filter = signal<StatusFilter>('all');
  protected readonly loadError = signal<string | null>(null);
  protected readonly endedReason = signal<'session' | 'profile' | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly panel = signal<Panel | null>(null);
  protected readonly editorInitial = signal<StaffDraft>(EMPTY_DRAFT);
  protected readonly serverProblems = signal<StaffProblems>({});
  protected readonly saving = signal(false);
  protected readonly staleEdit = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly confirmation = signal<Confirmation | null>(null);

  /** Fallos de envío conocidos en esta visita. El contrato no los persiste. */
  private readonly mailFailures = signal<ReadonlySet<string>>(new Set());

  protected readonly currentUserId = computed(() => this.session.user()?.id ?? null);
  protected readonly editing = computed(() => this.panel()?.kind === 'editor');
  private readonly editor = viewChild(StaffEditor);

  protected readonly localsById = computed(
    () => new Map(this.locals().map((local) => [local.id, local])),
  );

  protected readonly selected = computed<StaffAccountResponse | null>(() => {
    const panel = this.panel();

    return panel?.kind === 'detail' ? (this.accountById(panel.id) ?? null) : null;
  });

  protected readonly visible = computed(() => {
    const term = normalize(this.search());
    const filter = this.filter();

    return this.accounts().filter(
      (account) =>
        (filter === 'all' || filter === statusKey(account)) &&
        (!term || normalize(`${account.displayName} ${account.email}`).includes(term)),
    );
  });

  protected readonly counts = computed(() => {
    const accounts = this.accounts();

    return {
      all: accounts.length,
      active: accounts.filter((account) => account.status === 'Activa').length,
      pending: accounts.filter((account) => account.status === 'Pendiente').length,
      inactive: accounts.filter((account) => account.status === 'Inactiva').length,
    };
  });

  protected readonly confirmTitle = computed(() => {
    const confirmation = this.confirmation();

    switch (confirmation?.kind) {
      case 'status': {
        const account = this.accountById(confirmation.id);
        return account?.status === 'Inactiva'
          ? '¿Reactivar esta cuenta?'
          : '¿Desactivar el acceso?';
      }
      case 'resend':
        return '¿Enviar una invitación nueva?';
      case 'email-change':
        return '¿Cambiar el correo de acceso?';
      case 'profile-change':
        return '¿Cambiar el perfil?';
      case 'discard':
        return '¿Salir sin guardar?';
      case 'last-administrator':
        return 'Esta cuenta debe seguir activa';
      default:
        return '';
    }
  });

  protected readonly confirmMessage = computed(() => {
    const confirmation = this.confirmation();

    switch (confirmation?.kind) {
      case 'status': {
        const account = this.accountById(confirmation.id);
        if (!account) {
          return '';
        }

        return account.status === 'Inactiva'
          ? `${account.displayName} podrá iniciar una sesión nueva con su contraseña. No se restaurarán sus sesiones anteriores.`
          : `${account.displayName} perderá el acceso en su siguiente solicitud. Sus enlaces pendientes dejarán de servir. Conservaremos sus datos.`;
      }
      case 'resend':
        return 'El enlace anterior dejará de servir, incluso si falla este envío. El nuevo enlace dura 24 horas.';
      case 'email-change':
        return 'Se cerrarán sus sesiones y se invalidarán los enlaces anteriores. La cuenta quedará pendiente y enviaremos una invitación al correo nuevo.';
      case 'profile-change':
        return 'La persona tendrá que iniciar sesión de nuevo para usar su nuevo perfil. Sus permisos anteriores dejarán de servir en la siguiente solicitud.';
      case 'discard':
        return 'Los cambios de este formulario no se guardarán.';
      case 'last-administrator':
        return 'Es la última cuenta de administración activa con contraseña. Una invitación pendiente no la reemplaza. Activa otra cuenta de administración antes de desactivar esta.';
      default:
        return '';
    }
  });

  protected readonly confirmDanger = computed(() => {
    const confirmation = this.confirmation();

    if (confirmation?.kind !== 'status') {
      return false;
    }

    return this.accountById(confirmation.id)?.status !== 'Inactiva';
  });

  protected readonly confirmLabel = computed(() => {
    const confirmation = this.confirmation();

    switch (confirmation?.kind) {
      case 'status': {
        const account = this.accountById(confirmation.id);
        return account?.status === 'Inactiva' ? 'Reactivar cuenta' : 'Desactivar acceso';
      }
      case 'resend':
        return 'Enviar invitación';
      case 'email-change':
        return 'Cambiar y enviar invitación';
      case 'profile-change':
        return 'Cambiar perfil';
      case 'discard':
        return 'Descartar cambios';
      default:
        return 'Confirmar';
    }
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.loadError.set(null);
    this.endedReason.set(null);

    const [staff, locals] = await Promise.all([this.api.list(), this.localesApi.list()]);

    if (!staff.ok) {
      if (staff.failure.kind === 'unauthorized') {
        this.end('session');
        return;
      }

      if (staff.failure.kind === 'forbidden') {
        this.end('profile');
        return;
      }

      this.loadError.set(
        'No pudimos cargar las cuentas. Revisa tu conexión y vuelve a intentarlo.',
      );
      this.state.set('error');
      return;
    }

    if (!locals.ok) {
      this.loadError.set(
        'No pudimos cargar los locales ni sus asignaciones. Revisa tu conexión y vuelve a intentarlo.',
      );
      this.state.set('error');
      return;
    }

    this.accounts.set(staff.value);
    this.locals.set(locals.value.map(toStaffLocal));
    this.state.set('ready');
  }

  protected setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  protected setFilter(value: StatusFilter): void {
    this.filter.set(value);
  }

  protected clearFilters(): void {
    this.search.set('');
    this.filter.set('all');
  }

  protected openDetail(id: string): void {
    this.notice.set(null);
    this.serverProblems.set({});
    this.actionError.set(null);
    this.panel.set({ kind: 'detail', id });
  }

  protected openCreate(): void {
    this.openEditor(EMPTY_DRAFT);
  }

  protected openEdit(account: StaffAccountResponse): void {
    this.openEditor(draftFrom(account));
  }

  protected requestClosePanel(): void {
    if (this.panel()?.kind === 'editor' && this.editor()?.isDirtyNow()) {
      this.confirmation.set({ kind: 'discard' });
      return;
    }

    this.panel.set(null);
    this.serverProblems.set({});
    this.actionError.set(null);
  }

  /** El formulario cambió: el error del servidor deja de ser vigente. */
  protected clearServerProblem(field: StaffProblemField): void {
    this.serverProblems.update((problems) => ({
      ...problems,
      [field]: undefined,
      form: this.staleEdit() ? problems.form : undefined,
    }));
  }

  protected requestStatusChange(account: StaffAccountResponse): void {
    if (account.status !== 'Inactiva' && this.isLastAvailableAdministrator(account)) {
      this.confirmation.set({ kind: 'last-administrator' });
      return;
    }

    this.confirmation.set({ kind: 'status', id: account.id });
  }

  protected requestResend(account: StaffAccountResponse): void {
    this.confirmation.set({ kind: 'resend', id: account.id });
  }

  protected cancelConfirmation(): void {
    this.confirmation.set(null);
  }

  protected async confirm(): Promise<void> {
    const confirmation = this.confirmation();
    this.confirmation.set(null);

    switch (confirmation?.kind) {
      case 'status':
        await this.commitStatus(confirmation.id);
        return;
      case 'resend':
        await this.commitResend(confirmation.id);
        return;
      case 'email-change':
      case 'profile-change':
        await this.commitSave(confirmation.draft);
        return;
      case 'discard':
        this.closePanel();
        return;
    }
  }

  /** Recibe el formulario validado del panel lateral. */
  protected async requestSave(draft: StaffDraft): Promise<void> {
    if (this.staleEdit()) {
      return;
    }

    const account = draft.id ? (this.accountById(draft.id) ?? null) : null;
    const emailChanged =
      account !== null && account.email.toLowerCase() !== draft.email.trim().toLowerCase();
    const profileChanged = account !== null && account.profile !== draft.profile;

    if (account && this.isLastAvailableAdministrator(account) && (emailChanged || profileChanged)) {
      this.serverProblems.set({ form: LAST_ADMINISTRATOR_MESSAGE });
      return;
    }

    if (emailChanged) {
      this.confirmation.set({ kind: 'email-change', draft });
      return;
    }

    if (profileChanged) {
      this.confirmation.set({ kind: 'profile-change', draft });
      return;
    }

    await this.commitSave(draft);
  }

  protected accountById(id: string): StaffAccountResponse | undefined {
    return this.accounts().find((account) => account.id === id);
  }

  protected isCurrentSession(account: StaffAccountResponse): boolean {
    return account.id === this.currentUserId();
  }

  protected mailFailed(account: StaffAccountResponse): boolean {
    return this.mailFailures().has(account.id);
  }

  protected profileOf(account: StaffAccountResponse): string {
    return profileLabel(account.profile);
  }

  protected accessLabel(account: StaffAccountResponse): string {
    return accessSummary(account, this.localsById());
  }

  protected statusOf(account: StaffAccountResponse): string {
    return statusLabel(account.status);
  }

  protected countFor(value: StatusFilter): number {
    return this.counts()[value];
  }

  protected filterPressed(value: StatusFilter): boolean {
    return this.filter() === value;
  }

  protected panelLabelId(): string {
    return this.panel()?.kind === 'editor' ? 'staff-editor-title' : 'staff-detail-title';
  }

  protected endedTitle(): string {
    return this.endedReason() === 'profile'
      ? 'Tu perfil ya no permite gestionar el personal'
      : 'Tu sesión ya no está disponible';
  }

  protected endedMessage(): string {
    return this.endedReason() === 'profile'
      ? 'Los permisos vigentes se comprueban en cada solicitud. Vuelve a tu espacio para continuar.'
      : 'Vuelve a iniciar sesión para comprobar tu acceso vigente.';
  }

  protected initialsOf(account: StaffAccountResponse): string {
    return initials(account.displayName);
  }

  protected isLastAvailableAdministrator(account: StaffAccountResponse): boolean {
    if (account.profile !== 'Administrador' || account.status !== 'Activa') {
      return false;
    }

    return (
      this.accounts().filter(
        (candidate) => candidate.profile === 'Administrador' && candidate.status === 'Activa',
      ).length === 1
    );
  }

  protected selectedLocals(account: StaffAccountResponse): readonly StaffLocal[] {
    return account.assignedLocalIds
      .map((id) => this.localsById().get(id))
      .filter((local): local is StaffLocal => local !== undefined);
  }

  protected selectedHasActiveLocal(account: StaffAccountResponse): boolean {
    return hasActiveLocal(account, this.localsById());
  }

  private openEditor(draft: StaffDraft): void {
    this.staleEdit.set(false);
    this.notice.set(null);
    this.editorInitial.set(draft);
    this.serverProblems.set({});
    this.actionError.set(null);
    this.panel.set({ kind: 'editor' });
  }

  private closePanel(): void {
    this.panel.set(null);
    this.serverProblems.set({});
    this.actionError.set(null);
    this.saving.set(false);
  }

  private end(reason: 'session' | 'profile'): void {
    this.accounts.set([]);
    this.locals.set([]);
    this.panel.set(null);
    this.endedReason.set(reason);
    this.state.set('ended');
  }

  private async commitSave(draft: StaffDraft): Promise<void> {
    this.saving.set(true);
    this.serverProblems.set({});
    this.actionError.set(null);

    const form = toForm(draft);
    const result = draft.id ? await this.api.update(draft.id, form) : await this.api.create(form);

    this.saving.set(false);

    if (!result.ok) {
      await this.handleSaveFailure(result.failure, draft);
      return;
    }

    const previous = draft.id ? (this.accountById(draft.id) ?? null) : null;
    const emailChanged =
      previous !== null && previous.email.toLowerCase() !== result.value.email.toLowerCase();

    this.clearMailFailure(result.value.id);
    this.upsert(result.value);
    this.closePanel();
    this.search.set('');
    this.filter.set('all');
    this.notice.set(
      draft.id === null
        ? 'Cuenta creada. La invitación se envió al correo indicado.'
        : emailChanged
          ? 'Cambios guardados. La invitación se envió al correo nuevo.'
          : 'Cambios guardados.',
    );
  }

  private async handleSaveFailure(failure: StaffFailure, draft: StaffDraft): Promise<void> {
    if (failure.kind === 'unauthorized') {
      this.end('session');
      return;
    }

    if (failure.kind === 'forbidden') {
      this.end('profile');
      return;
    }

    if (failure.kind === 'concurrent-change') {
      this.staleEdit.set(true);
      const refreshed = await this.reloadAccounts();
      if (this.state() === 'ended') {
        return;
      }
      this.serverProblems.set({
        form: refreshed
          ? 'La cuenta cambió durante la edición. Conserva tus cambios, cierra el formulario y vuelve a abrirlo para revisar los datos actuales.'
          : 'La cuenta cambió y no se pudo actualizar la lista. Conserva tus cambios y recarga la página antes de volver a editar.',
      });
      return;
    }

    if (failure.kind === 'invitation-failed') {
      const id = failure.accountId ?? draft.id;
      await this.reloadAccounts();

      if (this.state() === 'ended') {
        return;
      }

      this.saving.set(false);

      if (id && this.accountById(id)) {
        this.markMailFailure(id);
        this.serverProblems.set({});
        this.panel.set({ kind: 'detail', id });
        this.actionError.set(invitationFailedMessage(failure));
        return;
      }

      this.serverProblems.set({
        form: `${invitationFailedMessage(failure)} Actualiza la lista antes de volver a intentarlo.`,
      });
      return;
    }

    this.serverProblems.set(problemsFromFailure(failure));
  }

  private async commitStatus(id: string): Promise<void> {
    const account = this.accountById(id);

    if (!account) {
      return;
    }

    this.busyId.set(id);
    this.actionError.set(null);

    const result = await this.api.setStatus(id, account.status === 'Inactiva');

    this.busyId.set(null);

    if (!result.ok) {
      this.handleActionFailure(result.failure);
      return;
    }

    this.upsert(result.value);
    this.notice.set(
      result.value.status === 'Inactiva'
        ? 'Acceso desactivado. Sus datos se conservan.'
        : 'Cuenta reactivada. Las sesiones anteriores no se restauran.',
    );
  }

  private async commitResend(id: string): Promise<void> {
    this.busyId.set(id);
    this.actionError.set(null);

    const result = await this.api.resendInvitation(id);

    this.busyId.set(null);

    if (!result.ok) {
      if (result.failure.kind === 'invitation-failed') {
        this.markMailFailure(id);
        this.actionError.set(invitationFailedMessage(result.failure));
        return;
      }

      this.handleActionFailure(result.failure);
      return;
    }

    this.clearMailFailure(id);
    this.upsert(result.value);
    this.actionError.set(null);
    this.notice.set('Nueva invitación enviada. El enlace anterior ya no sirve.');
  }

  private handleActionFailure(failure: StaffFailure): void {
    switch (failure.kind) {
      case 'unauthorized':
        this.end('session');
        return;
      case 'forbidden':
        this.end('profile');
        return;
      case 'not-found':
        this.actionError.set('La cuenta ya no existe. Actualiza la lista y vuelve a intentarlo.');
        void this.reloadAccounts();
        return;
      case 'last-administrator':
        this.actionError.set(failure.detail ?? LAST_ADMINISTRATOR_MESSAGE);
        return;
      default: {
        const detail = 'detail' in failure ? failure.detail : undefined;

        this.actionError.set(
          failure.kind === 'unavailable'
            ? 'La operación no se completó. Revisa tu conexión y vuelve a intentarlo.'
            : (detail ?? 'La operación no se completó. Vuelve a intentarlo.'),
        );
      }
    }
  }

  private async reloadAccounts(): Promise<boolean> {
    const result = await this.api.list();

    if (result.ok) {
      this.accounts.set(result.value);
      return true;
    }

    if (result.failure.kind === 'unauthorized') {
      this.end('session');
      return false;
    }

    if (result.failure.kind === 'forbidden') {
      this.end('profile');
    }
    return false;
  }

  private upsert(account: StaffAccountResponse): void {
    this.accounts.update((accounts) => {
      const exists = accounts.some((candidate) => candidate.id === account.id);

      return exists
        ? accounts.map((candidate) => (candidate.id === account.id ? account : candidate))
        : [...accounts, account];
    });
  }

  private markMailFailure(id: string): void {
    const next = new Set(this.mailFailures());
    next.add(id);
    this.mailFailures.set(next);
  }

  private clearMailFailure(id: string): void {
    if (!this.mailFailures().has(id)) {
      return;
    }

    const next = new Set(this.mailFailures());
    next.delete(id);
    this.mailFailures.set(next);
  }
}

function toStaffLocal(local: LocalResponse): StaffLocal {
  return { id: local.id, name: local.name, isActive: local.isActive };
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0))
    .join('')
    .toUpperCase();
}

function normalize(value: string): string {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();
}

function statusKey(account: StaffAccountResponse): Exclude<StatusFilter, 'all'> {
  return account.status === 'Activa'
    ? 'active'
    : account.status === 'Pendiente'
      ? 'pending'
      : 'inactive';
}

function invitationFailedMessage(failure: StaffFailure): string {
  return failure.kind === 'invitation-failed' && failure.detail
    ? failure.detail
    : 'La cuenta está guardada, pero el correo no se pudo enviar. Puedes reenviar la invitación sin crear otra cuenta.';
}
