import type { LocalResponse } from '@mousedent/api-client';
import {
  GeneratedLocalesApi,
  classifyWriteFailure,
  validationErrors,
} from './generated-locales-api';

const sdk = vi.hoisted(() => ({
  listLocales: vi.fn(),
  createLocal: vi.fn(),
  updateLocal: vi.fn(),
  setLocalStatus: vi.fn(),
  getLocalPhoto: vi.fn(),
}));

vi.mock('@mousedent/api-client', () => sdk);

const LOCAL: LocalResponse = {
  id: 'local-1',
  name: 'MouseDent Miraflores',
  address: 'Av. José Pardo 620, Miraflores',
  phone: '(01) 555 0120',
  isActive: true,
  schedule: [{ day: 'Monday', isClosed: true, opensAt: null, closesAt: null }],
  photo: null,
};

function response(status: number): Response {
  return { status } as Response;
}

describe('respuestas de la API de locales', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('traduce los errores de validación por campo', () => {
    const failure = classifyWriteFailure(400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        Name: ['El nombre es obligatorio.'],
        'Schedule[0].ClosesAt': ['La hora de cierre debe ser posterior a la hora de apertura.'],
      },
    });

    expect(failure).toEqual({
      kind: 'validation',
      errors: {
        Name: ['El nombre es obligatorio.'],
        'Schedule[0].ClosesAt': ['La hora de cierre debe ser posterior a la hora de apertura.'],
      },
    });
  });

  it('marca el nombre repetido con el detalle del servidor', () => {
    const failure = classifyWriteFailure(409, {
      title: 'Nombre de local repetido',
      detail: 'Ya existe un local con ese nombre.',
      status: 409,
    });

    expect(failure).toEqual({
      kind: 'duplicate',
      detail: 'Ya existe un local con ese nombre.',
    });
  });

  it('distingue un local inexistente de una API no disponible', () => {
    expect(classifyWriteFailure(404, { title: 'Local no encontrado' })).toEqual({
      kind: 'not-found',
    });
    expect(classifyWriteFailure(500, { title: 'Error' })).toEqual({ kind: 'unavailable' });
    expect(classifyWriteFailure(undefined, undefined)).toEqual({ kind: 'unavailable' });
  });

  it('no inventa mensajes cuando el error no tiene la forma esperada', () => {
    expect(validationErrors('fallo de red')).toEqual({});
    expect(validationErrors({ errors: { Name: 'no es un arreglo' } })).toEqual({ Name: [] });
  });

  it('envía el alta y la edición al endpoint que corresponde', async () => {
    sdk.createLocal.mockResolvedValue({ data: LOCAL });
    sdk.updateLocal.mockResolvedValue({ data: LOCAL });
    const api = new GeneratedLocalesApi();

    const form = {
      name: 'MouseDent Miraflores',
      address: 'Av. José Pardo 620',
      phone: '555 0120',
      schedule: '[]',
    };

    await api.create(form);
    await api.update('local-1', { ...form, photoAction: 'keep' });

    expect(sdk.createLocal).toHaveBeenCalledWith({ body: form });
    expect(sdk.updateLocal).toHaveBeenCalledWith({
      path: { id: 'local-1' },
      body: { ...form, photoAction: 'keep' },
    });
  });

  it('devuelve el nombre repetido que respondió la API al guardar', async () => {
    sdk.createLocal.mockResolvedValue({
      error: { title: 'Nombre de local repetido', detail: 'Ya existe un local con ese nombre.' },
      response: response(409),
    });

    const result = await new GeneratedLocalesApi().create({ name: 'Repetido' });

    expect(result).toEqual({
      ok: false,
      failure: { kind: 'duplicate', detail: 'Ya existe un local con ese nombre.' },
    });
  });

  it('pide la foto autenticada y la entrega como blob', async () => {
    const photo = new Blob(['foto'], { type: 'image/png' });
    sdk.getLocalPhoto.mockResolvedValue({ data: photo });

    const result = await new GeneratedLocalesApi().photo('local-1');

    expect(sdk.getLocalPhoto).toHaveBeenCalledWith({ path: { id: 'local-1' } });
    expect(result).toEqual({ ok: true, value: photo });
  });

  it('informa cuando el local o su foto ya no existen', async () => {
    sdk.getLocalPhoto.mockResolvedValue({ response: response(404) });
    sdk.listLocales.mockImplementation(async () => {
      throw new Error('sin conexión');
    });

    const api = new GeneratedLocalesApi();

    expect(await api.photo('local-1')).toEqual({ ok: false, failure: { kind: 'not-found' } });
    expect(await api.list()).toEqual({ ok: false, failure: { kind: 'unavailable' } });
  });
});
