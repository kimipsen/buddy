import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { SESSION_EXPIRED_REASON } from '../../core/session-expired';

@Component({
  selector: 'app-login',
  imports: [TranslatePipe],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);

  protected readonly sessionExpired =
    inject(ActivatedRoute).snapshot.queryParamMap.get('reason') === SESSION_EXPIRED_REASON;

  protected signIn(): void {
    void this.auth.login();
  }
}
