import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type {
  EspecialidadResponse,
  LocalResponse,
  ProfesionalResponse,
} from '@mousedent/api-client';
import type { CatalogoApi } from '../../core/catalogo/catalogo-api';
import { CATALOGO_API } from '../../core/catalogo/catalogo-api';
import { LOCALES_API, type LocalesApi } from '../../core/locales/locales-api';
import {
  PROFESIONALES_API,
  type ProfesionalResult,
  type ProfesionalesApi,
} from '../../core/profesionales/profesionales-api';
import { ProfesionalPage } from './profesional-page';

function profesional(overrides: Partial<ProfesionalResponse> = {}): ProfesionalResponse {
  return {
    id: 'ficha-valeria',
    staffAccountId: 'cuenta-valeria',
    displayName: 'Valeria Torres',
    email: 'valeria.torres@example.test',
    phone: '987 123 456',
    colegiatura: 'COP 12345',
    isActive: true,
    accountStatus: 'Activa',
    invitationFailed: false,
    especialidades: [
      { id: 'general', name: 'Odontología general', isActive: true },
      { id: 'orto', name: 'Ortodoncia', isActive: true },
    ],
    locales: [
      { id: 'miraflores', name: 'Miraflores', isActive: true },
      { id: 'surco', name: 'Surco', isActive: false },
    ],
    ...overrides,
  };
}

const VALERIA = profesional();

const CAMILA = profesional({
  id: 'ficha-camila',
  displayName: 'Camila Mendoza',
  email: 'camila.mendoza@example.test',
  phone: null,
  colegiatura: null,
  accountStatus: 'Pendiente',
  invitationFailed: true,
  especialidades: [
    { id: 'orto', name: 'Ortodoncia', isActive: true },
    { id: 'perio', name: 'Periodoncia', isActive: false },
  ],
  locales: [{ id: 'miraflores', name: 'Miraflores', isActive: true }],
});

const LUCIA = profesional({
  id: 'ficha-lucia',
  displayName: 'Lucía Rojas',
  email: 'lucia.rojas@example.test',
  isActive: false,
  especialidades: [{ id: 'perio', name: 'Periodoncia', isActive: false }],
  locales: [{ id: 'surco', name: 'Surco', isActive: false }],
});

const DIEGO = profesional({
  id: 'ficha-diego',
  displayName: 'Diego Salazar',
  email: 'diego.salazar@example.test',
  isActive: false,
  especialidades: [{ id: 'general', name: 'Odontología general', isActive: true }],
  locales: [{ id: 'miraflores', name: 'Miraflores', isActive: true }],
});

const ESPECIALIDADES: readonly EspecialidadResponse[] = [
  { id: 'general', name: 'Odontología general', description: null, isActive: true },
  { id: 'orto', name: 'Ortodoncia', description: null, isActive: true },
  { id: 'perio', name: 'Periodoncia', description: null, isActive: false },
];

function local(id: string, name: string, isActive: boolean): LocalResponse {
  return {
    id,
    name,
    address: 'Av. Siempre Viva 742',
    phone: '(01) 555 0120',
    isActive,
    schedule: [],
    photo: null,
  };
}

const LOCALES: readonly LocalResponse[] = [
  local('miraflores', 'Miraflores', true),
  local('surco', 'Surco', false),
];

function profesionalesApi(overrides: Partial<ProfesionalesApi> = {}): ProfesionalesApi {
  return {
    list: vi.fn(async () => ({ ok: true as const, value: [VALERIA, CAMILA] })),
    create: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unavailable' as const } })),
    update: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unavailable' as const } })),
    setStatus: vi.fn(async () => ({ ok: true as const, value: VALERIA })),
    resendInvitation: vi.fn(async () => ({ ok: true as const, value: CAMILA })),
    ...overrides,
  };
}

