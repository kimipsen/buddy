import { Component, inject, signal } from '@angular/core';

import { AuthService } from '../../../../core/auth.service';
import { UsersService } from '../../../../core/users.service';

@Component({
  selector: 'app-delete-account',
  templateUrl: './delete-account.html'
})
export class DeleteAccount {
  private readonly users = inject(UsersService);
  private readonly auth = inject(AuthService);

  protected readonly confirmOpen = signal(false);
  protected readonly deleting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected openConfirm(): void {
    this.error.set(null);
    this.confirmOpen.set(true);
  }

  protected closeConfirm(): void {
    if (this.deleting()) {
      return;
    }

    this.confirmOpen.set(false);
  }

  protected async confirmDelete(): Promise<void> {
    this.deleting.set(true);
    this.error.set(null);

    try {
      await this.users.deleteCurrentUser();
      this.auth.logout();
    } catch {
      this.error.set('Unable to delete your account.');
      this.deleting.set(false);
    }
  }
}
