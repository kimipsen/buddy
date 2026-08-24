import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-profile-menu',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './profile-menu.html'
})
export class ProfileMenu {
  private readonly auth = inject(AuthService);

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);
  }

  protected close(): void {
    this.open.set(false);
  }

  protected logout(): void {
    this.close();
    this.auth.logout();
  }
}
