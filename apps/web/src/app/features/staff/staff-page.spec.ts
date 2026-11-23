import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { LocalResponse, StaffAccountResponse } from '@mousedent/api-client';
import { SessionService, type SessionUser } from '../../core/auth/session';
import { LOCALES_API, type LocalesApi } from '../../core/locales/locales-api';
import { STAFF_API, type StaffApi, type StaffResult } from '../../core/staff/staff-api';
import { StaffPage } from './staff-page';

const ADMIN: SessionUser = {
  id: 'admin-1',
  email: 'elena.ramirez@example.test',
  displayName: 'Elena Ramírez',
  roles: ['Administrador'],
};

const ELENA: StaffAccountResponse = {
  id: 'admin-1',
  displayName: 'Elena Ramírez',
  email: 'elena.ramirez@example.test',
  phone: null,
  profile: 'Administrador',
  status: 'Activa',
  assignedLocalIds: [],
};

const MARTIN: StaffAccountResponse = {
  id: 'admin-2',
  displayName: 'Martín Paredes',
  email: 'martin.paredes@example.test',
  phone: null,
  profile: 'Administrador',
  status: 'Pendiente',
  assignedLocalIds: [],
};

const VALERIA: StaffAccountResponse = {
  id: 'recep-1',
  displayName: 'Valeria Torres',
  email: 'valeria.torres@example.test',
  phone: '987 123 456',
  profile: 'Recepcion',
  status: 'Activa',
  assignedLocalIds: ['local-miraflores'],
};

const CAMILA: StaffAccountResponse = {
  id: 'recep-2',
  displayName: 'Camila Mendoza',
  email: 'camila.mendoza@example.test',
  phone: null,
  profile: 'Recepcion',
  status: 'Pendiente',
  assignedLocalIds: ['local-miraflores'],
};

const LUCIA: StaffAccountResponse = {
  id: 'recep-3',
  displayName: 'Lucía Rojas',
  email: 'lucia.rojas@example.test',
  phone: null,
  profile: 'Recepcion',
  status: 'Activa',
  assignedLocalIds: ['local-barranco'],
};

const ROSA: StaffAccountResponse = {
  id: 'recep-4',
  displayName: 'Rosa Castillo',
  email: 'rosa.castillo@example.test',
  phone: null,
  profile: 'Recepcion',
  status: 'Inactiva',
  assignedLocalIds: ['local-barranco'],
};

const ANDREA: StaffAccountResponse = {
  id: 'prof-1',
  displayName: 'Andrea Quispe',
  email: 'andrea.quispe@example.test',
  phone: null,
  profile: 'Profesional',
  status: 'Activa',
  assignedLocalIds: [],
};

function local(overrides: Partial<LocalResponse>): LocalResponse {
  return {
    id: 'local-1',
    name: 'Miraflores',
    address: 'Av. José Pardo 620',
    phone: '(01) 555 0120',
    isActive: true,
    schedule: [],
    photo: null,
    ...overrides,
  };
}

const LOCALS: readonly LocalResponse[] = [
  local({ id: 'local-miraflores', name: 'Miraflores', isActive: true }),
  local({ id: 'local-barranco', name: 'Barranco', isActive: false }),
];

function staffApi(overrides: Partial<StaffApi> = {}): StaffApi {
  return {
    list: vi.fn(async () => ({ ok: true as const, value: [ELENA, VALERIA, CAMILA, LUCIA, ROSA] })),
    create: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unavailable' as const } })),
    update: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unavailable' as const } })),
    setStatus: vi.fn<StaffApi['setStatus']>(async () => ({
      ok: true,
      value: { ...ROSA, status: 'Activa' },
    })),
    resendInvitation: vi.fn(async () => ({ ok: true as const, value: CAMILA })),
    ...overrides,
  };
}

function localesApi(overrides: Partial<LocalesApi> = {}): LocalesApi {
  return {
    list: vi.fn(async () => ({ ok: true as const, value: LOCALS })),
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
  api: StaffApi = staffApi(),
  locals: LocalesApi = localesApi(),
): Promise<ComponentFixture<StaffPage>> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: STAFF_API, useValue: api },
      { provide: LOCALES_API, useValue: locals },
      { provide: SessionService, useValue: { user: () => ADMIN } },
    ],
  });

  const fixture = TestBed.createComponent(StaffPage);
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

