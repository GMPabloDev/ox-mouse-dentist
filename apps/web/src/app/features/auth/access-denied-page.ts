import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { BrandMark } from '../../core/brand/brand-mark';

/**
 * Rechazo visible para una cuenta autenticada sin perfil `Administrador`. No
 * muestra el panel ni ofrece la gestión: solo explica la restricción y permite
 * cerrar la sesión.
 */
@Component({
  selector: 'app-access-denied-page',
  imports: [BrandMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './access-denied-page.html',
})
export class AccessDeniedPage {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly account = this.session.user;

  protected signOut(): void {
    this.session.logout();
    void this.router.navigateByUrl('/acceso');
  }
}
