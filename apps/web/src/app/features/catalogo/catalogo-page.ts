import { CdkTrapFocus } from '@angular/cdk/a11y';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { EspecialidadResponse, ServicioResponse } from '@mousedent/api-client';
import { toast } from '@spartan-ng/brain/sonner';
import {
  HlmSelect,
  HlmSelectContent,
  HlmSelectItem,
  HlmSelectPortal,
  HlmSelectTrigger,
  HlmSelectValue,
} from '@mousedent/ui/select';
import { SessionService } from '../../core/auth/session';
import { CATALOGO_API, type CatalogoFailure } from '../../core/catalogo/catalogo-api';
import { StaffDrawer } from '../staff/staff-drawer';

type Item = EspecialidadResponse | ServicioResponse;
type Kind = 'especialidad' | 'servicio';

/** Filtro sin especialidad. El select trata la cadena vacía como ausencia de valor. */
const ALL_SPECIALTIES = '__todas__';

@Component({
  selector: 'app-catalogo-page',
  imports: [
    CdkTrapFocus,
    FormsModule,
    HlmSelect,
    HlmSelectContent,
    HlmSelectItem,
    HlmSelectPortal,
    HlmSelectTrigger,
    HlmSelectValue,
    StaffDrawer,
  ],
  templateUrl: './catalogo-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogoPage {
  private readonly api = inject(CATALOGO_API);
  protected readonly admin = inject(SessionService).isAdministrator;
  protected readonly specialties = signal<EspecialidadResponse[]>([]);
  protected readonly services = signal<ServicioResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  /** Valor del select de especialidad para «todas»; el filtro guarda la cadena vacía. */
  protected readonly allSpecialties = ALL_SPECIALTIES;
  /** Errores por campo que devuelve la API en el ProblemDetails de validación. */
  protected readonly fieldErrors = signal<Readonly<Record<string, readonly string[]>>>({});
  protected readonly query = signal('');
  protected readonly filterSpecialty = signal('');
  protected readonly filterStatus = signal('all');
  protected readonly collapsed = signal<Set<string>>(new Set());
  protected readonly selected = signal<{
    kind: Kind;
    item: Item | null;
    mode: 'detail' | 'form';
  } | null>(null);
  protected readonly confirming = signal(false);
  /** Cambio de estado pendiente de confirmación, en una capa sobre el panel. */
  protected readonly confirmation = computed(() => {
    const selected = this.selected();
    if (!this.confirming() || !selected?.item) return null;
    return { kind: selected.kind, item: selected.item };
  });
  private readonly cancelConfirmationButton = viewChild<ElementRef<HTMLButtonElement>>(
    'cancelConfirmationButton',
  );
  private readonly statusTrigger = viewChild<ElementRef<HTMLButtonElement>>('statusTrigger');
  /** La confirmación se canceló: hay que devolver el foco al panel al cerrarse. */
  private restoreFocus = false;
  protected name = '';
  protected description = '';
  protected specialtyId = '';
  // El filtrado es local a propósito: la vista agrupa por especialidad y necesita
  // las listas completas para los recuentos y el desplegable de especialidades.
  // La búsqueda normaliza igual que el servidor: espacios exteriores fuera y
  // minúsculas sin configuración regional.
  protected readonly groups = computed(() => {
    const query = this.query().trim().toLowerCase();
    const status = this.filterStatus();
    const matches = (item: Item) => status === 'all' || item.isActive === (status === 'active');
    return this.specialties()
      .filter((s) => !this.filterSpecialty() || s.id === this.filterSpecialty())
      .map((s) => {
        const children = this.services().filter((x) => x.especialidadId === s.id);
        const parentMatches = !query || s.name.toLowerCase().includes(query);
        const visible = children.filter(
          (x) => matches(x) && (parentMatches || x.name.toLowerCase().includes(query)),
        );
        return {
          specialty: s,
          children,
          visible,
          show: (parentMatches && matches(s)) || visible.length > 0,
        };
      })
      .filter((g) => g.show);
  });
  constructor() {
    // La confirmación aparece sobre un panel inert: el foco entra en ella en
    // cuanto está en el DOM. Al cancelarla, el panel deja de estar inert y el
    // foco vuelve al botón que la abrió.
    effect(() => {
      if (this.confirmation()) {
        this.cancelConfirmationButton()?.nativeElement.focus({ preventScroll: true });
        return;
      }

      if (this.restoreFocus) {
        this.restoreFocus = false;
        this.statusTrigger()?.nativeElement.focus({ preventScroll: true });
      }
    });
    void this.load();
  }
  protected async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    const [specialties, services] = await Promise.all([
      this.api.especialidades(),
      this.api.servicios(),
    ]);
    if (specialties.ok && services.ok) {
      this.specialties.set([...specialties.value]);
      this.services.set([...services.value]);
    } else {
      this.loadError.set(true);
    }
    this.loading.set(false);
  }
  /** Cada búsqueda despliega los grupos para que el resultado se vea. */
  protected search(query: string): void {
    this.query.set(query);
    this.collapsed.set(new Set());
  }
  protected clear(): void {
    this.query.set('');
    this.filterSpecialty.set('');
    this.filterStatus.set('all');
  }
  /** El select muestra la etiqueta del valor; el filtro guarda el identificador. */
  protected specialtyFilterLabel = (value: string | null | undefined): string => {
    if (!value || value === ALL_SPECIALTIES) {
      return 'Todas las especialidades';
    }

    const specialty = this.specialties().find((s) => s.id === value);

    return specialty ? `${specialty.name}${specialty.isActive ? '' : ' · Inactiva'}` : '';
  };
  protected statusFilterLabel = (value: string | null | undefined): string =>
    value === 'active' ? 'Activos' : value === 'inactive' ? 'Inactivos' : 'Todos';
  protected setFilterSpecialty(value: string | null | undefined): void {
    this.filterSpecialty.set(!value || value === ALL_SPECIALTIES ? '' : value);
  }
  protected setFilterStatus(value: string | null | undefined): void {
    this.filterStatus.set(value === 'active' || value === 'inactive' ? value : 'all');
  }
  /** Servicios de la especialidad: administración cuenta todos, el resto solo los activos. */
  protected specialtyServiceCount(id: string): number {
    return this.services().filter(
      (service) => service.especialidadId === id && (this.admin() || service.isActive),
    ).length;
  }
  protected toggle(id: string): void {
    const next = new Set(this.collapsed());
    next.has(id) ? next.delete(id) : next.add(id);
    this.collapsed.set(next);
  }
  protected detail(kind: Kind, item: Item): void {
    this.error.set('');
    this.fieldErrors.set({});
    this.confirming.set(false);
    this.selected.set({ kind, item, mode: 'detail' });
  }
  protected form(kind: Kind, item: Item | null = null, specialtyId = ''): void {
    if (!this.admin()) return;
    this.name = item?.name ?? '';
    this.description = item?.description ?? '';
    this.specialtyId = item && 'especialidadId' in item ? item.especialidadId : specialtyId;
    this.error.set('');
    this.fieldErrors.set({});
    this.confirming.set(false);
    this.selected.set({ kind, item, mode: 'form' });
  }
  protected close(): void {
    if (!this.saving()) {
      this.selected.set(null);
      this.confirming.set(false);
      this.error.set('');
      this.fieldErrors.set({});
    }
  }
  /** Abre la confirmación de estado desde el detalle. */
  protected confirmStatus(): void {
    this.confirming.set(true);
  }
  /** Cierra solo la confirmación; el panel que sigue abierto no se toca. */
  protected cancelConfirmation(): void {
    this.restoreFocus = true;
    this.confirming.set(false);
  }
  /** Escape cancela la confirmación; el panel que la abrió no se cierra. */
  protected cancelConfirmationWithEscape(event: Event): void {
    event.stopPropagation();
    this.cancelConfirmation();
  }
  protected async save(): Promise<void> {
    const selected = this.selected();
    if (!selected || !this.admin() || this.saving()) return;
    this.error.set('');
    this.fieldErrors.set({});
    // El contrato exige un Guid de especialidad: sin ella no hay cuerpo que
    // enviar, así que el formulario la señala antes de llamar a la API.
    if (selected.kind === 'servicio' && !this.specialtyId) {
      this.fieldErrors.set({ EspecialidadId: ['Selecciona una especialidad.'] });
      return;
    }
    this.saving.set(true);
    const body = {
      name: this.name.trim(),
      description: this.description.trim() || null,
    };
    const result =
      selected.kind === 'especialidad'
        ? selected.item
          ? await this.api.updateEspecialidad(selected.item.id, body)
          : await this.api.createEspecialidad(body)
        : selected.item
          ? await this.api.updateServicio(selected.item.id, {
              ...body,
              especialidadId: this.specialtyId,
            })
          : await this.api.createServicio({ ...body, especialidadId: this.specialtyId });
    this.saving.set(false);
    if (!result.ok) {
      this.fieldErrors.set(result.failure.kind === 'validation' ? result.failure.errors : {});
      this.error.set(failureMessage(result.failure));
      return;
    }
    this.selected.set(null);
    // El servicio guardado puede quedar oculto si su especialidad está
    // contraída: se despliega el destino, como en la referencia.
    if (selected.kind === 'servicio' && this.specialtyId) {
      const collapsed = new Set(this.collapsed());
      collapsed.delete(this.specialtyId);
      this.collapsed.set(collapsed);
    }
    toast.success(
      selected.kind === 'especialidad'
        ? selected.item
          ? 'Especialidad actualizada.'
          : 'Especialidad creada.'
        : selected.item
          ? 'Servicio actualizado.'
          : 'Servicio creado.',
    );
    await this.load();
  }
  protected fieldError(field: string): string {
    return (this.fieldErrors()[field] ?? []).join(' ');
  }
  protected async changeStatus(): Promise<void> {
    const selected = this.selected();
    if (!selected?.item || !this.admin() || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    const isActive = !selected.item.isActive;
    const result =
      selected.kind === 'especialidad'
        ? await this.api.setEspecialidadStatus(selected.item.id, isActive)
        : await this.api.setServicioStatus(selected.item.id, isActive);
    this.saving.set(false);
    this.confirming.set(false);
    if (!result.ok) {
      this.error.set(failureMessage(result.failure, STATUS_ERROR));
      return;
    }
    this.selected.set({ ...selected, item: result.value });
    toast.success(
      selected.kind === 'especialidad'
        ? isActive
          ? 'Especialidad reactivada.'
          : 'Especialidad desactivada.'
        : isActive
          ? 'Servicio reactivado.'
          : 'Servicio desactivado.',
    );
    await this.load();
  }
  protected specialtyName(id: string): string {
    return this.specialties().find((s) => s.id === id)?.name ?? '';
  }
  protected blocked(item: Item, kind: Kind): string {
    if (
      kind === 'especialidad' &&
      item.isActive &&
      this.services().some((s) => s.especialidadId === item.id && s.isActive)
    )
      return 'Esta especialidad tiene servicios activos. Desactívalos primero para poder continuar.';
    if (
      kind === 'servicio' &&
      !item.isActive &&
      'especialidadId' in item &&
      !this.specialties().find((s) => s.id === item.especialidadId)?.isActive
    )
      return 'La especialidad está inactiva. Reactívala antes de activar este servicio.';
    return '';
  }
}
function failureMessage(failure: CatalogoFailure, fallback = SAVE_ERROR): string {
  if (failure.kind === 'forbidden') return 'No tienes permiso para realizar esta operación.';
  // Los errores de validación se muestran junto a cada campo del formulario.
  if (failure.kind === 'validation') return '';
  if (failure.kind === 'rejected') {
    return failure.detail ?? failure.title ?? fallback;
  }
  return fallback;
}

const SAVE_ERROR = 'No se pudo guardar. Tus datos siguen aquí. Vuelve a intentarlo.';
const STATUS_ERROR = 'No se pudo cambiar el estado. Vuelve a intentarlo.';
