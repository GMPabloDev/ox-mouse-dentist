import { Injectable } from '@angular/core';
import { requestPasswordRecovery, resetPassword } from '@mousedent/api-client';
import { problemDetail, validationErrors } from '../api/problem-details';
import type { PasswordApi, RecoveryAttempt, ResetAttempt } from './password-api';

/**
 * Adaptador del cliente OpenAPI generado para los flujos públicos de contraseña.
 * La solicitud de recuperación solo distingue el límite de peticiones o una API
 * no disponible: nunca confirma si la cuenta existe.
 */
@Injectable()
export class GeneratedPasswordApi implements PasswordApi {
  async requestRecovery(email: string): Promise<RecoveryAttempt> {
    try {
      const result = await requestPasswordRecovery({ body: { email } });
      const status = result.response?.status;

      if (isSuccess(status)) {
        return { ok: true };
      }

      if (status === 400) {
        return { ok: false, failure: 'invalid-email', message: firstError(result.error) };
      }

      return status === 429
        ? { ok: false, failure: 'rate-limit' }
        : { ok: false, failure: 'unavailable' };
    } catch {
      return { ok: false, failure: 'unavailable' };
    }
  }

  async resetPassword(token: string, password: string): Promise<ResetAttempt> {
    try {
      const result = await resetPassword({ body: { token, password } });
      const status = result.response?.status;

      if (isSuccess(status)) {
        return { ok: true };
      }

      if (status !== 400) {
        return { ok: false, failure: 'unavailable' };
      }

      // Un enlace inservible llega como `ProblemDetails`; una contraseña que no
      // cumple la política, como `ValidationProblemDetails` con `errors`.
      const passwordProblems = validationErrors(result.error)['Password'] ?? [];

      return passwordProblems.length > 0
        ? { ok: false, failure: 'password', passwordProblems }
        : { ok: false, failure: 'invalid-link', message: problemDetail(result.error) };
    } catch {
      return { ok: false, failure: 'unavailable' };
    }
  }
}

function firstError(error: unknown): string | undefined {
  return Object.values(validationErrors(error))[0]?.[0] ?? problemDetail(error);
}

/** Sin respuesta no hubo error HTTP: el cliente generado devolvió datos válidos. */
function isSuccess(status: number | undefined): boolean {
  return status === undefined || (status >= 200 && status < 300);
}
