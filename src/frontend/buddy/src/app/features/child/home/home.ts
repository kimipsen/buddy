import { Component, OnInit, inject, signal } from '@angular/core';

import { AuthService } from '../../../core/auth.service';
import { GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-child-home',
  imports: [TranslatePipe],
  templateUrl: './home.html'
})
export class ChildHome implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly guardians = inject(GuardiansService);

  protected readonly guardianList = signal<GuardianSummary[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    void this.loadGuardians();
  }

  protected logout(): void {
    this.auth.logout();
  }

  private async loadGuardians(): Promise<void> {
    this.loading.set(true);

    try {
      this.guardianList.set(await this.guardians.listMyGuardians());
    } finally {
      this.loading.set(false);
    }
  }
}
