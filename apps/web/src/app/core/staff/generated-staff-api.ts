import { Injectable } from '@angular/core';
import {
  createStaffAccount,
  listStaffAccounts,
  resendStaffInvitation,
  setStaffAccountStatus,
  updateStaffAccount,
  type CreateStaffAccountForm,
  type StaffAccountResponse,
  type UpdateStaffAccountForm,
} from '@mousedent/api-client';
import { problemDetail, problemTitle, validationErrors } from '../api/problem-details';
import type { StaffApi, StaffFailure, StaffResult } from './staff-api';

/**
 * Adaptador del cliente OpenAPI generado. Traduce cada respuesta a un resultado
 * que la pantalla pueda explicar: datos inválidos, correo repetido, protección
 * del último administrador, correo sin enviar o API no disponible.
 */
@Injectable()
export class GeneratedStaffApi implements StaffApi {
  async list(): Promise<StaffResult<readonly StaffAccountResponse[]>> {
    try {
      const result = await listStaffAccounts();

      return result.data
        ? succeed([...result.data])
        : fail(classify(result.response?.status, result.error));
    } catch {
      return fail({ kind: 'unavailable' });
    }
  }

  async create(form: CreateStaffAccountForm): Promise<StaffResult<StaffAccountResponse>> {
    return this.write(() => createStaffAccount({ body: form }));
  }

  async update(
    id: string,
    form: UpdateStaffAccountForm,
  ): Promise<StaffResult<StaffAccountResponse>> {
    return this.write(() => updateStaffAccount({ path: { id }, body: form }));
  }

  async setStatus(id: string, isActive: boolean): Promise<StaffResult<StaffAccountResponse>> {
    return this.write(() => setStaffAccountStatus({ path: { id }, body: { isActive } }));
  }

  async resendInvitation(id: string): Promise<StaffResult<StaffAccountResponse>> {
    return this.write(() => resendStaffInvitation({ path: { id } }));
  }

  private async write(
    send: () => Promise<{
      data?: StaffAccountResponse;
      error?: unknown;
      response?: Response;
    }>,
  ): Promise<StaffResult<StaffAccountResponse>> {
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
export function classify(status: number | undefined, error: unknown): StaffFailure {
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
        accountId: accountId(error),
        detail: problemDetail(error),
      };
    default:
      return { kind: 'unavailable' };
  }
}

/**
 * El estado 409 cubre varios conflictos con el mismo código HTTP. La API todavía
 * no publica un código estable, así que se usa el título del `ProblemDetails`.
 */
function classifyConflict(error: unknown): StaffFailure {
  const detail = problemDetail(error);

  switch (problemTitle(error)) {
    case 'Cuenta modificada':
      return { kind: 'concurrent-change', detail };
    case 'Correo ya registrado':
      return { kind: 'duplicate-email', detail };
    case 'Último administrador disponible':
      return { kind: 'last-administrator', detail };
    default:
      return { kind: 'conflict', detail };
  }
}

/**
 * La API envía el identificador de la cuenta guardada como extensión del
 * `ProblemDetails` de `502`. El contrato generado no la incluye, así que se lee
 * como dato externo y se descarta si no tiene la forma esperada.
 */
function accountId(error: unknown): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return undefined;
  }

  const value = (error as Record<string, unknown>)['accountId'];
  return typeof value === 'string' && value ? value : undefined;
}

function succeed<T>(value: T): StaffResult<T> {
  return { ok: true, value };
}

function fail<T>(failure: StaffFailure): StaffResult<T> {
  return { ok: false, failure };
}
