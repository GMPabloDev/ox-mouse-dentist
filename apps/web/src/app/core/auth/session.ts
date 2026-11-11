import { computed, inject, Injectable, signal } from '@angular/core';
import { AUTH_API, type LoginAttempt } from './auth-api';
import { SessionStore } from './session-storage';

/** Perfil con acceso a la gestión de locales. */
export const ADMINISTRATOR_ROLE = 'Administrador';

/** Aviso que ve el administrador cuando la API ya no reconoce su sesión. */
export const SESSION_EXPIRED_MESSAGE = 'Tu sesión caducó. Vuelve a iniciar sesión.';

export interface SessionUser {
  readonly id: string;
  readonly email: string;
  readonly displayName: string;
  readonly roles: readonly string[];
}

export interface SessionRecord {
  readonly accessToken: string;
  readonly expiresAt: string;
  readonly user: SessionUser;
}

/**
 * Sesión del personal. El estado vive en signals, se conserva en localStorage y
 * se revalida contra la API antes de confiar en un token guardado. La API sigue
 * siendo la autoridad: aquí solo se decide qué muestra la web.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly api = inject(AUTH_API);
  private readonly store = inject(SessionStore);

  private readonly current = signal<SessionRecord | null>(null);
  private readonly notice = signal<string | null>(null);

  readonly user = computed(() => this.current()?.user ?? null);
  readonly isAdministrator = computed(
    () => this.user()?.roles.includes(ADMINISTRATOR_ROLE) ?? false,
  );
  readonly endNotice = this.notice.asReadonly();

  /** Hay una sesión guardada en memoria, aunque su token haya vencido. */
  hasSession(): boolean {
    return this.current() !== null;
  }

  /** Hay una sesión guardada y su token sigue vigente. */
  isSessionActive(): boolean {
    const session = this.current();
    return session !== null && !isExpired(session.expiresAt);
  }

  accessToken(): string | null {
    return this.current()?.accessToken ?? null;
  }

  async login(email: string, password: string): Promise<LoginAttempt> {
    const attempt = await this.api.login(email, password);

    if (attempt.ok) {
      this.adopt(attempt.session);
      this.notice.set(null);
    }

    return attempt;
  }

  /**
   * Revalida la sesión guardada antes de confiar en ella. No navega: cuando una
   * ruta protegida detecta la sesión perdida, su guard decide el destino.
   */
  async restore(): Promise<void> {
    const stored = this.store.read();
    if (!stored) {
      return;
    }

    if (isExpired(stored.expiresAt)) {
      this.discard();
      this.notice.set(SESSION_EXPIRED_MESSAGE);
      return;
    }

    const account = await this.api.currentAccount(stored.accessToken);

    if (account.ok) {
      this.adopt({ ...stored, user: account.user });
      return;
    }

    if (account.failure === 'unavailable') {
      // La API no responde: el token sigue vigente y la API seguirá siendo la
      // autoridad en la próxima petición. No se cierra la sesión por esto.
      this.adopt(stored);
      return;
    }

    this.discard();
    this.notice.set(SESSION_EXPIRED_MESSAGE);
  }

  /** Cierra la sesión por decisión de la persona: sin aviso de caducidad. */
  logout(): void {
    this.discard();
    this.notice.set(null);
  }

  /** Descarta una sesión que la API rechazó y deja el aviso de caducidad. */
  expire(): void {
    if (!this.hasSession()) {
      return;
    }

    this.discard();
    this.notice.set(SESSION_EXPIRED_MESSAGE);
  }

  private adopt(session: SessionRecord): void {
    this.current.set(session);
    this.store.save(session);
  }

  private discard(): void {
    this.current.set(null);
    this.store.clear();
  }
}

function isExpired(expiresAt: string, now = Date.now()): boolean {
  const expires = Date.parse(expiresAt);
  return Number.isNaN(expires) || expires <= now;
}
