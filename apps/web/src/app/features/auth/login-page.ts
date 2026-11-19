import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type { LoginAttempt } from '../../core/auth/auth-api';
import { homePathFor, SessionService } from '../../core/auth/session';
import { PublicShell } from './public-shell';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Credenciales rechazadas por la API, sin distinguir cuenta inexistente y contraseña. */
const CREDENTIALS_MESSAGE = 'El correo o la contraseña no son correctos.';

@Component({
  selector: 'app-login-page',
  imports: [RouterLink, PublicShell],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly showPassword = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly endNotice = this.session.endNotice;

  protected onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  protected onPasswordInput(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
  }

  protected togglePassword(): void {
    this.showPassword.update((visible) => !visible);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();

    if (this.submitting()) {
      return;
    }

    const email = this.email().trim();
    const password = this.password();
    this.error.set(null);

    if (!email || !password) {
      this.error.set('Escribe tu correo y tu contraseña.');
      return;
    }

    if (!EMAIL_PATTERN.test(email)) {
      this.error.set('Escribe un correo electrónico válido.');
      return;
    }

    this.submitting.set(true);

    try {
      const attempt = await this.session.login(email, password);

      if (!attempt.ok) {
        this.error.set(failureMessage(attempt));
        return;
      }

      await this.router.navigateByUrl(homePathFor(this.session.isAdministrator()));
    } finally {
      this.submitting.set(false);
    }
  }
}

function failureMessage(attempt: Extract<LoginAttempt, { ok: false }>): string {
  switch (attempt.failure) {
    case 'credentials':
      return attempt.serverMessage ?? CREDENTIALS_MESSAGE;
    case 'request':
      return 'Revisa el correo y la contraseña.';
    case 'unavailable':
      return 'No se pudo iniciar sesión. Revisa tu conexión e inténtalo de nuevo.';
  }
}
