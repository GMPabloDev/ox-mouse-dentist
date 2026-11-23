import { signal } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import type { EspecialidadResponse, ServicioResponse } from '@mousedent/api-client';
import { SessionService } from '../../core/auth/session';
import {
  CATALOGO_API,
  type CatalogoApi,
  type CatalogoResult,
} from '../../core/catalogo/catalogo-api';
import { CatalogoPage } from './catalogo-page';

const toasts = vi.hoisted(() => ({ success: vi.fn() }));
vi.mock('@spartan-ng/brain/sonner', () => ({ toast: { success: toasts.success } }));

const SPECIALTY: EspecialidadResponse = {
  id: 's1',
  name: 'General',
  description: null,
  isActive: true,
};

const SERVICE: ServicioResponse = {
  id: 'v1',
  name: 'Limpieza',
  description: null,
  especialidadId: 's1',
  isActive: true,
};

function ok<T>(value: T): CatalogoResult<T> {
  return { ok: true, value };
}

function catalogoApi(overrides: Partial<CatalogoApi> = {}): CatalogoApi {
  return {
    especialidades: vi.fn<CatalogoApi['especialidades']>(async () => ok([SPECIALTY])),
    servicios: vi.fn<CatalogoApi['servicios']>(async () => ok([SERVICE])),
    createEspecialidad: vi.fn<CatalogoApi['createEspecialidad']>(async () => ok(SPECIALTY)),
    updateEspecialidad: vi.fn<CatalogoApi['updateEspecialidad']>(async () => ok(SPECIALTY)),
    createServicio: vi.fn<CatalogoApi['createServicio']>(async () => ok(SERVICE)),
    updateServicio: vi.fn<CatalogoApi['updateServicio']>(async () => ok(SERVICE)),
    setEspecialidadStatus: vi.fn<CatalogoApi['setEspecialidadStatus']>(async () => ok(SPECIALTY)),
    setServicioStatus: vi.fn<CatalogoApi['setServicioStatus']>(async () => ok(SERVICE)),
    ...overrides,
  };
}

async function render(options: { admin?: boolean; api?: CatalogoApi } = {}) {
  const api = options.api ?? catalogoApi();
  TestBed.configureTestingModule({
    providers: [
      { provide: CATALOGO_API, useValue: api },
      { provide: SessionService, useValue: { isAdministrator: signal(options.admin ?? true) } },
    ],
  });
  const fixture = TestBed.createComponent(CatalogoPage);
  await update(fixture);

  return { fixture, api };
}

function button(host: HTMLElement, text: string): HTMLButtonElement {
  return [...host.querySelectorAll('button')].find((candidate) =>
    candidate.textContent?.includes(text),
  )!;
}

async function update(fixture: ComponentFixture<CatalogoPage>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

/** Deja correr el popover del select de Spartan, que se monta fuera del fixture. */
async function flush(fixture: ComponentFixture<CatalogoPage>): Promise<void> {
  for (let round = 0; round < 3; round += 1) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
  }

  fixture.detectChanges();
}

function filterOptions(): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>('[role="option"]')];
}

/** Abre el select de filtros y devuelve sus opciones, como las ve quien filtra. */
async function openFilter(
  fixture: ComponentFixture<CatalogoPage>,
  id: string,
): Promise<HTMLElement[]> {
  const trigger = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(`#${id}`);

  if (!trigger) {
    throw new Error(`No se encontró el filtro «${id}»`);
  }

  if (trigger.getAttribute('aria-expanded') !== 'true') {
    trigger.click();
    await flush(fixture);
  }

  return filterOptions();
}

async function chooseFilter(
  fixture: ComponentFixture<CatalogoPage>,
  id: string,
  label: string,
): Promise<void> {
  const options = await openFilter(fixture, id);
  const option = options.find((item) => item.textContent?.includes(label));

  if (!option) {
    throw new Error(`No se encontró la opción «${label}»`);
  }

  option.click();
  await flush(fixture);
}

/** Mensaje del último aviso de éxito, si lo hubo. */
function lastToast(): string | undefined {
  return toasts.success.mock.lastCall?.[0] as string | undefined;
}

async function fillName(fixture: ComponentFixture<CatalogoPage>, name: string): Promise<void> {
  const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
    'input[name="name"]',
  )!;
  input.value = name;
  input.dispatchEvent(new Event('input'));
  await update(fixture);
}

async function submitForm(fixture: ComponentFixture<CatalogoPage>): Promise<void> {
  (fixture.nativeElement as HTMLElement)
    .querySelector('form')!
    .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  await update(fixture);
}

