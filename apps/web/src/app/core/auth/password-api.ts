import { InjectionToken } from '@angular/core';

/**
 * Resultado de pedir un enlace de recuperación. La API responde de forma
 * genérica, así que este resultado nunca indica si la cuenta existe.
 */
export type RecoveryAttempt =
  | { readonly ok: true }
  | {
      readonly ok: false;
      readonly failure: 'invalid-email' | 'rate-limit' | 'unavailable';
      readonly message?: string;
    };

/** Resultado de establecer la contraseña con el token de un enlace. */
export type ResetAttempt =
  | { readonly ok: true }
  | {
      readonly ok: false;
      /**
       * `invalid-link`: el enlace está usado, vencido o no existe.
       * `password`: la contraseña nueva no cumple la política.
       */
      readonly failure: 'invalid-link' | 'password' | 'unavailable';
      readonly message?: string;
      readonly passwordProblems?: readonly string[];
    };

/**
 * Acceso a los flujos públicos de contraseña. Son anónimos: no dependen de la
 * sesión y el adaptador no adjunta el bearer.
 */
export interface PasswordApi {
  requestRecovery(email: string): Promise<RecoveryAttempt>;
  resetPassword(token: string, password: string): Promise<ResetAttempt>;
}

export const PASSWORD_API = new InjectionToken<PasswordApi>('PASSWORD_API');
