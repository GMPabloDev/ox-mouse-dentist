import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PASSWORD_API } from '../../core/auth/password-api';
import { PublicShell } from './public-shell';

interface PasswordRequirement {
  readonly label: string;
  readonly met: (password: string) => boolean;
}

/** Requisitos de la política de Identity configurada en el proyecto. */
const REQUIREMENTS: readonly PasswordRequirement[] = [
  { label: 'Al menos 8 caracteres', met: (password) => password.length >= 8 },
  { label: 'Una letra mayúscula', met: (password) => /[A-Z]/.test(password) },
  { label: 'Una letra minúscula', met: (password) => /[a-z]/.test(password) },
  { label: 'Un número', met: (password) => /[0-9]/.test(password) },
  { label: 'Un símbolo', met: (password) => /[^a-zA-Z0-9]/.test(password) },
];

/**
 * Formulario público del enlace de invitación o recuperación
 * (`/establecer-contrasena?token=...`). La misma pantalla sirve para los dos
 * recorridos: no puede distinguirlos sin revelar información interna. Un enlace
 * usado, vencido o inexistente recibe un mensaje genérico con orientación.
 */
@Component({
  selector: 'app-reset-password-page',
  imports: [RouterLink, PublicShell],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password-page.html',
})
export class ResetPasswordPage {
  private readonly api = inject(PASSWORD_API);
  private readonly route = inject(ActivatedRoute);

  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  protected readonly password = signal('');
  protected readonly confirmation = signal('');
  protected readonly submitting = signal(false);
  protected readonly success = signal(false);
  protected readonly invalidLink = signal(this.token === '');
  protected readonly error = signal<string | null>(null);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly confirmationError = signal<string | null>(null);
  protected readonly policyProblems = signal<readonly string[]>([]);
  protected readonly showPassword = signal(false);
  protected readonly showConfirmation = signal(false);

  protected readonly requirements = REQUIREMENTS;
  protected readonly requirementState = computed(() =>
    REQUIREMENTS.map((requirement) => ({
      label: requirement.label,
      met: requirement.met(this.password()),
    })),
  );

  protected onPasswordInput(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
    this.passwordError.set(null);
    this.policyProblems.set([]);
  }

  protected onConfirmationInput(event: Event): void {
    this.confirmation.set((event.target as HTMLInputElement).value);
    this.confirmationError.set(null);
  }

  protected togglePassword(): void {
    this.showPassword.update((shown) => !shown);
  }

  protected toggleConfirmation(): void {
    this.showConfirmation.update((shown) => !shown);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();

    if (this.submitting() || this.invalidLink()) {
      return;
    }

    this.error.set(null);
    this.passwordError.set(null);
    this.confirmationError.set(null);
    this.policyProblems.set([]);

    if (!this.requirementState().every((requirement) => requirement.met)) {
      this.passwordError.set('La contraseña debe cumplir todos los requisitos.');
      return;
    }

    if (this.confirmation() !== this.password()) {
      this.confirmationError.set('Las contraseñas no coinciden.');
      return;
    }

    this.submitting.set(true);

    try {
      const attempt = await this.api.resetPassword(this.token, this.password());

      if (attempt.ok) {
        this.success.set(true);
        return;
      }

      switch (attempt.failure) {
        case 'invalid-link':
          this.invalidLink.set(true);
          return;
        case 'password':
          this.policyProblems.set(attempt.passwordProblems ?? []);
          this.passwordError.set(
            'La contraseña no cumple la política. Revisa los requisitos e inténtalo de nuevo.',
          );
          return;
        default:
          this.error.set(
            'No pudimos guardar la contraseña. Revisa tu conexión y vuelve a intentarlo.',
          );
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