function catalogoApi(overrides: Partial<CatalogoApi> = {}): CatalogoApi {
  return {
    especialidades: vi.fn(async () => ({ ok: true as const, value: ESPECIALIDADES })),
    servicios: vi.fn(),
    createEspecialidad: vi.fn(),
    updateEspecialidad: vi.fn(),
    createServicio: vi.fn(),
    updateServicio: vi.fn(),
    setEspecialidadStatus: vi.fn(),
    setServicioStatus: vi.fn(),
    ...overrides,
  } as unknown as CatalogoApi;
}

function localesApi(overrides: Partial<LocalesApi> = {}): LocalesApi {
  return {
    list: vi.fn(async () => ({ ok: true as const, value: LOCALES })),
    create: vi.fn(),
    update: vi.fn(),
    setStatus: vi.fn(),
    photo: vi.fn(),
    ...overrides,
  } as unknown as LocalesApi;
}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round += 1) {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
  }
  await fixture.whenStable();
}

async function render(
  api: ProfesionalesApi = profesionalesApi(),
  catalogo: CatalogoApi = catalogoApi(),
  locales: LocalesApi = localesApi(),
): Promise<ComponentFixture<ProfesionalPage>> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: PROFESIONALES_API, useValue: api },
      { provide: CATALOGO_API, useValue: catalogo },
      { provide: LOCALES_API, useValue: locales },
    ],
  });

  const fixture = TestBed.createComponent(ProfesionalPage);
  await flush(fixture);

  return fixture;
}

