import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { HlmAvatar, HlmAvatarFallback } from '@mousedent/ui/avatar';
import { type SessionUser, SessionService } from '../../core/auth/session';
import { BrandMark } from '../../core/brand/brand-mark';

/**
 * Contenedor del panel. En este bloque solo existe el destino de locales y el
 * cierre de sesión: el sidebar no anuncia historias que aún no tienen pantalla.
 */
@Component({
  selector: 'app-panel-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, BrandMark, HlmAvatar, HlmAvatarFallback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './panel-layout.html',
  host: { '(document:keydown.escape)': 'closeSidebar()' },
})
export class PanelLayout {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly account = this.session.user;
  protected readonly mobileOpen = signal(false);
  protected readonly initials = computed(() => initialsOf(this.account()));
  protected readonly roleLabel = computed(() => this.account()?.roles.join(' · ') ?? '');

  protected openSidebar(): void {
    this.mobileOpen.set(true);
  }

  protected closeSidebar(): void {
    this.mobileOpen.set(false);
  }

  protected signOut(): void {
    this.session.logout();
    void this.router.navigateByUrl('/acceso');
  }
}

function initialsOf(user: SessionUser | null): string {
  const name = user?.displayName.trim() || user?.email.trim() || '';

  if (!name) {
    return '';
  }

  const words = name.split(/\s+/).filter(Boolean);
  const letters =
    words.length > 1
      ? words
          .slice(0, 2)
          .map((word) => word.charAt(0))
          .join('')
      : name.slice(0, 2);

  return letters.toUpperCase();
}
