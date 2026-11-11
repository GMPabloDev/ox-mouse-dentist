import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import type { CreateLocalForm, LocalResponse, UpdateLocalForm } from '@mousedent/api-client';
import { OBJECT_URLS } from '../../core/api/object-urls';
import { LOCALES_API, type LocalesApi } from '../../core/locales/locales-api';
import { LocalFormPage } from './local-form-page';
import { LocalesNotice } from './locales-notice';
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
    create: vi.fn<LocalesApi['create']>(async (form: CreateLocalForm) => ({
      ok: true as const,
      value: local({ name: form.name ?? '' }),
    })),
    update: vi.fn<LocalesApi['update']>(async (_id: string, form: UpdateLocalForm) => ({
      ok: true as const,
      value: local({ name: form.name ?? '' }),
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
  fixture.detectChanges();
}

async function render(
  api: ReturnType<typeof localesApi>,
  options: { readonly editId?: string } = {},
): Promise<{
  fixture: ComponentFixture<LocalFormPage>;
  urls: ReturnType<typeof objectUrls>;
  navigate: ReturnType<typeof vi.fn>;
}> {
  const urls = objectUrls();
  const navigate = vi.fn(async () => true);

  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: LOCALES_API, useValue: api },
      { provide: OBJECT_URLS, useValue: urls },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: {
            paramMap: convertToParamMap(options.editId ? { id: options.editId } : {}),
            params: {},
            data: {},
            pathFromRoot: [],
            url: [],
          },
          pathFromRoot: [],
          parent: null,
          firstChild: null,
          children: [],
        },
      },
    ],
  });
  vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockImplementation(navigate);

  const fixture = TestBed.createComponent(LocalFormPage);
  await flush(fixture);

  return { fixture, urls, navigate };
}

