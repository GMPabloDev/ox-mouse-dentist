import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { LocalResponse } from '@mousedent/api-client';
import { OBJECT_URLS } from '../../core/api/object-urls';
import { LOCALES_API, type LocalesApi } from '../../core/locales/locales-api';
import { LocalesNotice } from './locales-notice';
import { LocalesPage } from './locales-page';
import { applyRange, closedWeek, WEEK_DAYS } from './schedule-model';

const WEEK = applyRange(closedWeek(), new Set(WEEK_DAYS.slice(0, 6)), '09:00', '18:00');

function local(overrides: Partial<LocalResponse> = {}): LocalResponse {
  return {
    id: 'local-1',
    name: 'MouseDent Miraflores',
    address: 'Av. José Pardo 620, Miraflores',
    phone: '(01) 555 0120',
    isActive: true,
    schedule: WEEK,
    photo: null,
    ...overrides,
  };
}

function localesApi() {
  return {
    list: vi.fn<LocalesApi['list']>(async () => ({ ok: true as const, value: [] })),
    create: vi.fn<LocalesApi['create']>(async () => ({
      ok: false as const,
      failure: { kind: 'unavailable' as const },
    })),
    update: vi.fn<LocalesApi['update']>(async () => ({
      ok: false as const,
      failure: { kind: 'unavailable' as const },
    })),
    setStatus: vi.fn<LocalesApi['setStatus']>(async () => ({
      ok: false as const,
      failure: { kind: 'unavailable' as const },
    })),
    photo: vi.fn<LocalesApi['photo']>(async () => ({
      ok: true as const,
      value: new Blob(['foto'], { type: 'image/png' }),
    })),
  };
}

function objectUrls() {
  let count = 0;

  return {
    create: vi.fn((_blob: Blob) => `blob:foto-${++count}`),
    revoke: vi.fn(),
  };
}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round += 1) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
  }
  await fixture.whenStable();
}

async function render(api: ReturnType<typeof localesApi>): Promise<{
  fixture: ComponentFixture<LocalesPage>;
  urls: ReturnType<typeof objectUrls>;
}> {
  const urls = objectUrls();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: LOCALES_API, useValue: api },
      { provide: OBJECT_URLS, useValue: urls },
    ],
  });

  const fixture = TestBed.createComponent(LocalesPage);
  await flush(fixture);

  return { fixture, urls };
}

function host(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function button(fixture: ComponentFixture<unknown>, label: string): HTMLButtonElement {
  const found = [...host(fixture).querySelectorAll('button')].find((candidate) =>
    candidate.textContent?.includes(label),
  );

  if (!found) {
    throw new Error(`No se encontró el botón «${label}»`);
  }

  return found;
}

/** El filtro es un select de Spartan: se abre y se elige la opción como el usuario. */
function filterTrigger(fixture: ComponentFixture<unknown>): HTMLButtonElement {
  const trigger = host(fixture).querySelector<HTMLButtonElement>('#state-filter');

  if (!trigger) {
    throw new Error('No se encontró el filtro de estado');
  }

  return trigger;
}

function filterOptions(): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>('[role="option"]')];
}

function option(label: string): HTMLElement {
  const found = filterOptions().find((item) => item.textContent?.includes(label));

  if (!found) {
    throw new Error(`No se encontró la opción «${label}»`);
  }

  return found;
}

function optionTexts(): string[] {
  return filterOptions().map((item) => (item.textContent ?? '').replace(/\s+/g, ' ').trim());
}

async function openFilter(fixture: ComponentFixture<unknown>): Promise<HTMLButtonElement> {
  const trigger = filterTrigger(fixture);

  trigger.click();
  await flush(fixture);

  return trigger;
}

async function chooseFilter(fixture: ComponentFixture<unknown>, label: string): Promise<void> {
  await openFilter(fixture);

  option(label).click();
  await flush(fixture);
}

function alertText(fixture: ComponentFixture<unknown>): string {
  return [...host(fixture).querySelectorAll('[role="alert"]')]
    .map((element) => element.textContent ?? '')
    .join(' ');
}

