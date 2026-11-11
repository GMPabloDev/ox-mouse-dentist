import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
  type WritableSignal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { LocalResponse, LocalScheduleDay } from '@mousedent/api-client';
import { HlmAlert } from '@mousedent/ui/alert';
import { HlmAvatar, HlmAvatarFallback, HlmAvatarImage } from '@mousedent/ui/avatar';
import { HlmBadge } from '@mousedent/ui/badge';
import { HlmButton } from '@mousedent/ui/button';
import {
  HlmSelect,
  HlmSelectContent,
  HlmSelectItem,
  HlmSelectPortal,
  HlmSelectTrigger,
  HlmSelectValue,
} from '@mousedent/ui/select';
import { OBJECT_URLS } from '../../core/api/object-urls';
import { BrandMark } from '../../core/brand/brand-mark';
import { LOCALES_API } from '../../core/locales/locales-api';
import { ConfirmDialog } from './confirm-dialog';
import { LocalIcon } from './local-icon';
import { LocalesNotice } from './locales-notice';
import { PhotoViewer } from './photo-viewer';
import { DAY_LETTER, DAY_NAMES, hoursLabel, normalizeWeek, openDayCount } from './schedule-model';

type LoadState = 'loading' | 'ready' | 'error';
type LocalFilter = 'all' | 'active' | 'inactive';

const FILTER_LABELS: Readonly<Record<LocalFilter, string>> = {
  all: 'Todos los estados',
  active: 'Activos',
  inactive: 'Inactivos',
};

interface StatusError {
  readonly id: string;
  readonly message: string;
}

/**
 * Listado A de la referencia: filas con fotografía. Consulta los locales activos
 * e inactivos, permite filtrarlos, editar sus datos y cambiar su estado. Las
 * fotos se piden autenticadas y se muestran desde una URL de objeto que se
 * libera al salir.
 */
