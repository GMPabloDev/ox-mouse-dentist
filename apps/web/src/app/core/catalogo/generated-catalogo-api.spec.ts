import type { ServicioResponse } from '@mousedent/api-client';
import { GeneratedCatalogoApi, classify } from './generated-catalogo-api';

const sdk = vi.hoisted(() => ({
  listEspecialidades: vi.fn(),
  listServicios: vi.fn(),
  createEspecialidad: vi.fn(),
  createServicio: vi.fn(),
  updateEspecialidad: vi.fn(),
  updateServicio: vi.fn(),
  setEspecialidadStatus: vi.fn(),
  setServicioStatus: vi.fn(),
}));

vi.mock('@mousedent/api-client', () => sdk);

const SERVICE: ServicioResponse = {
  id: 'v1',
  name: 'Limpieza',
  description: null,
  especialidadId: 's1',
  isActive: true,
};

function response(status: number): Response {
  return { status } as Response;
}

describe('respuestas de la API del catálogo', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('traduce los errores de validación por campo', () => {
    const failure = classify(400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { EspecialidadId: ['Selecciona una especialidad.'] },
    });

    expect(failure).toEqual({
      kind: 'validation',
      errors: { EspecialidadId: ['Selecciona una especialidad.'] },
    });
  });

  it('conserva el título y el detalle de un rechazo del servidor', () => {
    expect(
      classify(409, {
        title: 'Nombre duplicado',
        detail: 'Ya existe un registro con ese nombre, incluso entre los inactivos.',
        status: 409,
      }),
    ).toEqual({
      kind: 'rejected',
      title: 'Nombre duplicado',
      detail: 'Ya existe un registro con ese nombre, incluso entre los inactivos.',
    });
    expect(classify(404, { title: 'Registro no encontrado' })).toEqual({
      kind: 'rejected',
      title: 'Registro no encontrado',
      detail: undefined,
    });
  });

  it('distingue la falta de permiso de una API no disponible', () => {
    expect(classify(403, { title: 'Forbidden' })).toEqual({ kind: 'forbidden' });
    expect(classify(401, { title: 'Unauthorized' })).toEqual({ kind: 'forbidden' });
    expect(classify(500, { title: 'Error' })).toEqual({ kind: 'unavailable' });
    expect(classify(undefined, undefined)).toEqual({ kind: 'unavailable' });
  });

  it('entrega las listas completas o el motivo del fallo', async () => {
    sdk.listServicios.mockResolvedValue({ data: [SERVICE] });

    const listed = await new GeneratedCatalogoApi().servicios();

    expect(listed).toEqual({ ok: true, value: [SERVICE] });

    sdk.listServicios.mockResolvedValue({ error: {}, response: response(503) });

    expect(await new GeneratedCatalogoApi().servicios()).toEqual({
      ok: false,
      failure: { kind: 'unavailable' },
    });
  });

  it('envía el cambio de estado con el identificador del registro', async () => {
    sdk.setServicioStatus.mockResolvedValue({ data: { ...SERVICE, isActive: false } });

    const result = await new GeneratedCatalogoApi().setServicioStatus('v1', false);

    expect(sdk.setServicioStatus).toHaveBeenCalledWith({
      path: { id: 'v1' },
      body: { isActive: false },
    });
    expect(result).toEqual({ ok: true, value: { ...SERVICE, isActive: false } });
  });

  it('no inventa mensajes cuando el error no tiene la forma esperada', async () => {
    sdk.createEspecialidad.mockRejectedValue(new Error('fallo de red'));

    expect(
      await new GeneratedCatalogoApi().createEspecialidad({ name: 'Dental', description: null }),
    ).toEqual({ ok: false, failure: { kind: 'unavailable' } });
  });
});
