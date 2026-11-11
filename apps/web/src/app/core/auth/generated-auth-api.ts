import { Injectable } from '@angular/core';
import {
  getCurrentUser,
  login,
  type AuthenticatedUser,
  type LoginResponse,
  type ProblemDetails,
} from '@mousedent/api-client';
import type { AccountCheck, AuthApi, LoginAttempt } from './auth-api';
import type { SessionRecord, SessionUser } from './session';

/**
 * Adaptador del cliente OpenAPI generado. Traduce cada respuesta a un resultado
 * que la aplicación pueda mostrar: credenciales rechazadas, datos inválidos o
 * API no disponible.
 */
@Injectable()
export class GeneratedAuthApi implements AuthApi {
  async login(email: string, password: string): Promise<LoginAttempt> {
    try {
      const result = await login({ body: { email, password } });

      if (result.data) {
        return { ok: true, session: toSession(result.data) };
      }

      const status = result.response?.status;

      if (status === 401) {
        return {
          ok: false,
          failure: 'credentials',
          serverMessage: problemDetail(result.error),
        };
      }

      return { ok: false, failure: status === 400 ? 'request' : 'unavailable' };
    } catch {
      return { ok: false, failure: 'unavailable' };
    }
  }

  async currentAccount(accessToken: string): Promise<AccountCheck> {
    try {
      // La sesión guardada todavía no se ha adoptado, así que el bearer viaja
      // explícito solo en esta comprobación.
      const result = await getCurrentUser({
        headers: { Authorization: `Bearer ${accessToken}` },
      });

      if (result.data) {
        return { ok: true, user: toUser(result.data) };
      }

      return result.response?.status === 401
        ? { ok: false, failure: 'unauthorized' }
        : { ok: false, failure: 'unavailable' };
    } catch {
      return { ok: false, failure: 'unavailable' };
    }
  }
}

function toSession(response: LoginResponse): SessionRecord {
  return {
    accessToken: response.accessToken,
    expiresAt: response.expiresAt,
    user: toUser(response.user),
  };
}

function toUser(user: AuthenticatedUser): SessionUser {
  return {
    id: user.id,
    email: user.email,
    displayName: user.displayName,
    roles: [...user.roles],
  };
}

function problemDetail(error: unknown): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return undefined;
  }

  const detail = (error as ProblemDetails).detail;
  return typeof detail === 'string' && detail.trim() ? detail : undefined;
}
