import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PASSWORD_API } from '../../core/auth/password-api';
import { PublicShell } from './public-shell';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Solicitud pública de un enlace de recuperación. La confirmación es genérica:
 * no revela si existe una cuenta, si está inactiva o si el envío falló.
 */
@Component({
  selector: 'app-recovery-page',
  imports: [RouterLink, PublicShell],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './recovery-page.html',
})
export class RecoveryPage {
  private readonly api = inject(PASSWORD_API);

  protected readonly email = signal('');
  protected readonly emailError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);

  protected onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
    this.emailError.set(null);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();

    if (this.submitting()) {
      return;
    }

    const email = this.email().trim();
    this.error.set(null);
    this.emailError.set(null);

    if (!email) {
      this.emailError.set('Escribe tu correo electrónico.');
      return;
    }

    if (!EMAIL_PATTERN.test(email)) {
      this.emailError.set('Escribe un correo electrónico válido.');
      return;
    }

    this.submitting.set(true);

    try {
      const attempt = await this.api.requestRecovery(email);

      if (!attempt.ok) {
        if (attempt.failure === 'invalid-email') {
          this.emailError.set(attempt.message ?? 'Escribe un correo electrónico válido.');
          return;
        }

        this.error.set(
          attempt.failure === 'rate-limit'
            ? 'Has realizado varias solicitudes. Espera un momento antes de volver a intentarlo.'
            : 'No pudimos conectar. Conservamos tu correo para que puedas reintentar.',
        );
        return;
      }

      // La API responde igual exista o no la cuenta: la pantalla también.
      this.sent.set(true);
    } finally {
      this.submitting.set(false);
    }
  }
}
