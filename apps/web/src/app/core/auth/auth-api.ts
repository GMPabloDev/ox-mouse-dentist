import { InjectionToken } from '@angular/core';
import type { SessionRecord, SessionUser } from './session';

/** Motivo por el que no se pudo iniciar sesión. */
export type LoginFailure = 'credentials' | 'request' | 'unavailable';

export type LoginAttempt =
  | { readonly ok: true; readonly session: SessionRecord }
  | {
      readonly ok: false;
      readonly failure: LoginFailure;
      /** Mensaje de la API cuando lo envió, por ejemplo al rechazar credenciales. */
      readonly serverMessage?: string;
    };

export type AccountCheck =
  | { readonly ok: true; readonly user: SessionUser }
  | { readonly ok: false; readonly failure: 'unauthorized' | 'unavailable' };

/**
 * Acceso a los endpoints de autenticación. Es el borde entre la web y la API:
 * el adaptador traduce las respuestas HTTP y el resto de la aplicación solo ve
 * resultados tipados.
 */
export interface AuthApi {
  login(email: string, password: string): Promise<LoginAttempt>;
  currentAccount(accessToken: string): Promise<AccountCheck>;
}

export const AUTH_API = new InjectionToken<AuthApi>('AUTH_API');