async function confirmStatus(
  fixture: ComponentFixture<CatalogoPage>,
  itemLabel: string,
  actionLabel: string,
): Promise<void> {
  const host = fixture.nativeElement as HTMLElement;
  button(host, itemLabel).click();
  await update(fixture);
  button(host, actionLabel).click();
  await update(fixture);
  const dialog = host.querySelector<HTMLElement>('[role="alertdialog"]')!;
  button(dialog, actionLabel.split(' ')[0]).click();
  await update(fixture);
}

describe('Catálogo', () => {
  beforeEach(() => toasts.success.mockClear());
  afterEach(() => TestBed.resetTestingModule());

  it('permite buscar servicios y limpiar la búsqueda', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;
    const search = host.querySelector<HTMLInputElement>('#catalog-search')!;
    search.value = 'inexistente';
    search.dispatchEvent(new Event('input'));
    await update(fixture);

    expect(host.textContent).toContain('No encontramos coincidencias');

    button(host, 'Limpiar búsqueda').click();
    await update(fixture);

    expect(host.textContent).toContain('Limpieza');
  });

  it.each(['Recepción', 'Profesional'])('consulta sin gestión para %s', async () => {
    const { fixture } = await render({ admin: false });
    const host = fixture.nativeElement as HTMLElement;

    expect(host.textContent).toContain('Limpieza');
    expect(host.textContent).not.toContain('Nueva especialidad');
    expect(host.querySelector('#catalog-specialty')).not.toBeNull();
    expect(host.querySelector('#catalog-status')).toBeNull();

    button(host, 'Limpieza').click();
    await update(fixture);

    expect(host.textContent).not.toContain('Desactivar');
    expect(host.textContent).not.toContain('Editar');
  });

  it('explica el bloqueo de desactivar una especialidad con servicios activos', async () => {
    const { fixture, api } = await render();
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'General').click();
    await update(fixture);

    button(host, 'Desactivar especialidad').click();
    await update(fixture);

    expect(host.textContent).toContain('Desactívalos primero');
    expect(api.setEspecialidadStatus).not.toHaveBeenCalled();
  });

  it('explica el estado de un registro inactivo en el detalle', async () => {
    const api = catalogoApi({
      especialidades: vi.fn(async () => ok([{ ...SPECIALTY, isActive: false }])),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;

    button(host, 'General').click();
    await update(fixture);

    const drawer = host.querySelector<HTMLElement>('[role="dialog"]')!;

    expect(drawer.textContent).toContain(
      'Este registro no aparece en la consulta del equipo. Puedes reactivarlo.',
    );
    expect(drawer.textContent).not.toContain('Al desactivar');
  });

  it('conserva el formulario cuando la API rechaza un duplicado', async () => {
    const api = catalogoApi({
      createEspecialidad: vi.fn(async () => ({
        ok: false as const,
        failure: { kind: 'rejected' as const, detail: 'Nombre duplicado' },
      })),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Nueva especialidad').click();
    await update(fixture);
    const input = host.querySelector<HTMLInputElement>('input[name="name"]')!;
    input.value = 'General';
    input.dispatchEvent(new Event('input'));
    await update(fixture);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await update(fixture);

    expect(host.textContent).toContain('Nombre duplicado');
    expect(input.value).toBe('General');
    expect(toasts.success).not.toHaveBeenCalled();
  });

  it('envía el nombre al servidor y muestra su error de longitud en el campo', async () => {
    const api = catalogoApi({
      createEspecialidad: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'validation' as const,
          errors: { Name: ['El nombre debe tener entre 1 y 100 caracteres.'] },
        },
      })),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Nueva especialidad').click();
    await update(fixture);
    const input = host.querySelector<HTMLInputElement>('input[name="name"]')!;
    input.value = 'x'.repeat(101);
    input.dispatchEvent(new Event('input'));
    await update(fixture);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await update(fixture);

    expect(api.createEspecialidad).toHaveBeenCalledWith({
      name: 'x'.repeat(101),
      description: null,
    });
    expect(input.parentElement!.textContent).toContain(
      'El nombre debe tener entre 1 y 100 caracteres.',
    );
    expect(host.textContent).not.toContain('Escribe un nombre de hasta 100 caracteres');
  });

  it('convierte una descripción vacía en null y ubica el error en su campo', async () => {
    const api = catalogoApi({
      createEspecialidad: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'validation' as const,
          errors: { Description: ['La descripción no puede superar 500 caracteres.'] },
        },
      })),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Nueva especialidad').click();
    await update(fixture);
    const name = host.querySelector<HTMLInputElement>('input[name="name"]')!;
    name.value = 'General';
    name.dispatchEvent(new Event('input'));
    const description = host.querySelector<HTMLTextAreaElement>('textarea[name="description"]')!;
    description.value = '   ';
    description.dispatchEvent(new Event('input'));
    await update(fixture);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await update(fixture);

    expect(api.createEspecialidad).toHaveBeenCalledWith({
      name: 'General',
      description: null,
    });
    expect(description.parentElement!.textContent).toContain(
      'La descripción no puede superar 500 caracteres.',
    );
  });

  it('no crea un servicio sin especialidad y señala el campo', async () => {
    const { fixture, api } = await render();
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Agregar servicio').click();
    await update(fixture);
    const name = host.querySelector<HTMLInputElement>('input[name="name"]')!;
    name.value = 'Blanqueamiento';
    name.dispatchEvent(new Event('input'));
    const specialty = host.querySelector<HTMLSelectElement>('select[name="specialty"]')!;
    specialty.value = '';
    specialty.dispatchEvent(new Event('change'));
    await update(fixture);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await update(fixture);

    expect(api.createServicio).not.toHaveBeenCalled();
    expect(specialty.parentElement!.textContent).toContain('Selecciona una especialidad.');
  });

  it('muestra el servicio buscado aunque su grupo esté colapsado', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;
    host.querySelector<HTMLButtonElement>('button[aria-label="Contraer General"]')!.click();
    await update(fixture);

    expect(host.textContent).not.toContain('Limpieza');

    const search = host.querySelector<HTMLInputElement>('#catalog-search')!;
    search.value = 'limpieza';
    search.dispatchEvent(new Event('input'));
    await update(fixture);

    expect(host.textContent).toContain('Limpieza');
  });

  it('despliega la especialidad destino al guardar un servicio', async () => {
    const created: ServicioResponse = { ...SERVICE, id: 'v2', name: 'Blanqueamiento' };
    const servicios = vi
      .fn<CatalogoApi['servicios']>()
      .mockResolvedValueOnce(ok([SERVICE]))
      .mockResolvedValue(ok([SERVICE, created]));
    const { fixture } = await render({ api: catalogoApi({ servicios }) });
    const host = fixture.nativeElement as HTMLElement;

    button(host, 'Agregar servicio').click();
    await update(fixture);

    // El grupo se contrae mientras el formulario sigue abierto.
    host.querySelector<HTMLButtonElement>('button[aria-label="Contraer General"]')!.click();
    await update(fixture);
    expect(host.textContent).not.toContain('Blanqueamiento');

    const name = host.querySelector<HTMLInputElement>('input[name="name"]')!;
    name.value = 'Blanqueamiento';
    name.dispatchEvent(new Event('input'));
    await update(fixture);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await update(fixture);

    expect(host.textContent).toContain('Blanqueamiento');
  });

  it('descarta la confirmación pendiente al abrir el formulario', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Limpieza').click();
    await update(fixture);
    button(host, 'Desactivar servicio').click();
    await update(fixture);

    expect(host.querySelector('[role="alertdialog"]')).not.toBeNull();

    button(host, 'Editar').click();
    await update(fixture);

    expect(host.querySelector('[role="alertdialog"]')).toBeNull();
  });

  it('abre la confirmación con el foco en Cancelar y los textos asociados', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Limpieza').click();
    await update(fixture);
    button(host, 'Desactivar servicio').click();
    await update(fixture);

    const dialog = host.querySelector<HTMLElement>('[role="alertdialog"]')!;
    const cancel = button(dialog, 'Cancelar');

    expect(dialog.contains(document.activeElement)).toBe(true);
    expect(document.activeElement).toBe(cancel);
    expect(document.getElementById(dialog.getAttribute('aria-labelledby')!)?.textContent).toContain(
      '¿Desactivar servicio?',
    );
    expect(
      document.getElementById(dialog.getAttribute('aria-describedby')!)?.textContent,
    ).toContain('dejará de aparecer');
  });

  it('cancela la confirmación con Escape y deja el detalle abierto', async () => {
    const { fixture, api } = await render();
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Limpieza').click();
    await update(fixture);
    button(host, 'Desactivar servicio').click();
    await update(fixture);

    document.activeElement!.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );
    await update(fixture);

    expect(host.querySelector('[role="alertdialog"]')).toBeNull();
    expect(host.querySelector('[role="dialog"]')).not.toBeNull();
    expect(button(host, 'Desactivar servicio')).toBeDefined();
    expect(document.activeElement).toBe(button(host, 'Desactivar servicio'));
    expect(api.setServicioStatus).not.toHaveBeenCalled();
  });

  it('muestra la confirmación fuera del panel que bloquea', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Limpieza').click();
    await update(fixture);
    button(host, 'Desactivar servicio').click();
    await update(fixture);

    const blocked = host.querySelector('[inert]');
    const dialog = host.querySelector('[role="alertdialog"]');

    expect(blocked).not.toBeNull();
    expect(dialog).not.toBeNull();
    expect(blocked!.contains(dialog)).toBe(false);
  });

  it('ofrece reintento tras error de consulta', async () => {
    const especialidades = vi.fn<CatalogoApi['especialidades']>();
    especialidades.mockResolvedValue(ok([SPECIALTY]));
    especialidades.mockResolvedValueOnce({ ok: false, failure: { kind: 'unavailable' } });
    const api = catalogoApi({ especialidades });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;

    expect(host.textContent).toContain('No pudimos cargar');

    button(host, 'Reintentar').click();
    await update(fixture);

    expect(host.textContent).toContain('General');
  });

  it('filtra por especialidad y estado con los selects de Spartan', async () => {
    const api = catalogoApi({
      especialidades: vi.fn(async () =>
        ok([
          SPECIALTY,
          { id: 's2', name: 'Ortodoncia', description: null, isActive: false },
        ] satisfies EspecialidadResponse[]),
      ),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelector('label[for="catalog-specialty"]')).not.toBeNull();
    expect(host.querySelector('label[for="catalog-status"]')).not.toBeNull();
    expect(host.querySelector('#catalog-specialty')!.textContent).toContain(
      'Todas las especialidades',
    );

    const specialtyOptions = await openFilter(fixture, 'catalog-specialty');
    expect(specialtyOptions.map((option) => option.textContent!.trim())).toEqual([
      'Todas las especialidades',
      'General',
      'Ortodoncia · Inactiva',
    ]);

    await chooseFilter(fixture, 'catalog-specialty', 'Ortodoncia');
    expect(host.querySelector('#catalog-specialty')!.textContent).toContain('Ortodoncia');
    expect(host.textContent).not.toContain('Limpieza');

    await chooseFilter(fixture, 'catalog-specialty', 'Todas las especialidades');
    expect(host.textContent).toContain('Limpieza');

    await chooseFilter(fixture, 'catalog-status', 'Inactivos');
    expect(host.textContent).toContain('Ortodoncia');
    expect(host.textContent).not.toContain('Limpieza');

    await chooseFilter(fixture, 'catalog-specialty', 'General');
    expect(host.textContent).toContain('No encontramos coincidencias');

    button(host, 'Limpiar filtros').click();
    await update(fixture);

    expect(host.querySelector('#catalog-specialty')!.textContent).toContain(
      'Todas las especialidades',
    );
    expect(host.querySelector('#catalog-status')!.textContent).toContain('Todos');
    expect(host.textContent).toContain('Limpieza');
  });

  it('explica al pie que las especialidades y los servicios se conservan', async () => {
    const { fixture } = await render();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.textContent).toContain(
      'Las especialidades y los servicios se conservan al desactivarlos.',
    );
    expect(host.textContent).not.toContain('El catálogo es compartido por todos los locales');
  });

  it('cuenta en el detalle todos los servicios de la especialidad para administración', async () => {
    const api = catalogoApi({
      servicios: vi.fn(async () =>
        ok([SERVICE, { ...SERVICE, id: 'v2', name: 'Blanqueamiento', isActive: false }]),
      ),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'General').click();
    await update(fixture);

    const drawer = host.querySelector<HTMLElement>('[role="dialog"]')!;
    const text = drawer.textContent!;

    expect(text).toContain('Servicios');
    expect(text).toContain('2 en esta especialidad');
    expect(text.indexOf('Servicios')).toBeLessThan(text.indexOf('Descripción'));
  });

  it('cuenta solo los servicios activos de la especialidad para recepción', async () => {
    const api = catalogoApi({
      servicios: vi.fn(async () =>
        ok([SERVICE, { ...SERVICE, id: 'v2', name: 'Blanqueamiento', isActive: false }]),
      ),
    });
    const { fixture } = await render({ admin: false, api });
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'General').click();
    await update(fixture);

    const drawer = host.querySelector<HTMLElement>('[role="dialog"]')!;

    expect(drawer.textContent).toContain('1 en esta especialidad');
  });

  interface NoticeCase {
    readonly name: string;
    readonly message: string;
    readonly api?: Partial<CatalogoApi>;
    readonly run: (fixture: ComponentFixture<CatalogoPage>) => Promise<void>;
  }

  const NOTICE_CASES: readonly NoticeCase[] = [
    {
      name: 'crear una especialidad',
      message: 'Especialidad creada.',
      run: async (fixture) => {
        button(fixture.nativeElement as HTMLElement, 'Nueva especialidad').click();
        await update(fixture);
        await fillName(fixture, 'Ortodoncia');
        await submitForm(fixture);
      },
    },
    {
      name: 'editar una especialidad',
      message: 'Especialidad actualizada.',
      run: async (fixture) => {
        const host = fixture.nativeElement as HTMLElement;
        button(host, 'General').click();
        await update(fixture);
        button(host, 'Editar').click();
        await update(fixture);
        await submitForm(fixture);
      },
    },
    {
      name: 'crear un servicio',
      message: 'Servicio creado.',
      run: async (fixture) => {
        button(fixture.nativeElement as HTMLElement, 'Agregar servicio').click();
        await update(fixture);
        await fillName(fixture, 'Blanqueamiento');
        await submitForm(fixture);
      },
    },
    {
      name: 'editar un servicio',
      message: 'Servicio actualizado.',
      run: async (fixture) => {
        const host = fixture.nativeElement as HTMLElement;
        button(host, 'Limpieza').click();
        await update(fixture);
        button(host, 'Editar').click();
        await update(fixture);
        await submitForm(fixture);
      },
    },
    {
      name: 'desactivar una especialidad',
      message: 'Especialidad desactivada.',
      api: { servicios: vi.fn(async () => ok<readonly ServicioResponse[]>([])) },
      run: async (fixture) => confirmStatus(fixture, 'General', 'Desactivar especialidad'),
    },
    {
      name: 'reactivar una especialidad',
      message: 'Especialidad reactivada.',
      api: {
        especialidades: vi.fn(async () =>
          ok([{ ...SPECIALTY, isActive: false }] satisfies EspecialidadResponse[]),
        ),
        servicios: vi.fn(async () => ok<readonly ServicioResponse[]>([])),
      },
      run: async (fixture) => confirmStatus(fixture, 'General', 'Reactivar especialidad'),
    },
    {
      name: 'desactivar un servicio',
      message: 'Servicio desactivado.',
      run: async (fixture) => confirmStatus(fixture, 'Limpieza', 'Desactivar servicio'),
    },
    {
      name: 'reactivar un servicio',
      message: 'Servicio reactivado.',
      api: {
        servicios: vi.fn(async () =>
          ok([{ ...SERVICE, isActive: false }] satisfies ServicioResponse[]),
        ),
      },
      run: async (fixture) => confirmStatus(fixture, 'Limpieza', 'Reactivar servicio'),
    },
  ];

  it.each(NOTICE_CASES)('avisa del éxito después de $name', async ({ api, message, run }) => {
    const rendered = await render({ api: catalogoApi(api) });

    await run(rendered.fixture);

    expect(lastToast()).toBe(message);
  });

  it('no avisa de éxito cuando el cambio de estado falla', async () => {
    const api = catalogoApi({
      setServicioStatus: vi.fn(async () => ({
        ok: false as const,
        failure: { kind: 'unavailable' as const },
      })),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;

    await confirmStatus(fixture, 'Limpieza', 'Desactivar servicio');

    expect(host.textContent).toContain('No se pudo cambiar el estado');
    expect(toasts.success).not.toHaveBeenCalled();
  });

  it('cambia el estado de un servicio y actualiza el detalle', async () => {
    const api = catalogoApi({
      setServicioStatus: vi.fn(async () => ok({ ...SERVICE, isActive: false })),
    });
    const { fixture } = await render({ api });
    const host = fixture.nativeElement as HTMLElement;
    button(host, 'Limpieza').click();
    await update(fixture);
    button(host, 'Desactivar servicio').click();
    await update(fixture);
    button(host.querySelector<HTMLElement>('[role="alertdialog"]')!, 'Desactivar').click();
    await update(fixture);

    expect(api.setServicioStatus).toHaveBeenCalledWith('v1', false);
    expect(host.textContent).toContain('Reactivar servicio');
  });

  it('muestra el catálogo vacío sin confundirlo con una búsqueda', async () => {
    const api = catalogoApi({
      especialidades: vi.fn(async () => ok<readonly EspecialidadResponse[]>([])),
      servicios: vi.fn(async () => ok<readonly ServicioResponse[]>([])),
    });
    const { fixture } = await render({ admin: false, api });
    const host = fixture.nativeElement as HTMLElement;

    expect(host.textContent).toContain('El catálogo está vacío');
    expect(host.textContent).not.toContain('Nueva especialidad');
  });
});
