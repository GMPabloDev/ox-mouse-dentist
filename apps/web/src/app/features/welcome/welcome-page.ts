import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HlmAvatar, HlmAvatarFallback } from '@mousedent/ui/avatar';
import { profileLabel, SessionService } from '../../core/auth/session';
import { BrandMark } from '../../core/brand/brand-mark';

/**
 * Bienvenida de recepción y profesionales: identidad de la cuenta y cierre de
 * sesión. No anuncia módulos que todavía no existen ni ofrece acciones que la
 * API aún no autoriza.
 */
@Component({
  selector: 'app-welcome-page',
  imports: [BrandMark, HlmAvatar, HlmAvatarFallback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './welcome-page.html',
})
export class WelcomePage {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly account = this.session.user;
  protected readonly profile = computed(() => {
    const profile = this.session.profile();

    return profile ? profileLabel(profile) : '';
  });
  protected readonly firstName = computed(
    () => this.session.user()?.displayName.trim().split(/\s+/)[0] ?? '',
  );
  protected readonly initials = computed(() => initialsOf(this.session.user()?.displayName ?? ''));

  protected signOut(): void {
    this.session.logout();
    void this.router.navigateByUrl('/acceso');
  }
}

function initialsOf(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0))
    .join('')
    .toUpperCase();
}
