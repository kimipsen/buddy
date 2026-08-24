import { Component, inject } from '@angular/core';

import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-login',
  imports: [TranslatePipe],
  templateUrl: './login.html'
})
export class Login {
  private readonly auth = inject(AuthService);

  protected signIn(): void {
    void this.auth.login();
  }
}
