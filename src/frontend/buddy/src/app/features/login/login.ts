import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { NO_ACCESS_REASON, SESSION_EXPIRED_REASON } from '../../core/session-expired';

@Component({
  selector: 'app-login',
  imports: [TranslatePipe],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);

  private readonly reason = inject(ActivatedRoute).snapshot.queryParamMap.get('reason');

  protected readonly sessionExpired = this.reason === SESSION_EXPIRED_REASON;
  protected readonly noAccess = this.reason === NO_ACCESS_REASON;

  protected signIn(): void {
    void this.auth.login();
  }
}
