import { Injectable } from '@angular/core';
import {
  createLocal,
  getLocalPhoto,
  listLocales,
  setLocalStatus,
  updateLocal,
  type CreateLocalForm,
  type HttpValidationProblemDetails,
  type LocalResponse,
  type ProblemDetails,
  type UpdateLocalForm,
} from '@mousedent/api-client';
import type { LocalesApi, LocalesFailure, LocalesResult } from './locales-api';

/**
 * Adaptador del cliente OpenAPI generado. Traduce cada respuesta a un resultado
 * que la aplicación pueda mostrar: datos inválidos, nombre repetido, local
 * inexistente o API no disponible.
 */
@Injectable()
export class GeneratedLocalesApi implements LocalesApi {
  async list(): Promise<LocalesResult<readonly LocalResponse[]>> {
    try {
      const result = await listLocales();

      return result.data ? succeed([...result.data]) : fail({ kind: 'unavailable' });
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }

  async create(form: CreateLocalForm): Promise<LocalesResult<LocalResponse>> {
    return this.write(() => createLocal({ body: form }));
  }

  async update(id: string, form: UpdateLocalForm): Promise<LocalesResult<LocalResponse>> {
    return this.write(() => updateLocal({ path: { id }, body: form }));
  }

  async setStatus(id: string, isActive: boolean): Promise<LocalesResult<LocalResponse>> {
    return this.write(() => setLocalStatus({ path: { id }, body: { isActive } }));
  }

  async photo(id: string): Promise<LocalesResult<Blob>> {
    try {
      const result = await getLocalPhoto({ path: { id } });

      if (result.data) {
        return succeed(result.data);
      }

      return fail(
        result.response?.status === 404 ? { kind: 'not-found' } : { kind: 'unavailable' },
      );
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }

  private async write(
    send: () => Promise<{
      data?: LocalResponse;
      error?: unknown;
      response?: Response;
    }>,
  ): Promise<LocalesResult<LocalResponse>> {
    try {
      const result = await send();

      return result.data
        ? succeed(result.data)
        : fail(classifyWriteFailure(result.response?.status, result.error));
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }
}

/** Traduce el error de una escritura a un motivo que la web pueda explicar. */
export function classifyWriteFailure(status: number | undefined, error: unknown): LocalesFailure {
  switch (status) {
    case 400:
      return { kind: 'validation', errors: validationErrors(error) };
    case 409:
      return { kind: 'duplicate', detail: problemDetail(error) };
    case 404:
      return { kind: 'not-found' };
    default:
      return { kind: 'unavailable' };
  }
}

/** Errores por campo de un `ValidationProblemDetails`, sin perder los mensajes. */
export function validationErrors(error: unknown): Readonly<Record<string, readonly string[]>> {
  if (typeof error !== 'object' || error === null) {
    return {};
  }

  const errors = (error as HttpValidationProblemDetails).errors;
  if (typeof errors !== 'object' || errors === null) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(errors).map(([field, messages]) => [
      field,
      Array.isArray(messages) ? messages.filter((message) => typeof message === 'string') : [],
    ]),
  );
}

function problemDetail(error: unknown): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return undefined;
  }

  const detail = (error as ProblemDetails).detail;
  return typeof detail === 'string' && detail.trim() ? detail : undefined;
}

function succeed<T>(value: T): LocalesResult<T> {
  return { ok: true, value };
}

function fail<T>(failure: LocalesFailure): LocalesResult<T> {
  return { ok: false, failure };
}
