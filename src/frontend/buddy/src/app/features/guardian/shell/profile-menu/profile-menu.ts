import { Component, HostListener, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { THEME_MODES, ThemeMode } from '../../../../core/theme';
import { ThemeService } from '../../../../core/theme.service';

@Component({
  selector: 'app-profile-menu',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './profile-menu.html'
})
export class ProfileMenu {
  private readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);

  protected readonly themeModes = THEME_MODES;

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);
  }

  protected close(): void {
    this.open.set(false);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }

  protected setTheme(mode: ThemeMode): void {
    this.theme.setMode(mode);
  }

  protected logout(): void {
    this.close();
    this.auth.logout();
  }
}
