import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { storePendingVerifyEmailToken } from '../../core/pending-verify-email-token';
import { UsersService } from '../../core/users.service';

@Component({
  selector: 'app-verify-email',
  imports: [TranslatePipe],
  templateUrl: './verify-email.html'
})
export class VerifyEmail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly users = inject(UsersService);

  private token = '';

  protected readonly isAuthenticated = this.auth.isAuthenticated;

  protected readonly verifying = signal(false);
  protected readonly verifyError = signal<string | null>(null);
  protected readonly verified = signal(false);

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
  }

  protected logInToVerify(): void {
    storePendingVerifyEmailToken(this.token);
    void this.auth.login();
  }

  protected async verify(): Promise<void> {
    this.verifying.set(true);
    this.verifyError.set(null);

    try {
      await this.users.verifyEmail(this.token);
      this.verified.set(true);
    } catch (error) {
      this.verifyError.set(
        error instanceof HttpErrorResponse && typeof error.error === 'string'
          ? error.error
          : 'verifyEmail.error'
      );
    } finally {
      this.verifying.set(false);
    }
  }

  protected goToApp(): void {
    void this.router.navigateByUrl('/');
  }
}
