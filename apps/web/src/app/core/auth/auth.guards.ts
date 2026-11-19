import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session';

/** Deja pasar solo con una sesión vigente; si no, pide iniciar sesión. */
export const requireSession: CanActivateFn = () => {
  const session = inject(SessionService);
  const router = inject(Router);

  if (session.isSessionActive()) {
    return true;
  }

  // Descarta un token vencido que siguiera en memoria y deja el aviso de caducidad.
  session.expire();
  return router.createUrlTree(['/acceso']);
};

/** Deja pasar solo al perfil `Administrador`; el resto recibe un rechazo visible. */
export const requireAdministrator: CanActivateFn = () => {
  const session = inject(SessionService);
  const router = inject(Router);

  if (!session.isSessionActive()) {
    session.expire();
    return router.createUrlTree(['/acceso']);
  }

  return session.isAdministrator() ? true : router.createUrlTree(['/sin-acceso']);
};

/**
 * La bienvenida es el destino de recepción y profesionales mientras no existan
 * sus módulos. Administración vuelve a su panel: no debe leer que no tiene
 * módulos de trabajo.
 */
export const requireNonAdministrator: CanActivateFn = () => {
  const session = inject(SessionService);
  const router = inject(Router);

  if (!session.isSessionActive()) {
    session.expire();
    return router.createUrlTree(['/acceso']);
  }

  return session.isAdministrator() ? router.createUrlTree(['/locales']) : true;
};