function host(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function element<T extends HTMLElement>(fixture: ComponentFixture<unknown>, selector: string): T {
  const found = host(fixture).querySelector<T>(selector);

  if (!found) {
    throw new Error(`No se encontró «${selector}»`);
  }

  return found;
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

function alerts(fixture: ComponentFixture<unknown>): string {
  return [...host(fixture).querySelectorAll('[role="alert"]')]
    .map((node) => node.textContent?.trim() ?? '')
    .join(' | ');
}

function type(fixture: ComponentFixture<unknown>, selector: string, value: string): void {
  const input = element<HTMLInputElement>(fixture, selector);
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function chooseFile(fixture: ComponentFixture<unknown>, file: File): void {
  const input = element<HTMLInputElement>(fixture, 'input[type="file"]');
  Object.defineProperty(input, 'files', { configurable: true, value: [file] });

  input.dispatchEvent(new Event('change'));
}

function submit(fixture: ComponentFixture<unknown>): void {
  element<HTMLFormElement>(fixture, 'form').dispatchEvent(new Event('submit'));
}

function summary(fixture: ComponentFixture<unknown>): string[] {
  return [...host(fixture).querySelectorAll('app-schedule-editor .summary-row')].map((row) =>
    (row.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

function fileOf(type: string, size: number, name = 'foto.png'): File {
  return new File([new Uint8Array(size)], name, { type });
}

describe('LocalFormPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('señala cada campo obligatorio y no pierde lo escrito', async () => {
    const api = localesApi();
    const { fixture } = await render(api);

    submit(fixture);
    await flush(fixture);

    expect(alerts(fixture)).toContain('El nombre es obligatorio.');
    expect(alerts(fixture)).toContain('La dirección es obligatoria.');
    expect(alerts(fixture)).toContain('El teléfono es obligatorio.');
    expect(alerts(fixture)).toContain('El horario debe tener al menos un día abierto.');
    expect(element<HTMLInputElement>(fixture, '#local-name').getAttribute('aria-invalid')).toBe(
      'true',
    );

    type(fixture, '#local-name', 'MouseDent San Borja');
    type(fixture, '#local-address', 'Av. San Borja Sur 248');
    submit(fixture);
    await flush(fixture);

    expect(element<HTMLInputElement>(fixture, '#local-name').value).toBe('MouseDent San Borja');
    expect(element<HTMLInputElement>(fixture, '#local-address').value).toBe(
      'Av. San Borja Sur 248',
    );
    expect(alerts(fixture)).toContain('El horario debe tener al menos un día abierto.');
    expect(alerts(fixture)).not.toContain('El nombre es obligatorio.');
    expect(api.create).not.toHaveBeenCalled();
  });

  it('aplica el tramo a los días elegidos sin guardar el local', async () => {
    const api = localesApi();
    const { fixture } = await render(api);

    button(fixture, 'Aplicar a 5 días').click();
    await flush(fixture);

    expect(summary(fixture).filter((row) => row.includes('09:00 a 18:00'))).toHaveLength(5);
    expect(host(fixture).textContent).toContain('5 días abiertos');
    expect(host(fixture).textContent).toContain('Cambios sin guardar');
    expect(api.create).not.toHaveBeenCalled();
    expect(api.update).not.toHaveBeenCalled();
  });

  it('registra el local con los siete días y la foto elegida', async () => {
    const api = localesApi();
    const { fixture, navigate } = await render(api);
    const photo = fileOf('image/png', 900, 'frente.png');

    type(fixture, '#local-name', '  MouseDent San Borja ');
    type(fixture, '#local-address', 'Av. San Borja Sur 248');
    type(fixture, '#local-phone', '(01) 555 0130');
    button(fixture, 'Aplicar a 5 días').click();
    chooseFile(fixture, photo);
    await flush(fixture);

    expect(
      element<HTMLImageElement>(fixture, 'img[alt="Vista previa de la foto del local"]'),
    ).toBeTruthy();
    expect(host(fixture).textContent).toContain('Pendiente de guardar');

    submit(fixture);
    await flush(fixture);

    const form = api.create.mock.calls[0]?.[0];
    const schedule = JSON.parse(form?.schedule ?? '') as LocalResponse['schedule'];

    expect(form?.name).toBe('MouseDent San Borja');
    expect(form?.address).toBe('Av. San Borja Sur 248');
    expect(form?.phone).toBe('(01) 555 0130');
    expect(form?.photo).toBe(photo);
    expect(schedule.map((day) => day.day)).toEqual([...WEEK_DAYS]);
    expect(schedule[0]).toEqual({
      day: 'Monday',
      isClosed: false,
      opensAt: '09:00',
      closesAt: '18:00',
    });
    expect(navigate).toHaveBeenCalledWith('/locales');
    expect(TestBed.inject(LocalesNotice).take()).toContain('Se registró');
  });

  it('cancela la edición sin guardar y conserva la foto guardada', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [local({ photo: { contentType: 'image/png', version: 'foto-1.png' } })],
    });
    const { fixture, navigate } = await render(api, { editId: 'local-1' });

    expect(element<HTMLInputElement>(fixture, '#local-name').value).toBe('MouseDent Miraflores');
    expect(
      element<HTMLImageElement>(fixture, 'img[alt="Vista previa de la foto del local"]'),
    ).toBeTruthy();
    expect(summary(fixture).filter((row) => row.includes('09:00 a 18:00'))).toHaveLength(6);

    type(fixture, '#local-phone', '(01) 555 0999');
    button(fixture, 'Cancelar').click();
    await flush(fixture);

    expect(host(fixture).querySelector('[role="alertdialog"]')?.textContent).toContain(
      '¿Descartar los cambios?',
    );

    button(fixture, 'Descartar cambios').click();
    await flush(fixture);

    expect(api.update).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/locales');
  });

  it('quita la foto guardada y envía la indicación de eliminación al guardar', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [local({ photo: { contentType: 'image/png', version: 'foto-1.png' } })],
    });
    const { fixture } = await render(api, { editId: 'local-1' });

    button(fixture, 'Quitar').click();
    await flush(fixture);

    expect(host(fixture).textContent).toContain('La foto se quitará al guardar');
    expect(host(fixture).querySelector('img[alt="Vista previa de la foto del local"]')).toBeNull();

    submit(fixture);
    await flush(fixture);

    expect(api.update.mock.calls[0]?.[1].photoAction).toBe('remove');
    expect(api.update.mock.calls[0]?.[1].photo).toBeUndefined();
  });

  it('deshace la eliminación de la foto antes de guardar', async () => {
    const api = localesApi();
    api.list.mockResolvedValue({
      ok: true,
      value: [local({ photo: { contentType: 'image/png', version: 'foto-1.png' } })],
    });
    const { fixture } = await render(api, { editId: 'local-1' });

    button(fixture, 'Quitar').click();
    await flush(fixture);
    button(fixture, 'Deshacer eliminación').click();
    await flush(fixture);

    expect(
      host(fixture).querySelector('img[alt="Vista previa de la foto del local"]'),
    ).toBeTruthy();

    submit(fixture);
    await flush(fixture);

    expect(api.update.mock.calls[0]?.[1].photoAction).toBe('keep');
  });

  it('recupera la foto quitada al deshacer un cambio sin foto guardada', async () => {
    const api = localesApi();
    const { fixture } = await render(api);
    const photo = fileOf('image/webp', 1024, 'nueva.webp');

    chooseFile(fixture, photo);
    await flush(fixture);
    button(fixture, 'Quitar').click();
    await flush(fixture);
    expect(host(fixture).querySelector('img[alt="Vista previa de la foto del local"]')).toBeNull();

    button(fixture, 'Deshacer eliminación').click();
    await flush(fixture);

    expect(
      host(fixture).querySelector('img[alt="Vista previa de la foto del local"]'),
    ).toBeTruthy();
    expect(host(fixture).textContent).toContain('nueva.webp');
  });

  it('rechaza un archivo no admitido antes de enviarlo', async () => {
    const api = localesApi();
    const { fixture } = await render(api);

    chooseFile(fixture, fileOf('image/gif', 1024, 'animado.gif'));
    await flush(fixture);

    expect(alerts(fixture)).toContain('Solo se aceptan imágenes JPG, PNG o WebP.');
    expect(host(fixture).querySelector('img[alt="Vista previa de la foto del local"]')).toBeNull();
    expect(api.create).not.toHaveBeenCalled();
  });

  it('muestra el error de guardado, conserva los datos y permite reintentar', async () => {
    const api = localesApi();
    api.create
      .mockResolvedValueOnce({ ok: false, failure: { kind: 'unavailable' } })
      .mockResolvedValueOnce({ ok: true, value: local({ name: 'MouseDent San Borja' }) });
    const { fixture, navigate } = await render(api);

    type(fixture, '#local-name', 'MouseDent San Borja');
    type(fixture, '#local-address', 'Av. San Borja Sur 248');
    type(fixture, '#local-phone', '555 0130');
    button(fixture, 'Aplicar a 5 días').click();
    submit(fixture);
    await flush(fixture);

    expect(alerts(fixture)).toContain('No se pudo guardar el local');
    expect(element<HTMLInputElement>(fixture, '#local-name').value).toBe('MouseDent San Borja');
    expect(navigate).not.toHaveBeenCalled();

    submit(fixture);
    await flush(fixture);

    expect(api.create).toHaveBeenCalledTimes(2);
    expect(navigate).toHaveBeenCalledWith('/locales');
  });

  it('muestra el error de foto del servidor y lo retira al corregirla', async () => {
    const api = localesApi();
    api.create.mockResolvedValue({
      ok: false,
      failure: { kind: 'validation', errors: { Photo: ['Imagen inválida'] } },
    });
    const { fixture } = await render(api);
    type(fixture, '#local-name', 'Local');
    type(fixture, '#local-address', 'Av. 1');
    type(fixture, '#local-phone', '555');
    button(fixture, 'Aplicar a 5 días').click();
    chooseFile(fixture, fileOf('image/png', 900, 'falsa.png'));
    await flush(fixture);
    submit(fixture);
    await flush(fixture);
    expect(host(fixture).textContent).toContain('Imagen inválida');
    chooseFile(fixture, fileOf('image/png', 900, 'buena.png'));
    await flush(fixture);
    expect(host(fixture).textContent).not.toContain('Imagen inválida');
  });

  it('traduce el nombre repetido al campo del nombre', async () => {
    const api = localesApi();
    api.create.mockResolvedValue({
      ok: false,
      failure: { kind: 'duplicate', detail: 'Ya existe un local con ese nombre.' },
    });
    const { fixture } = await render(api);

    type(fixture, '#local-name', 'MouseDent Miraflores');
    type(fixture, '#local-address', 'Av. 1');
    type(fixture, '#local-phone', '555');
    button(fixture, 'Aplicar a 5 días').click();
    submit(fixture);
    await flush(fixture);

    expect(element(fixture, '#local-name-error').textContent).toContain(
      'Ya existe un local con ese nombre.',
    );
    expect(element<HTMLInputElement>(fixture, '#local-name').value).toBe('MouseDent Miraflores');
  });

  it('explica el fallo de carga de un local y permite reintentar', async () => {
    const api = localesApi();
    api.list
      .mockResolvedValueOnce({ ok: false, failure: { kind: 'unavailable' } })
      .mockResolvedValueOnce({ ok: true, value: [local()] });

    const { fixture } = await render(api, { editId: 'local-1' });

    expect(host(fixture).textContent).toContain('No se pudo cargar el local');

    button(fixture, 'Reintentar').click();
    await flush(fixture);

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(element<HTMLInputElement>(fixture, '#local-name').value).toBe('MouseDent Miraflores');
  });
});
