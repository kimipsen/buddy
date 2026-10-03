import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { storePendingVerifyEmailToken } from '../../core/pending-verify-email-token';
import { UsersService } from '../../core/users.service';
import { createAction } from '../../shared/action-state/action-state';

@Component({
  selector: 'app-verify-email',
  imports: [TranslatePipe],
  templateUrl: './verify-email.html',
})
export class VerifyEmail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly users = inject(UsersService);

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  protected readonly isAuthenticated = this.auth.isAuthenticated;

  protected readonly verifying = createAction();
  protected readonly verified = signal(false);

  protected logInToVerify(): void {
    storePendingVerifyEmailToken(this.token);
    void this.auth.login();
  }

  protected async verify(): Promise<void> {
    await this.verifying.run(
      true,
      async () => {
        await this.users.verifyEmail(this.token);
        this.verified.set(true);
      },
      // The backend's validation failures now come back as a structured envelope
      // ({ code, message, details, requestId }), not a bare string body.
      (error) =>
        error instanceof HttpErrorResponse &&
        error.error &&
        typeof error.error === 'object' &&
        'message' in error.error
          ? String(error.error.message)
          : 'verifyEmail.error',
    );
  }

  protected goToApp(): void {
    void this.router.navigateByUrl('/');
  }
}