describe('LocalesPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('muestra los locales activos e inactivos con contacto y atención semanal', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [
        local(),
        local({
          id: 'local-2',
          name: 'MouseDent San Borja',
          address: 'Av. San Borja Sur 248',
          phone: '(01) 555 0130',
          isActive: false,
        }),
      ],
    });

    const { fixture } = await render(api);
    const text = host(fixture).textContent ?? '';

    expect(text).toContain('MouseDent Miraflores');
    expect(text).toContain('Av. José Pardo 620, Miraflores');
    expect(text).toContain('(01) 555 0120');
    expect(text).toContain('MouseDent San Borja');
    expect(text).toContain('Activo');
    expect(text).toContain('Inactivo');
    expect(text).toContain('6 días de atención');
    expect(text).toContain('2 locales registrados');
    expect(host(fixture).querySelector('a[href="/locales/nuevo"]')).not.toBeNull();
  });

  it('distingue un local sin foto de uno con la foto cargada de la API', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [
        local(),
        local({
          id: 'local-2',
          name: 'MouseDent San Borja',
          photo: { contentType: 'image/png', version: 'foto-2.png' },
        }),
      ],
    });

    const { fixture } = await render(api);
    const image = host(fixture).querySelector<HTMLImageElement>('img');

    expect(api.photo).toHaveBeenCalledWith('local-2');
    expect(api.photo).not.toHaveBeenCalledWith('local-1');
    expect(image?.getAttribute('src')).toBe('blob:foto-1');
    expect(image?.getAttribute('alt')).toBe('Foto de MouseDent San Borja');
    expect(host(fixture).textContent).toContain('Sin foto');
  });

  it('muestra la lista vacía con la acción de registrar el primer local', async () => {
    const { fixture } = await render(localesApi());
    const firstLocalLink = [...host(fixture).querySelectorAll('a')].find((candidate) =>
      candidate.textContent?.includes('Registrar primer local'),
    );

    expect(host(fixture).textContent).toContain('Tu primer local empieza aquí');
    expect(host(fixture).textContent).toContain('Sin locales registrados');
    expect(firstLocalLink?.getAttribute('href')).toBe('/locales/nuevo');
  });

  it('cuenta un único local en singular', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({ ok: true, value: [local()] });

    const { fixture } = await render(api);
    const text = host(fixture).textContent ?? '';

    expect(text).toContain('1 local registrado');
    expect(text).not.toContain('locales registrados');
  });

  it('rotula el filtro como Estado, sin «Mostrar», y lo marca con un punto', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [local(), local({ id: 'local-2', name: 'MouseDent San Borja', isActive: false })],
    });

    const { fixture } = await render(api);
    const label = host(fixture).querySelector('label[for="state-filter"]');
    const trigger = filterTrigger(fixture);
    const dot = () => trigger.querySelector('span[aria-hidden="true"]')?.className ?? '';

    expect(label?.textContent?.trim()).toBe('Estado');
    expect(host(fixture).textContent).not.toContain('Mostrar');
    expect(trigger.getAttribute('role')).toBe('combobox');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(dot()).toContain('bg-primary');

    await openFilter(fixture);
    expect(filterTrigger(fixture).getAttribute('aria-expanded')).toBe('true');

    option('Inactivos').click();
    await flush(fixture);

    expect(dot()).toContain('bg-muted-foreground');
  });

  it('muestra el recuento por estado dentro del filtro', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [
        local(),
        local({ id: 'local-2', name: 'MouseDent San Borja', isActive: false }),
        local({ id: 'local-3', name: 'MouseDent Surco', isActive: false }),
      ],
    });

    const { fixture } = await render(api);

    await openFilter(fixture);

    expect(optionTexts()).toEqual(['Todos los estados (3)', 'Activos (1)', 'Inactivos (2)']);
  });

  it('mantiene disponible el estado sin locales y explica la lista filtrada vacía', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({ ok: true, value: [local()] });

    const { fixture } = await render(api);

    await openFilter(fixture);

    const inactive = option('Inactivos');
    expect(inactive.textContent).toContain('Inactivos (0)');
    expect(inactive.getAttribute('aria-disabled')).not.toBe('true');

    inactive.click();
    await flush(fixture);

    expect(host(fixture).textContent).toContain('No hay locales con este estado');
  });

  it('explica el fallo de la consulta y permite reintentarla', async () => {
    const api = localesApi();
    api.list
      .mockResolvedValueOnce({ ok: false, failure: { kind: 'unavailable' } })
      .mockResolvedValueOnce({ ok: true, value: [local()] });

    const { fixture } = await render(api);

    expect(host(fixture).textContent).toContain('No se pudieron cargar los locales');

    button(fixture, 'Reintentar').click();
    await flush(fixture);

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(host(fixture).textContent).toContain('MouseDent Miraflores');
    expect(host(fixture).textContent).not.toContain('No se pudieron cargar los locales');
  });

  it('desactiva un local tras confirmar y conserva sus datos en la lista', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({ ok: true, value: [local()] });
    api.setStatus.mockResolvedValue({ ok: true, value: local({ isActive: false }) });

    const { fixture } = await render(api);

    button(fixture, 'Desactivar').click();
    await flush(fixture);

    expect(host(fixture).querySelector('[role="alertdialog"]')?.textContent).toContain(
      'quedará inactivo',
    );

    button(fixture, 'Desactivar local').click();
    await flush(fixture);

    expect(api.setStatus).toHaveBeenCalledWith('local-1', false);
    const text = host(fixture).textContent ?? '';
    expect(text).toContain('Inactivo');
    expect(text).toContain('MouseDent Miraflores');
    expect(text).toContain('Av. José Pardo 620, Miraflores');
    expect(host(fixture).querySelector('[role="alertdialog"]')).toBeNull();
  });

  it('reactiva un local inactivo', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({ ok: true, value: [local({ isActive: false })] });
    api.setStatus.mockResolvedValue({ ok: true, value: local() });

    const { fixture } = await render(api);

    button(fixture, 'Reactivar').click();
    await flush(fixture);
    button(fixture, 'Reactivar local').click();
    await flush(fixture);

    expect(api.setStatus).toHaveBeenCalledWith('local-1', true);
    expect(host(fixture).textContent).toContain('Activo');
  });

  it('no da el cambio por bueno cuando falla y deja reintentar', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({ ok: true, value: [local()] });

    const { fixture } = await render(api);

    button(fixture, 'Desactivar').click();
    await flush(fixture);
    button(fixture, 'Desactivar local').click();
    await flush(fixture);

    expect(alertText(fixture)).toContain('No se pudo desactivar el local. Inténtalo de nuevo.');
    expect(host(fixture).textContent).toContain('Activo');
    expect(host(fixture).textContent).toContain('MouseDent Miraflores');
    expect(button(fixture, 'Desactivar')).not.toBeNull();
  });

  it('filtra por estado sin ocultar el estado del resto', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [local(), local({ id: 'local-2', name: 'MouseDent San Borja', isActive: false })],
    });

    const { fixture } = await render(api);

    expect(host(fixture).querySelector('#state-filter')?.textContent).toContain(
      'Todos los estados',
    );

    await chooseFilter(fixture, 'Inactivos');

    expect(host(fixture).textContent).toContain('MouseDent San Borja');
    expect(host(fixture).textContent).not.toContain('MouseDent Miraflores');
    expect(host(fixture).querySelector('#state-filter')?.textContent).toContain('Inactivos');

    await chooseFilter(fixture, 'Activos');

    expect(host(fixture).textContent).toContain('MouseDent Miraflores');
    expect(host(fixture).textContent).not.toContain('MouseDent San Borja');
  });

  it('muestra el aviso de guardado que dejó el formulario', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: LOCALES_API, useValue: localesApi() },
        { provide: OBJECT_URLS, useValue: objectUrls() },
      ],
    });
    TestBed.inject(LocalesNotice).set('Se registró «MouseDent Miraflores».');

    const fixture = TestBed.createComponent(LocalesPage);
    await flush(fixture);

    expect(host(fixture).querySelector('[role="status"]')?.textContent).toContain(
      'Se registró «MouseDent Miraflores».',
    );
  });

  it('libera las URL de las fotos al salir de la lista', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [local({ photo: { contentType: 'image/png', version: 'foto-2.png' } })],
    });

    const { fixture, urls } = await render(api);

    fixture.destroy();

    expect(urls.revoke).toHaveBeenCalledWith('blob:foto-1');
  });
});
