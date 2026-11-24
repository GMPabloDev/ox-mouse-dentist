import { Injectable } from '@angular/core';
import {
  createProfesional,
  listProfesionales,
  resendProfesionalInvitation,
  setProfesionalStatus,
  updateProfesional,
  type CreateProfesionalForm,
  type ProfesionalResponse,
  type UpdateProfesionalForm,
} from '@mousedent/api-client';
import { problemDetail, problemTitle, validationErrors } from '../api/problem-details';
import type { ProfesionalFailure, ProfesionalResult, ProfesionalesApi } from './profesionales-api';

/**
 * Adaptador del cliente OpenAPI generado. Traduce cada respuesta a un resultado
 * que la pantalla pueda explicar: datos inválidos, colegiatura repetida, correo
 * ocupado, regla de activación, correo sin enviar o API no disponible.
 */
@Injectable()
export class GeneratedProfesionalesApi implements ProfesionalesApi {
  async list(): Promise<ProfesionalResult<readonly ProfesionalResponse[]>> {
    try {
      const result = await listProfesionales();

      return result.data
        ? succeed([...result.data])
        : fail(classify(result.response?.status, result.error));
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }

  async create(form: CreateProfesionalForm): Promise<ProfesionalResult<ProfesionalResponse>> {
    return this.write(() => createProfesional({ body: form }));
  }

  async update(
    id: string,
    form: UpdateProfesionalForm,
  ): Promise<ProfesionalResult<ProfesionalResponse>> {
    return this.write(() => updateProfesional({ path: { id }, body: form }));
  }

  async setStatus(id: string, isActive: boolean): Promise<ProfesionalResult<ProfesionalResponse>> {
    return this.write(() => setProfesionalStatus({ path: { id }, body: { isActive } }));
  }

  async resendInvitation(id: string): Promise<ProfesionalResult<ProfesionalResponse>> {
    return this.write(() => resendProfesionalInvitation({ path: { id } }));
  }

  private async write(
    send: () => Promise<{
      data?: ProfesionalResponse;
      error?: unknown;
      response?: Response;
    }>,
  ): Promise<ProfesionalResult<ProfesionalResponse>> {
    try {
      const result = await send();

      return result.data
        ? succeed(result.data)
        : fail(classify(result.response?.status, result.error));
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }
}

/** Traduce el estado HTTP y el cuerpo de error a un motivo que la web explique. */
export function classify(status: number | undefined, error: unknown): ProfesionalFailure {
  switch (status) {
    case 400:
      return { kind: 'validation', errors: validationErrors(error) };
    case 401:
      return { kind: 'unauthorized' };
    case 403:
      return { kind: 'forbidden' };
    case 404:
      return { kind: 'not-found' };
    case 409:
      return classifyConflict(error);
    case 502:
      return {
        kind: 'invitation-failed',
        profesionalId: profesionalId(error),
        detail: problemDetail(error),
      };
    default:
      return { kind: 'unavailable' };
  }
}

/**
 * El estado 409 cubre varios conflictos. La API todavía no publica un código
 * estable, así que se usa el título del `ProblemDetails` para saber qué campo
 * corregir; los conflictos sin campo propio conservan el detalle del servidor.
 */
function classifyConflict(error: unknown): ProfesionalFailure {
  const detail = problemDetail(error);

  switch (problemTitle(error)) {
    case 'Colegiatura duplicada':
      return { kind: 'duplicate-colegiatura', detail };
    case 'Correo con ficha':
      return { kind: 'email-taken', detail };
    case 'Correo de otra cuenta':
      return { kind: 'other-profile', detail };
    case 'Activación incompleta':
      return { kind: 'activation-incomplete', detail };
    default:
      return { kind: 'conflict', detail };
  }
}

/**
 * La API envía el identificador de la ficha guardada como extensión del
 * `ProblemDetails` de `502`. El contrato generado no la incluye, así que se lee
 * como dato externo y se descarta si no tiene la forma esperada.
 */
function profesionalId(error: unknown): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return undefined;
  }

  const value = (error as Record<string, unknown>)['profesionalId'];
  return typeof value === 'string' && value ? value : undefined;
}

function succeed<T>(value: T): ProfesionalResult<T> {
  return { ok: true, value };
}

function fail<T>(failure: ProfesionalFailure): ProfesionalResult<T> {
  return { ok: false, failure };
}