@Component({
  selector: 'app-locales-page',
  imports: [
    RouterLink,
    BrandMark,
    ConfirmDialog,
    HlmAlert,
    HlmAvatar,
    HlmAvatarFallback,
    HlmAvatarImage,
    HlmBadge,
    HlmButton,
    HlmSelect,
    HlmSelectContent,
    HlmSelectItem,
    HlmSelectPortal,
    HlmSelectTrigger,
    HlmSelectValue,
    LocalIcon,
    PhotoViewer,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './locales-page.html',
})
export class LocalesPage {
  private readonly api = inject(LOCALES_API);
  private readonly urls = inject(OBJECT_URLS);
  private readonly notice = inject(LocalesNotice);
  private readonly destroyRef = inject(DestroyRef);

  private readonly photoUrls = new Map<string, string>();
  private readonly photoVersions = new Map<string, string>();

  protected readonly state = signal<LoadState>('loading');
  protected readonly locals = signal<readonly LocalResponse[]>([]);
  protected readonly filter = signal<LocalFilter>('all');
  protected readonly loadError = signal<string | null>(null);
  protected readonly savedNotice = signal<string | null>(this.notice.take());
  protected readonly pendingStatusId = signal<string | null>(null);
  protected readonly statusError = signal<StatusError | null>(null);
  protected readonly photos = signal<ReadonlyMap<string, string>>(new Map());
  protected readonly failedPhotos = signal<ReadonlySet<string>>(new Set());
  protected readonly confirmTarget = signal<LocalResponse | null>(null);
  protected readonly viewed = signal<LocalResponse | null>(null);

  protected readonly visible = computed(() => {
    const filter = this.filter();
    const locals = this.locals();

    return filter === 'all'
      ? locals
      : locals.filter((local) => (filter === 'active' ? local.isActive : !local.isActive));
  });

  protected readonly counts = computed(() => {
    const locals = this.locals();
    const active = locals.filter((local) => local.isActive).length;

    return { all: locals.length, active, inactive: locals.length - active };
  });

  /**
   * El input `class` de `hlm-select-trigger` se enlaza como propiedad. Con el
   * atributo estático, Angular deja además las clases en el host inline y su
   * fondo y su padding dibujan una caja fantasma alrededor del disparador.
   */
  protected readonly triggerClasses =
    'h-11 min-h-11 w-full gap-2 rounded-[10px] border-input bg-card px-[14px] py-0 text-[13px] leading-[1.2] font-semibold sm:w-fit sm:min-w-[220px] *:data-[slot=select-value]:min-w-0 *:data-[slot=select-value]:text-ellipsis';

  /** El punto del disparador resume el estado filtrado: teal salvo «Inactivos». */
  protected readonly filterDot = computed(() =>
    this.filter() === 'inactive' ? 'bg-muted-foreground' : 'bg-primary',
  );

  protected readonly countLabel = computed(() => {
    const count = this.counts().all;

    return count === 0
      ? 'Sin locales registrados'
      : count === 1
        ? '1 local registrado'
        : `${count} locales registrados`;
  });

  protected readonly viewedUrl = computed(() => {
    const local = this.viewed();

    return local ? (this.photos().get(local.id) ?? null) : null;
  });

  protected readonly viewedTitle = computed(() => {
    const local = this.viewed();

    return local ? `Foto de ${local.name}` : 'Foto del local';
  });

  constructor() {
    void this.load();
    this.destroyRef.onDestroy(() => this.releasePhotos());
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.loadError.set(null);

    const result = await this.api.list();

    if (!result.ok) {
      this.loadError.set(
        'No se pudieron cargar los locales. Revisa tu conexión e inténtalo de nuevo.',
      );
      this.state.set('error');
      return;
    }

    this.locals.set(result.value);
    this.state.set('ready');
    void this.loadPhotos(result.value);
  }

  protected setFilter(value: LocalFilter | null | undefined): void {
    if (value) {
      this.filter.set(value);
    }
  }

  /** El select de Spartan muestra la etiqueta del valor, no el valor interno. */
  protected filterLabel = (value: LocalFilter | null | undefined): string =>
    value ? FILTER_LABELS[value] : '';

  protected requestStatusChange(local: LocalResponse): void {
    this.statusError.set(null);
    this.confirmTarget.set(local);
  }

  protected cancelStatusChange(): void {
    this.confirmTarget.set(null);
  }

  protected async confirmStatusChange(): Promise<void> {
    const local = this.confirmTarget();

    if (!local) {
      return;
    }

    this.confirmTarget.set(null);
    this.pendingStatusId.set(local.id);
    this.statusError.set(null);

    const result = await this.api.setStatus(local.id, !local.isActive);

    this.pendingStatusId.set(null);

    if (!result.ok) {
      this.statusError.set({
        id: local.id,
        message: `No se pudo ${local.isActive ? 'desactivar' : 'reactivar'} el local. Inténtalo de nuevo.`,
      });
      return;
    }

    const updated = result.value;
    this.locals.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
  }

  protected openPhoto(local: LocalResponse): void {
    if (this.photos().get(local.id)) {
      this.viewed.set(local);
    }
  }

  protected statusLabel(local: LocalResponse): string {
    return local.isActive ? 'Activo' : 'Inactivo';
  }

  protected scheduleOf(local: LocalResponse): readonly LocalScheduleDay[] {
    return normalizeWeek(local.schedule);
  }

  protected attentionLabel(local: LocalResponse): string {
    const count = openDayCount(local.schedule);

    return count === 1 ? '1 día de atención' : `${count} días de atención`;
  }

  protected dayLetter(day: LocalScheduleDay): string {
    return DAY_LETTER[day.day];
  }

  protected dayTitle(day: LocalScheduleDay): string {
    return `${DAY_NAMES[day.day]}: ${hoursLabel(day)}`;
  }

  protected confirmTitle(local: LocalResponse): string {
    return local.isActive ? '¿Desactivar este local?' : '¿Reactivar este local?';
  }

  protected confirmMessage(local: LocalResponse): string {
    return local.isActive
      ? `${local.name} quedará inactivo. Sus datos, su horario y su foto se conservan.`
      : `${local.name} volverá a estar activo. Sus datos, su horario y su foto se conservan.`;
  }

  protected confirmLabel(local: LocalResponse): string {
    return local.isActive ? 'Desactivar local' : 'Reactivar local';
  }

  private async loadPhotos(locals: readonly LocalResponse[]): Promise<void> {
    for (const local of locals) {
      const photo = local.photo;

      if (!photo || this.photoVersions.get(local.id) === photo.version) {
        continue;
      }

      const result = await this.api.photo(local.id);

      if (!result.ok) {
        this.markPhoto(this.failedPhotos, local.id, true);
        continue;
      }

      const previous = this.photoUrls.get(local.id);
      if (previous) {
        this.urls.revoke(previous);
      }

      const url = this.urls.create(result.value);
      this.photoUrls.set(local.id, url);
      this.photoVersions.set(local.id, photo.version);
      this.markPhoto(this.failedPhotos, local.id, false);
      this.photos.set(new Map(this.photoUrls));
    }
  }

  private markPhoto(
    state: WritableSignal<ReadonlySet<string>>,
    id: string,
    present: boolean,
  ): void {
    const next = new Set(state());

    if (present) {
      next.add(id);
    } else {
      next.delete(id);
    }

    state.set(next);
  }

  private releasePhotos(): void {
    for (const url of this.photoUrls.values()) {
      this.urls.revoke(url);
    }

    this.photoUrls.clear();
    this.photoVersions.clear();
    this.photos.set(new Map());
  }
}
