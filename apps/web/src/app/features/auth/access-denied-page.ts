import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { profileLabel, SessionService } from '../../core/auth/session';
import { BrandMark } from '../../core/brand/brand-mark';

/**
 * Rechazo visible para una cuenta autenticada que intenta abrir una página
 * reservada a otro perfil. No se limita a la gestión de locales: explica la
 * restricción y ofrece volver a la bienvenida o cerrar sesión.
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
  protected readonly profile = this.session.profile;
  protected readonly profileLabel = profileLabel;

  protected goHome(): void {
    void this.router.navigateByUrl('/bienvenida');
  }

  protected signOut(): void {
    this.session.logout();
    void this.router.navigateByUrl('/acceso');
  }
}
