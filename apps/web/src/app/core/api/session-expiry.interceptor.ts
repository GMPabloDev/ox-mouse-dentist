import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { client } from '@mousedent/api-client';
import { SessionService } from '../auth/session';

/**
 * El cliente generado usa `fetch` directo, no `HttpClient`, así que el bearer se
 * registra en él y los 401 se traducen en su cadena de interceptores. Una
 * petición con sesión que recibe 401 la cierra y devuelve al acceso.
 */
export function endSessionOnUnauthorized(
  session: SessionService,
  router: Router,
): (error: unknown, response?: Response, request?: Request) => unknown {
  return (error, response, request) => {
    const token = session.accessToken();
    if (
      response?.status === 401 &&
      token &&
      request?.headers.get('Authorization') === `Bearer ${token}`
    ) {
      session.expire();
      void router.navigateByUrl('/acceso');
    }

    return error;
  };
}

/** Registra el interceptor en el cliente generado. Devuelve su identificador. */
export function installSessionExpiryInterceptor(): number {
  const session = inject(SessionService);
  const router = inject(Router);

  return client.interceptors.error.use(endSessionOnUnauthorized(session, router));
}
