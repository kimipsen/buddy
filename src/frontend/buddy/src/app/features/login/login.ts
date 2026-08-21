import { Component, inject } from '@angular/core';

import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.html'
})
export class Login {
  private readonly auth = inject(AuthService);

  protected signIn(): void {
    void this.auth.login();
  }
}