function setChecked(fixture: ComponentFixture<unknown>, selector: string, checked: boolean): void {
  const input = host(fixture).querySelector<HTMLInputElement>(selector);

  if (!input) {
    throw new Error(`No se encontró la casilla ${selector}`);
  }

  input.checked = checked;
  input.dispatchEvent(new Event('change'));
}

describe('StaffPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('lista las cuentas, marca la sesión, busca y filtra por estado', async () => {
    const fixture = await render();

    expect(text(fixture)).toContain('Elena Ramírez');
    expect(text(fixture)).toContain('valeria.torres@example.test');
    expect(text(fixture)).toContain('Tu sesión');
    expect(text(fixture)).toContain('Todos los locales');
    expect(text(fixture)).toContain('Sin locales activos');

    setInput(fixture, 'input[type="search"]', 'valeria');
    await flush(fixture);

    expect(text(fixture)).toContain('Valeria Torres');
    expect(text(fixture)).not.toContain('Camila Mendoza');
    expect(text(fixture)).toContain('1 de 5 cuentas');

    setInput(fixture, 'input[type="search"]', '');
    const pending = host(fixture).querySelector<HTMLButtonElement>(
      'button[aria-pressed][aria-pressed="false"]',
    );
    expect(pending).not.toBeNull();
    button(fixture, 'Pendientes').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Camila Mendoza');
    expect(text(fixture)).not.toContain('Valeria Torres');

    button(fixture, 'Inactivas').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Rosa Castillo');
    expect(text(fixture)).not.toContain('Camila Mendoza');
  });

  it('encuentra por correo sin distinguir tildes ni mayúsculas', async () => {
    const fixture = await render();

    setInput(fixture, 'input[type="search"]', 'LUCÍA');
    await flush(fixture);

    expect(text(fixture)).toContain('Lucía Rojas');

    setInput(fixture, 'input[type="search"]', 'rosa.castillo');
    await flush(fixture);

    expect(text(fixture)).toContain('Rosa Castillo');
    expect(text(fixture)).not.toContain('Lucía Rojas');
  });

  it('muestra la carga, el error con reintento y la ausencia de resultados', async () => {
    let resolveList!: (value: StaffResult<readonly StaffAccountResponse[]>) => void;
    const loading = staffApi({
      list: vi.fn(
        () =>
          new Promise<StaffResult<readonly StaffAccountResponse[]>>((resolve) => {
            resolveList = resolve;
          }),
      ),
    });
    const fixture = await render(loading);

    expect(host(fixture).querySelector('[role="status"][aria-busy="true"]')).not.toBeNull();
    expect(text(fixture)).toContain('Cargando el equipo');

    resolveList({ ok: true, value: [ELENA, VALERIA] });
    await flush(fixture);

    expect(text(fixture)).toContain('Valeria Torres');

    const failing = staffApi({
      list: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unavailable' as const } })),
    });
    TestBed.resetTestingModule();
    const errorFixture = await render(failing);

    expect(text(errorFixture)).toContain('No pudimos cargar las cuentas');
    expect(hasButton(errorFixture, 'Reintentar')).toBe(true);

    vi.mocked(failing.list).mockResolvedValueOnce({ ok: true, value: [ELENA, VALERIA] });
    button(errorFixture, 'Reintentar').click();
    await flush(errorFixture);

    expect(text(errorFixture)).toContain('Valeria Torres');

    setInput(errorFixture, 'input[type="search"]', 'persona inexistente');
    await flush(errorFixture);

    expect(text(errorFixture)).toContain('No encontramos coincidencias');

    button(errorFixture, 'Limpiar búsqueda y filtros').click();
    await flush(errorFixture);

    expect(text(errorFixture)).toContain('Valeria Torres');
  });

  it('no ofrece el perfil Profesional al invitar a una persona', async () => {
    const fixture = await render();

    button(fixture, 'Invitar persona').click();
    await flush(fixture);

    const profiles = [
      ...host(fixture).querySelectorAll<HTMLInputElement>('input[name="profile"]'),
    ].map((input) => input.value);

    expect(profiles).toEqual(['Recepcion', 'Administrador']);
    expect(text(fixture)).toContain('Elige un solo perfil para esta persona.');
  });

  it('no deja cambiar el perfil de una cuenta Profesional y explica dónde se asigna', async () => {
    const corrected: StaffAccountResponse = {
      ...ANDREA,
      displayName: 'Andrea Quispe Ríos',
      phone: '987 654 321',
    };
    const api = staffApi({
      list: vi.fn(async () => ({ ok: true as const, value: [ELENA, ANDREA] })),
      update: vi.fn(async () => ({ ok: true as const, value: corrected })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Andrea Quispe').click();
    await flush(fixture);
    button(fixture, 'Editar').click();
    await flush(fixture);

    expect(host(fixture).querySelectorAll('input[name="profile"]').length).toBe(0);
    expect(text(fixture)).toContain('Perfil Profesional asignado en profesionales');
    expect(text(fixture)).toContain('se asigna en el alta de profesionales');

    setInput(fixture, '#staff-name', 'Andrea Quispe Ríos');
    setInput(fixture, '#staff-phone', '987 654 321');
    button(fixture, 'Guardar cambios').click();
    await flush(fixture);

    expect(api.update).toHaveBeenCalledWith('prof-1', {
      displayName: 'Andrea Quispe Ríos',
      email: 'andrea.quispe@example.test',
      phone: '987 654 321',
      profile: 'Profesional',
      assignedLocalIds: [],
    });
    expect(text(fixture)).toContain('Cambios guardados.');
  });

  it('valida el alta, exige un local activo y conserva lo escrito', async () => {
    const api = staffApi({
      create: vi.fn(async () => ({ ok: true as const, value: CAMILA })),
    });
    const fixture = await render(api);

    button(fixture, 'Invitar persona').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Invitar a una persona');

    setInput(fixture, '#staff-name', 'Camila Mendoza');
    setInput(fixture, '#staff-email', 'camila.mendoza@example.test');

    const inactive = host(fixture).querySelector<HTMLInputElement>(
      'input[name="location"][value="local-barranco"]',
    );
    expect(inactive?.disabled).toBe(true);

    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Selecciona al menos un local activo.');
    expect(api.create).not.toHaveBeenCalled();
    expect(host(fixture).querySelector<HTMLInputElement>('#staff-name')?.value).toBe(
      'Camila Mendoza',
    );
    expect(host(fixture).querySelector<HTMLInputElement>('#staff-email')?.value).toBe(
      'camila.mendoza@example.test',
    );

    setChecked(fixture, 'input[name="location"][value="local-miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalledWith({
      displayName: 'Camila Mendoza',
      email: 'camila.mendoza@example.test',
      phone: null,
      profile: 'Recepcion',
      assignedLocalIds: ['local-miraflores'],
    });
    expect(text(fixture)).toContain('Cuenta creada. La invitación se envió al correo indicado.');
  });

  it('muestra los errores del servidor sin presentar el alta como exitosa', async () => {
    const api = staffApi({
      create: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'duplicate-email' as const,
          detail: 'Ese correo ya pertenece a otra cuenta, aunque esté inactiva o pendiente.',
        },
      })),
    });
    const fixture = await render(api);

    button(fixture, 'Invitar persona').click();
    await flush(fixture);

    setInput(fixture, '#staff-name', 'Camila Mendoza');
    setInput(fixture, '#staff-email', 'valeria.torres@example.test');
    setChecked(fixture, 'input[name="location"][value="local-miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(api.create).toHaveBeenCalled();
    expect(text(fixture)).toContain(
      'Ese correo ya pertenece a otra cuenta, aunque esté inactiva o pendiente.',
    );
    expect(text(fixture)).not.toContain('Cuenta creada. La invitación se envió');
    expect(host(fixture).querySelector<HTMLInputElement>('#staff-name')?.value).toBe(
      'Camila Mendoza',
    );
  });

  it('no presenta como exitoso un alta cuyo correo no se envió y permite reenviar', async () => {
    const saved: StaffAccountResponse = { ...CAMILA, id: 'recep-nueva' };
    const list = vi
      .fn<StaffApi['list']>()
      .mockResolvedValueOnce({ ok: true, value: [ELENA, VALERIA] })
      .mockResolvedValue({ ok: true, value: [ELENA, VALERIA, saved] });
    const api = staffApi({
      list,
      create: vi.fn(async () => ({
        ok: false as const,
        failure: {
          kind: 'invitation-failed' as const,
          accountId: saved.id,
          detail: 'La cuenta se guardó, pero el correo no se pudo enviar.',
        },
      })),
      resendInvitation: vi
        .fn<StaffApi['resendInvitation']>()
        .mockResolvedValueOnce({
          ok: false as const,
          failure: {
            kind: 'invitation-failed' as const,
            accountId: saved.id,
            detail: 'El correo sigue sin enviarse.',
          },
        })
        .mockResolvedValueOnce({ ok: true, value: saved }),
    });
    const fixture = await render(api);

    button(fixture, 'Invitar persona').click();
    await flush(fixture);

    setInput(fixture, '#staff-name', 'Camila Mendoza');
    setInput(fixture, '#staff-email', 'camila.mendoza@example.test');
    setChecked(fixture, 'input[name="location"][value="local-miraflores"]', true);
    button(fixture, 'Crear y enviar invitación').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Correo no enviado');
    expect(text(fixture)).toContain('La cuenta sigue pendiente y sus datos están guardados.');
    expect(text(fixture)).not.toContain('Cuenta creada. La invitación se envió');

    button(fixture, 'Reenviar invitación').click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Enviar una invitación nueva?');
    button(fixture, 'Enviar invitación', dialog(fixture)).click();
    await flush(fixture);

    expect(api.resendInvitation).toHaveBeenCalledWith(saved.id);
    expect(text(fixture)).toContain('El correo sigue sin enviarse.');
    expect(text(fixture)).toContain('Correo no enviado');

    button(fixture, 'Reenviar invitación').click();
    await flush(fixture);
    button(fixture, 'Enviar invitación', dialog(fixture)).click();
    await flush(fixture);

    expect(text(fixture)).not.toContain('Correo no enviado');
    expect(text(fixture)).toContain('Nueva invitación enviada. El enlace anterior ya no sirve.');
  });

  it('protege la última cuenta de administración disponible', async () => {
    const api = staffApi({
      list: vi.fn(async () => ({ ok: true as const, value: [ELENA, MARTIN] })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Elena Ramírez').click();
    await flush(fixture);

    expect(text(fixture)).toContain('Es la última cuenta de administración disponible');

    button(fixture, 'Desactivar acceso').click();
    await flush(fixture);

    expect(dialog(fixture).textContent).toContain('Esta cuenta debe seguir activa');
    expect(api.setStatus).not.toHaveBeenCalled();

    button(fixture, 'Entendido', dialog(fixture)).click();
    await flush(fixture);

    expect(host(fixture).querySelector('[role="alertdialog"]')).toBeNull();
  });

  it('desactiva una cuenta solo después de confirmar', async () => {
    const updated: StaffAccountResponse = { ...VALERIA, status: 'Inactiva' };
    const api = staffApi({
      setStatus: vi.fn(async () => ({ ok: true as const, value: updated })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Valeria Torres').click();
    await flush(fixture);

    button(fixture, 'Desactivar acceso').click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Desactivar el acceso?');
    expect(api.setStatus).not.toHaveBeenCalled();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await flush(fixture);

    expect(host(fixture).querySelector('[role="alertdialog"]')).toBeNull();
    expect(text(fixture)).toContain('Datos y acceso');

    button(fixture, 'Desactivar acceso').click();
    await flush(fixture);
    button(fixture, 'Desactivar acceso', dialog(fixture)).click();
    await flush(fixture);

    expect(api.setStatus).toHaveBeenCalledWith('recep-1', false);
    expect(text(fixture)).toContain('Acceso desactivado. Sus datos se conservan.');
  });

  it('confirma el cambio de correo y aplica la protección del último administrador', async () => {
    const updated: StaffAccountResponse = { ...VALERIA, email: 'valeria.nueva@example.test' };
    const api = staffApi({
      update: vi.fn(async () => ({ ok: true as const, value: updated })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Valeria Torres').click();
    await flush(fixture);
    button(fixture, 'Editar').click();
    await flush(fixture);

    setInput(fixture, '#staff-email', 'valeria.nueva@example.test');
    button(fixture, 'Guardar cambios').click();
    await flush(fixture);

    expect(text(fixture)).toContain('¿Cambiar el correo de acceso?');
    expect(api.update).not.toHaveBeenCalled();

    button(fixture, 'Cambiar y enviar invitación', dialog(fixture)).click();
    await flush(fixture);

    expect(api.update).toHaveBeenCalledWith('recep-1', {
      displayName: 'Valeria Torres',
      email: 'valeria.nueva@example.test',
      phone: '987 123 456',
      profile: 'Recepcion',
      assignedLocalIds: ['local-miraflores'],
    });
    expect(text(fixture)).toContain('Cambios guardados.');

    personButton(fixture, 'Elena Ramírez').click();
    await flush(fixture);
    button(fixture, 'Editar').click();
    await flush(fixture);

    setInput(fixture, '#staff-email', 'elena.nueva@example.test');
    button(fixture, 'Guardar cambios').click();
    await flush(fixture);

    expect(text(fixture)).toContain('No puedes dejar al equipo sin administración disponible');
    expect(api.update).toHaveBeenCalledTimes(1);
  });

  it('conserva la edición obsoleta y permite volver a abrir los datos actualizados', async () => {
    const refreshed = { ...VALERIA, email: 'correo.actual@example.test' };
    const list = vi
      .fn<StaffApi['list']>()
      .mockResolvedValueOnce({ ok: true, value: [ELENA, VALERIA] })
      .mockResolvedValue({ ok: true, value: [ELENA, refreshed] });
    const api = staffApi({
      list,
      update: vi.fn(async () => ({
        ok: false as const,
        failure: { kind: 'concurrent-change' as const, detail: 'La cuenta cambió.' },
      })),
    });
    const fixture = await render(api);
    personButton(fixture, 'Valeria Torres').click();
    await flush(fixture);
    button(fixture, 'Editar').click();
    await flush(fixture);
    setInput(fixture, '#staff-name', 'Cambio sin guardar');
    button(fixture, 'Guardar cambios').click();
    await flush(fixture);

    expect(text(fixture)).toContain('La cuenta cambió');
    expect(host(fixture).querySelector<HTMLInputElement>('#staff-name')?.value).toBe(
      'Cambio sin guardar',
    );
    expect(button(fixture, 'Guardar cambios').disabled).toBe(true);
    expect(list).toHaveBeenCalledTimes(2);

    button(fixture, 'Cancelar').click();
    await flush(fixture);
    button(fixture, 'Descartar', dialog(fixture)).click();
    await flush(fixture);
    personButton(fixture, 'Valeria Torres').click();
    await flush(fixture);
    button(fixture, 'Editar').click();
    await flush(fixture);
    expect(host(fixture).querySelector<HTMLInputElement>('#staff-email')?.value).toBe(
      'correo.actual@example.test',
    );
    expect(button(fixture, 'Guardar cambios').disabled).toBe(false);
  });

  it('deja de mostrar cuentas y acciones cuando la sesión ya no es válida', async () => {
    const api = staffApi({
      list: vi.fn(async () => ({ ok: false as const, failure: { kind: 'unauthorized' as const } })),
    });
    const fixture = await render(api);

    expect(text(fixture)).toContain('Tu sesión ya no está disponible');
    expect(text(fixture)).toContain('Vuelve a iniciar sesión para comprobar tu acceso vigente.');
    expect(host(fixture).querySelector('a[href="/acceso"]')).not.toBeNull();
    expect(text(fixture)).not.toContain('valeria.torres@example.test');
    expect(hasButton(fixture, 'Invitar persona')).toBe(false);
    expect(host(fixture).querySelector('table')).toBeNull();
  });

  it('cierra la pantalla si la sesión se invalida durante una operación', async () => {
    const api = staffApi({
      setStatus: vi.fn(async () => ({
        ok: false as const,
        failure: { kind: 'unauthorized' as const },
      })),
    });
    const fixture = await render(api);

    personButton(fixture, 'Valeria Torres').click();
    await flush(fixture);
    button(fixture, 'Desactivar acceso').click();
    await flush(fixture);
    button(fixture, 'Desactivar acceso', dialog(fixture)).click();
    await flush(fixture);

    expect(text(fixture)).toContain('Tu sesión ya no está disponible');
    expect(host(fixture).querySelector('[role="alertdialog"]')).toBeNull();
    expect(host(fixture).querySelector('table')).toBeNull();
  });
});
