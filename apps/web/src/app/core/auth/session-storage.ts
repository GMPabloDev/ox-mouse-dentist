import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import type { SessionRecord, SessionUser } from './session';

/** Clave con la que se guarda la sesión en el navegador. */
const SESSION_STORAGE_KEY = 'mousedent.session';

/**
 * Único punto que conoce el formato persistido de la sesión. Lo que llega de
 * localStorage es un dato externo: se valida antes de usarlo y un contenido
 * inservible se descarta sin romper el arranque.
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly storage = this.resolveStorage();

  read(): SessionRecord | null {
    let raw: string | null = null;
    try {
      raw = this.storage?.getItem(SESSION_STORAGE_KEY) ?? null;
    } catch {
      return null;
    }

    if (!raw) {
      return null;
    }

    const session = parseSession(raw);
    if (!session) {
      this.clear();
    }

    return session;
  }

  save(session: SessionRecord): void {
    try {
      this.storage?.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Sin almacenamiento disponible la sesión sigue viva en memoria.
    }
  }

  clear(): void {
    try {
      this.storage?.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // Nada que limpiar si el almacenamiento está bloqueado.
    }
  }

  private resolveStorage(): Storage | null {
    const platformId = inject(PLATFORM_ID);
    const document = inject(DOCUMENT);

    if (!isPlatformBrowser(platformId)) {
      return null;
    }

    try {
      return document.defaultView?.localStorage ?? null;
    } catch {
      return null;
    }
  }
}

function parseSession(raw: string): SessionRecord | null {
  let value: unknown;
  try {
    value = JSON.parse(raw);
  } catch {
    return null;
  }

  if (typeof value !== 'object' || value === null) {
    return null;
  }

  const record = value as Record<string, unknown>;
  const accessToken = record['accessToken'];
  const expiresAt = record['expiresAt'];
  const user = record['user'];

  if (typeof accessToken !== 'string' || !accessToken) {
    return null;
  }

  if (typeof expiresAt !== 'string' || !expiresAt) {
    return null;
  }

  return isSessionUser(user) ? { accessToken, expiresAt, user } : null;
}

function isSessionUser(value: unknown): value is SessionUser {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const user = value as Record<string, unknown>;
  const roles = user['roles'];

  return (
    typeof user['id'] === 'string' &&
    typeof user['email'] === 'string' &&
    typeof user['displayName'] === 'string' &&
    Array.isArray(roles) &&
    roles.every((role) => typeof role === 'string')
  );
}
