import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { UsersService } from '../../core/users.service';
import { createAction } from '../action-state/action-state';

// Sends the signed-in user another verification email for their current address. Used where an
// unverified email blocks the user: the profile, and accepting an invite.
@Component({
  selector: 'app-resend-verification',
  imports: [TranslatePipe],
  templateUrl: './resend-verification.html',
})
export class ResendVerification {
  private readonly users = inject(UsersService);

  protected readonly sending = createAction();
  protected readonly sent = signal(false);

  protected async resend(): Promise<void> {
    this.sent.set(false);
    await this.sending.run(
      true,
      async () => {
        await this.users.resendEmailVerification();
        this.sent.set(true);
      },
      (error) =>
        error instanceof HttpErrorResponse && error.status === 409
          ? 'verifyEmail.resend.cooldownError'
          : 'verifyEmail.resend.error',
    );
  }
}