function host(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function text(fixture: ComponentFixture<unknown>): string {
  return host(fixture).textContent ?? '';
}

function button(
  fixture: ComponentFixture<unknown>,
  label: string,
  root?: ParentNode,
): HTMLButtonElement {
  const found = [...(root ?? host(fixture)).querySelectorAll<HTMLButtonElement>('button')].find(
    (candidate) => candidate.textContent?.includes(label),
  );

  if (!found) {
    throw new Error(`No se encontró el botón «${label}»`);
  }

  return found;
}

function hasButton(fixture: ComponentFixture<unknown>, label: string): boolean {
  return [...host(fixture).querySelectorAll('button')].some((candidate) =>
    candidate.textContent?.includes(label),
  );
}

function personButton(fixture: ComponentFixture<unknown>, name: string): HTMLButtonElement {
  return button(fixture, name);
}

function dialog(fixture: ComponentFixture<unknown>): HTMLElement {
  const found = host(fixture).querySelector<HTMLElement>('[role="alertdialog"]');

  if (!found) {
    throw new Error('No se abrió el diálogo de confirmación');
  }

  return found;
}

function setInput(fixture: ComponentFixture<unknown>, selector: string, value: string): void {
  const input = host(fixture).querySelector<HTMLInputElement>(selector);

  if (!input) {
    throw new Error(`No se encontró el campo ${selector}`);
  }

  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function setSelect(fixture: ComponentFixture<unknown>, selector: string, value: string): void {
  const select = host(fixture).querySelector<HTMLSelectElement>(selector);

  if (!select) {
    throw new Error(`No se encontró el selector ${selector}`);
  }

  select.value = value;
  select.dispatchEvent(new Event('change'));
}

function setChecked(fixture: ComponentFixture<unknown>, selector: string, checked: boolean): void {
  const input = host(fixture).querySelector<HTMLInputElement>(selector);

  if (!input) {
    throw new Error(`No se encontró la casilla ${selector}`);
  }

  input.checked = checked;
  input.dispatchEvent(new Event('change'));
}

function openCreate(fixture: ComponentFixture<unknown>): void {
  button(fixture, 'Nuevo profesional').click();
}

describe('ProfesionalPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('muestra la carga y después la tabla con asignaciones y estados', async () => {
    let resolveList!: (value: ProfesionalResult<readonly ProfesionalResponse[]>) => void;
    const api = profesionalesApi({
      list: vi.fn(
        () =>
          new Promise<ProfesionalResult<readonly ProfesionalResponse[]>>((resolve) => {
            resolveList = resolve;
          }),
      ),
    });
    const fixture = await render(api);

    expect(host(fixture).querySelector('[role="status"][aria-busy="true"]')).not.toBeNull();
    expect(text(fixture)).toContain('Cargando profesionales');

    resolveList({ ok: true, value: [VALERIA, CAMILA] });
    await flush(fixture);

    expect(text(fixture)).toContain('Valeria Torres');
    expect(text(fixture)).toContain('valeria.torres@example.test');
    expect(text(fixture)).toContain('Colegiatura COP 12345');
    expect(text(fixture)).toContain('Sin colegiatura registrada');
    expect(text(fixture)).toContain('Odontología general');
    expect(text(fixture)).toContain('Periodoncia');
    expect(text(fixture)).toContain('Inactiva');
    expect(text(fixture)).toContain('Ficha activa');
    expect(text(fixture)).toContain('Cuenta activa');
    expect(text(fixture)).toContain('Cuenta pendiente');
    expect(text(fixture)).toContain('Correo no enviado');
    expect(text(fixture)).toContain('1 correo sin enviar');
    expect(host(fixture).querySelector('table')).not.toBeNull();
  });

  it('busca por nombre o correo y filtra por especialidad, local, ficha y cuenta', async () => {
    const api = profesionalesApi({
      list: vi.fn(async () => ({ ok: true as const, value: [VALERIA, CAMILA, LUCIA] })),
    });
    const fixture = await render(api);

    expect(text(fixture)).toContain('Periodoncia · Inactiva');
    expect(text(fixture)).toContain('Surco · Inactivo');

    setInput(fixture, 'input[type="search"]', 'LUCÍA');
    await flush(fixture);

    expect(text(fixture)).toContain('Lucía Rojas');
    expect(text(fixture)).not.toContain('Valeria Torres');
    expect(text(fixture)).toContain('1 de 3 profesionales');

    setInput(fixture, 'input[type="search"]', 'camila.mendoza@');
    await flush(fixture);

    expect(text(fixture)).toContain('Camila Mendoza');
    expect(text(fixture)).not.toContain('Lucía Rojas');

    setInput(fixture, 'input[type="search"]', 'valeria');
    await flush(fixture);

    expect(text(fixture)).toContain('Valeria Torres');
    expect(text(fixture)).not.toContain('Camila Mendoza');

    setInput(fixture, 'input[type="search"]', '');
    await flush(fixture);

    setSelect(fixture, '#profesional-specialty-filter', 'perio');
    await flush(fixture);

    expect(text(fixture)).toContain('Camila Mendoza');
    expect(text(fixture)).not.toContain('Valeria Torres');

    setSelect(fixture, '#profesional-specialty-filter', '');
    setSelect(fixture, '#profesional-local-filter', 'surco');
    await flush(fixture);

    expect(text(fixture)).toContain('Valeria Torres');
    expect(text(fixture)).not.toContain('Camila Mendoza');

    setSelect(fixture, '#profesional-local-filter', '');
    setSelect(fixture, '#profesional-account-filter', 'Pendiente');
    await flush(fixture);

    expect(text(fixture)).toContain('Camila Mendoza');
    expect(text(fixture)).not.toContain('Valeria Torres');

    setSelect(fixture, '#profesional-account-filter', 'all');
    setSelect(fixture, '#profesional-ficha-filter', 'inactive');
    await flush(fixture);

    expect(text(fixture)).toContain('Lucía Rojas');
    expect(text(fixture)).not.toContain('Valeria Torres');

    button(fixture, 'Limpiar filtros').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Valeria Torres');
    expect(text(fixture)).toContain('3 profesionales');
  });

  it('distingue sin profesionales de sin resultados y reintenta tras un error', async () => {
    const empty = profesionalesApi({
      list: vi.fn(async () => ({ ok: true as const, value: [] })),
    });
    const fixture = await render(empty);

    expect(text(fixture)).toContain('Aún no hay profesionales');
    expect(text(fixture)).toContain(
      'Registra al primer profesional para asignarle especialidades y locales de atención.',
    );
    expect(hasButton(fixture, 'Registrar profesional')).toBe(true);
    expect(host(fixture).querySelector('table')).toBeNull();
    expect(text(fixture)).not.toContain('No encontramos profesionales');

    TestBed.resetTestingModule();
    const failing = profesionalesApi({
      list: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unavailable' as const } })),
    });
    const errorFixture = await render(failing);

    expect(text(errorFixture)).toContain('No pudimos cargar los profesionales');
    expect(text(errorFixture)).not.toContain('Aún no hay profesionales');

    vi.mocked(failing.list).mockResolvedValueOnce({ ok: true, value: [VALERIA] });
    button(errorFixture, 'Reintentar').click();
    await flush(errorFixture);

    expect(text(errorFixture)).toContain('Valeria Torres');

    setInput(errorFixture, 'input[type="search"]', 'persona inexistente');
    await flush(errorFixture);

    expect(text(errorFixture)).toContain('No encontramos profesionales');
    expect(text(errorFixture)).not.toContain('Aún no hay profesionales');

    button(errorFixture, 'Limpiar filtros').click();
    await flush(errorFixture);

    expect(text(errorFixture)).toContain('Valeria Torres');
  });

  it('crea la ficha con su cuenta pendiente y la invitación', async () => {
    const creada = profesional({
      id: 'ficha-nueva',
      displayName: 'Rosa Castillo',
      email: 'rosa.castillo@example.test',
      phone: null,
      colegiatura: 'COP 77420',
      accountStatus: 'Pendiente',
      especialidades: [{ id: 'general', name: 'Odontología general', isActive: true }],
      locales: [{ id: 'miraflores', name: 'Miraflores', isActive: true }],
    });
    const api = profesionalesApi({
      create: vi.fn(async () => ({ ok: true as const, value: creada })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    expect(text(fixture)).toContain('Nuevo profesional');
    expect(text(fixture)).toContain('Alta de profesional');

    setInput(fixture, '#profesional-name', 'Rosa Castillo');
    setInput(fixture, '#profesional-email', 'rosa.castillo@example.test');
    setInput(fixture, '#profesional-colegiatura', 'COP 77420');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalledWith({
      displayName: 'Rosa Castillo',
      email: 'rosa.castillo@example.test',
      colegiatura: 'COP 77420',
      especialidadIds: ['general'],
      localIds: ['miraflores'],
    });
    expect(text(fixture)).toContain('Ficha creada. La cuenta queda pendiente');
    expect(text(fixture)).toContain('Ficha del profesional');
    expect(text(fixture)).toContain('Invitación pendiente');
    expect(hasButton(fixture, 'Reenviar invitación')).toBe(true);
    expect(text(fixture)).toContain('Cuenta pendiente');
  });

  it('vincula una cuenta Profesional existente y conserva su estado', async () => {
    const vinculada = profesional({
      id: 'ficha-vinculada',
      displayName: 'Jorge Salas',
      email: 'jorge.salas@example.test',
      phone: null,
      colegiatura: null,
      accountStatus: 'Activa',
    });
    const api = profesionalesApi({
      create: vi.fn(async () => ({ ok: true as const, value: vinculada })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    setInput(fixture, '#profesional-name', 'Nombre escrito en el alta');
    setInput(fixture, '#profesional-email', 'jorge.salas@example.test');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalledTimes(1);
    expect(text(fixture)).toContain('Cuenta vinculada. Jorge Salas ya puede acceder');
    expect(text(fixture)).toContain('Cuenta activa');
    expect(hasButton(fixture, 'Reenviar invitación')).toBe(false);
    expect(text(fixture)).not.toContain('Invitación pendiente');
  });

  it('avisa posibles homónimos al crear sin bloquear el guardado', async () => {
    const creada = profesional({
      id: 'ficha-nueva',
      displayName: 'Valeria Torres',
      email: 'valeria.nueva@example.test',
      colegiatura: null,
      accountStatus: 'Pendiente',
    });
    const api = profesionalesApi({
      create: vi.fn(async () => ({ ok: true as const, value: creada })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    setInput(fixture, '#profesional-name', 'Valeria Torres');
    await flush(fixture);

    expect(text(fixture)).toContain('Posible homónimo');
    expect(text(fixture)).toContain(
      'Valeria Torres ya tiene una ficha con un nombre igual o parecido. El nombre no es único, así que puedes continuar.',
    );

    setInput(fixture, '#profesional-email', 'valeria.nueva@example.test');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalledTimes(1);
    expect(text(fixture)).toContain('Ficha creada. La cuenta queda pendiente');
  });

  it('mantiene el formulario y muestra la colegiatura duplicada', async () => {
    const api = profesionalesApi({
      create: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'duplicate-colegiatura' as const,
          detail: 'Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.',
        },
      })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    setInput(fixture, '#profesional-name', 'Camila Mendoza');
    setInput(fixture, '#profesional-email', 'camila.mendoza@example.test');
    setInput(fixture, '#profesional-colegiatura', 'cop 12345');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalledWith({
      displayName: 'Camila Mendoza',
      email: 'camila.mendoza@example.test',
      colegiatura: 'cop 12345',
      especialidadIds: ['general'],
      localIds: ['miraflores'],
    });
    expect(text(fixture)).toContain(
      'Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.',
    );
    expect(
      host(fixture).querySelector('#profesional-colegiatura')?.getAttribute('aria-invalid'),
    ).toBe('true');
    expect(host(fixture).querySelector<HTMLInputElement>('#profesional-name')?.value).toBe(
      'Camila Mendoza',
    );
    expect(host(fixture).querySelector<HTMLInputElement>('#profesional-email')?.value).toBe(
      'camila.mendoza@example.test',
    );
    expect(text(fixture)).not.toContain('Ficha creada.');
  });

  it('muestra el correo ocupado en su campo sin crear nada', async () => {
    const api = profesionalesApi({
      create: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'email-taken' as const,
          detail:
            'Ese correo pertenece a una cuenta que ya tiene una ficha de profesional. Edita la ficha existente.',
        },
      })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    setInput(fixture, '#profesional-name', 'Valeria Torres');
    setInput(fixture, '#profesional-email', 'valeria.torres@example.test');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(text(fixture)).toContain(
      'Ese correo pertenece a una cuenta que ya tiene una ficha de profesional.',
    );
    expect(host(fixture).querySelector('#profesional-email')?.getAttribute('aria-invalid')).toBe(
      'true',
    );
    expect(host(fixture).querySelector<HTMLInputElement>('#profesional-email')?.value).toBe(
      'valeria.torres@example.test',
    );
    expect(text(fixture)).not.toContain('Ficha creada.');
  });

  it('informa el rechazo de la regla de activación sin cambiar la ficha', async () => {
    const api = profesionalesApi({
      list: vi.fn(async () => ({ ok: true as const, value: [DIEGO] })),
      setStatus: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'activation-incomplete' as const,
          detail:
            'La ficha necesita al menos una especialidad activa y un local de atención activo. Asigna una especialidad y un local activos antes de reactivarla.',
        },
      })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Diego Salazar').click();
    await flush(fixture);
    button(fixture, 'Reactivar ficha').click();
    await flush(fixture);
    button(fixture, 'Reactivar ficha', dialog(fixture)).click();
    await flush(fixture);

    expect(api.setStatus).toHaveBeenCalledWith(DIEGO.id, true);
    expect(text(fixture)).toContain('La operación no se completó');
    expect(text(fixture)).toContain(
      'Asigna una especialidad y un local activos antes de reactivarla.',
    );
    expect(text(fixture)).not.toContain(
      'Ficha reactivada. Recuperó sus especialidades y sus locales guardados.',
    );
    expect(text(fixture)).toContain('Ficha inactiva');
  });

  it('conserva lo escrito cuando la API rechaza una asignación que ya no está activa', async () => {
    const api = profesionalesApi({
      create: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'validation' as const,
          errors: {
            EspecialidadIds: ['Solo puedes asignar especialidades activas existentes.'],
          },
        },
      })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    setInput(fixture, '#profesional-name', 'Rosa Castillo');
    setInput(fixture, '#profesional-email', 'rosa.castillo@example.test');
    setInput(fixture, '#profesional-colegiatura', 'COP 77420');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalledTimes(1);
    expect(text(fixture)).toContain('Solo puedes asignar especialidades activas existentes.');
    expect(host(fixture).querySelector<HTMLInputElement>('#profesional-name')?.value).toBe(
      'Rosa Castillo',
    );
    expect(host(fixture).querySelector<HTMLInputElement>('#profesional-colegiatura')?.value).toBe(
      'COP 77420',
    );
    expect(
      host(fixture).querySelector<HTMLInputElement>('input[name="especialidad"][value="general"]')
        ?.checked,
    ).toBe(true);
    expect(text(fixture)).toContain('Crear y enviar invitación');
    expect(text(fixture)).not.toContain('Ficha creada.');
  });

  it('avisa del fallo de invitación con la ficha guardada y permite reenviarla', async () => {
    const guardada = profesional({
      id: 'ficha-nueva',
      displayName: 'Rosa Castillo',
      email: 'rosa.castillo@example.test',
      phone: null,
      colegiatura: null,
      accountStatus: 'Pendiente',
      invitationFailed: true,
    });
    const corregida = { ...guardada, invitationFailed: false };
    const list = vi
      .fn<ProfesionalesApi['list']>()
      .mockResolvedValueOnce({ ok: true, value: [VALERIA] })
      .mockResolvedValue({ ok: true, value: [VALERIA, guardada] });
    const api = profesionalesApi({
      list,
      create: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'invitation-failed' as const,
          profesionalId: guardada.id,
          detail: 'La ficha quedó guardada, pero el correo no se pudo enviar.',
        },
      })),
      resendInvitation: vi.fn(async () => ({ ok: true as const, value: corregida })),
    });
    const fixture = await render(api);

    openCreate(fixture);
    await flush(fixture);

    setInput(fixture, '#profesional-name', 'Rosa Castillo');
    setInput(fixture, '#profesional-email', 'rosa.castillo@example.test');
    setChecked(fixture, 'input[name="especialidad"][value="general"]', true);
    setChecked(fixture, 'input[name="local"][value="miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(text(fixture)).toContain('No pudimos enviar la invitación');
    expect(text(fixture)).toContain('La ficha quedó guardada, pero el correo no se pudo enviar.');
    expect(text(fixture)).toContain('1 correo sin enviar');
    expect(text(fixture)).not.toContain('Ficha creada.');
    expect(hasButton(fixture, 'Reintentar envío')).toBe(true);

    button(fixture, 'Reintentar envío').click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Enviar una invitación nueva?');
    button(fixture, 'Enviar invitación', dialog(fixture)).click();
    await flush(fixture);

    expect(api.resendInvitation).toHaveBeenCalledWith(guardada.id);
    expect(text(fixture)).toContain('Invitación reenviada a rosa.castillo@example.test.');
    expect(text(fixture)).not.toContain('No pudimos enviar la invitación');
    expect(text(fixture)).toContain('Invitación pendiente');
    expect(text(fixture)).not.toContain('1 correo sin enviar');
  });

  it('desactiva la ficha solo después de confirmar y explica qué se conserva', async () => {
    const desactivada = { ...VALERIA, isActive: false };
    const api = profesionalesApi({
      setStatus: vi.fn(async () => ({ ok: true as const, value: desactivada })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Valeria Torres').click();
    await flush(fixture);

    button(fixture, 'Desactivar ficha').click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Desactivar la ficha de Valeria Torres?');
    expect(text(fixture)).toContain(
      'Conserva sus especialidades, sus locales de atención y su cuenta.',
    );
    expect(api.setStatus).not.toHaveBeenCalled();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await flush(fixture);

    expect(host(fixture).querySelector('[role="alertdialog"]')).toBeNull();
    expect(api.setStatus).not.toHaveBeenCalled();

    button(fixture, 'Desactivar ficha').click();
    await flush(fixture);
    button(fixture, 'Desactivar ficha', dialog(fixture)).click();
    await flush(fixture);

    expect(api.setStatus).toHaveBeenCalledWith(VALERIA.id, false);
    expect(text(fixture)).toContain(
      'Ficha desactivada. Se conservaron sus especialidades, sus locales y su cuenta.',
    );
    expect(text(fixture)).toContain('Ficha inactiva');
  });

  it('reemplaza la reactivación por los requisitos que faltan', async () => {
    const api = profesionalesApi({
      list: vi.fn(async () => ({ ok: true as const, value: [LUCIA] })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Lucía Rojas').click();
    await flush(fixture);

    expect(hasButton(fixture, 'Reactivar ficha')).toBe(false);
    expect(text(fixture)).toContain('No se puede reactivar todavía');
    expect(text(fixture)).toContain('Una especialidad activa asignada');
    expect(text(fixture)).toContain('Un local activo asignado');

    button(fixture, 'Editar asignaciones').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Editar ficha de Lucía Rojas');
    expect(text(fixture)).toContain('Puedes guardar sin especialidades activas');
  });

  it('reactiva una ficha que recuperó sus requisitos', async () => {
    const reactivada = { ...DIEGO, isActive: true };
    const api = profesionalesApi({
      list: vi.fn(async () => ({ ok: true as const, value: [DIEGO] })),
      setStatus: vi.fn(async () => ({ ok: true as const, value: reactivada })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Diego Salazar').click();
    await flush(fixture);

    expect(hasButton(fixture, 'Reactivar ficha')).toBe(true);

    button(fixture, 'Reactivar ficha').click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Reactivar la ficha de Diego Salazar?');
    button(fixture, 'Reactivar ficha', dialog(fixture)).click();
    await flush(fixture);

    expect(api.setStatus).toHaveBeenCalledWith(DIEGO.id, true);
    expect(text(fixture)).toContain(
      'Ficha reactivada. Recuperó sus especialidades y sus locales guardados.',
    );
    expect(text(fixture)).toContain('Ficha activa');
  });

  it('pide confirmación antes de descartar un formulario con cambios', async () => {
    const fixture = await render();

    openCreate(fixture);
    await flush(fixture);
    setInput(fixture, '#profesional-name', 'Cambio sin guardar');

    host(fixture)
      .querySelector<HTMLButtonElement>('button[aria-label="Cerrar formulario"]')!
      .click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Salir sin guardar?');
    button(fixture, 'Descartar cambios', dialog(fixture)).click();
    await flush(fixture);

    expect(host(fixture).querySelector('[aria-label="Cerrar formulario"]')).toBeNull();
    expect(host(fixture).querySelector('table')).not.toBeNull();
  });

  it('no muestra la lista ni las acciones cuando la sesión ya no es válida', async () => {
    const api = profesionalesApi({
      list: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unauthorized' as const } })),
    });
    const fixture = await render(api);

    expect(text(fixture)).toContain('Tu sesión ya no está disponible');
    expect(host(fixture).querySelector('a[href="/acceso"]')).not.toBeNull();
    expect(host(fixture).querySelector('table')).toBeNull();
    expect(hasButton(fixture, 'Nuevo profesional')).toBe(false);
  });

  it('cierra la pantalla si el perfil pierde el permiso', async () => {
    const api = profesionalesApi({
      list: vi.fn(async () => ({ ok: false as const, failure: { kind: 'forbidden' as const } })),
    });
    const fixture = await render(api);

    expect(text(fixture)).toContain('Tu perfil ya no permite gestionar profesionales');
    expect(host(fixture).querySelector('a[href="/bienvenida"]')).not.toBeNull();
    expect(host(fixture).querySelector('table')).toBeNull();
  });
});
