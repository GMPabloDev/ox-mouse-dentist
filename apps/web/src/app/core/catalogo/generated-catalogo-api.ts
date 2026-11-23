import { Injectable } from '@angular/core';
import {
  createEspecialidad,
  createServicio,
  listEspecialidades,
  listServicios,
  setEspecialidadStatus,
  setServicioStatus,
  updateEspecialidad,
  updateServicio,
  type EspecialidadRequest,
  type EspecialidadResponse,
  type ServicioRequest,
  type ServicioResponse,
} from '@mousedent/api-client';
import { problemDetail, problemTitle, validationErrors } from '../api/problem-details';
import type { CatalogoApi, CatalogoFailure, CatalogoResult } from './catalogo-api';

/**
 * Adaptador del cliente OpenAPI generado. Traduce cada respuesta a un resultado
 * que la pantalla pueda explicar: datos inválidos, un rechazo del servidor
 * (nombre repetido, especialidad ocupada o inactiva) o la API no disponible.
 */
@Injectable()
export class GeneratedCatalogoApi implements CatalogoApi {
  async especialidades(): Promise<CatalogoResult<readonly EspecialidadResponse[]>> {
    return this.read(() => listEspecialidades());
  }

  async servicios(): Promise<CatalogoResult<readonly ServicioResponse[]>> {
    return this.read(() => listServicios());
  }

  async createEspecialidad(
    request: EspecialidadRequest,
  ): Promise<CatalogoResult<EspecialidadResponse>> {
    return this.write(() => createEspecialidad({ body: request }));
  }

  async updateEspecialidad(
    id: string,
    request: EspecialidadRequest,
  ): Promise<CatalogoResult<EspecialidadResponse>> {
    return this.write(() => updateEspecialidad({ path: { id }, body: request }));
  }

  async createServicio(request: ServicioRequest): Promise<CatalogoResult<ServicioResponse>> {
    return this.write(() => createServicio({ body: request }));
  }

  async updateServicio(
    id: string,
    request: ServicioRequest,
  ): Promise<CatalogoResult<ServicioResponse>> {
    return this.write(() => updateServicio({ path: { id }, body: request }));
  }

  async setEspecialidadStatus(
    id: string,
    isActive: boolean,
  ): Promise<CatalogoResult<EspecialidadResponse>> {
    return this.write(() => setEspecialidadStatus({ path: { id }, body: { isActive } }));
  }

  async setServicioStatus(
    id: string,
    isActive: boolean,
  ): Promise<CatalogoResult<ServicioResponse>> {
    return this.write(() => setServicioStatus({ path: { id }, body: { isActive } }));
  }

  private async read<T>(
    send: () => Promise<{ data?: T[]; error?: unknown; response?: Response }>,
  ): Promise<CatalogoResult<readonly T[]>> {
    try {
      const result = await send();

      return result.data
        ? succeed([...result.data])
        : fail(classify(result.response?.status, result.error));
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }

  private async write<T>(
    send: () => Promise<{ data?: T; error?: unknown; response?: Response }>,
  ): Promise<CatalogoResult<T>> {
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
export function classify(status: number | undefined, error: unknown): CatalogoFailure {
  switch (status) {
    case 400:
      return { kind: 'validation', errors: validationErrors(error) };
    case 401:
    case 403:
      return { kind: 'forbidden' };
    case 404:
    case 409:
      return { kind: 'rejected', title: problemTitle(error), detail: problemDetail(error) };
    default:
      return { kind: 'unavailable' };
  }
}

function succeed<T>(value: T): CatalogoResult<T> {
  return { ok: true, value };
}

function fail<T>(failure: CatalogoFailure): CatalogoResult<T> {
  return { ok: false, failure };
}
