import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { GuardianKind, GuardiansService } from '../../core/guardians.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { storePendingGuardianInviteToken } from '../../core/pending-guardian-invite-token';

const KIND_LABELS: Record<GuardianKind, string> = {
  0: 'invite.guardianPreview.kinds.parent',
  1: 'invite.guardianPreview.kinds.guardian'
};

@Component({
  selector: 'app-accept-guardian-invite',
  imports: [TranslatePipe],
  templateUrl: './accept-guardian-invite.html'
})
export class AcceptGuardianInvite implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly guardians = inject(GuardiansService);

  private token = '';

  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly kindLabels = KIND_LABELS;

  protected readonly loading = signal(true);
  protected readonly childGivenName = signal<string | null>(null);
  protected readonly kind = signal<GuardianKind | null>(null);
  protected readonly previewError = signal<string | null>(null);

  protected readonly accepting = signal(false);
  protected readonly acceptError = signal<string | null>(null);
  protected readonly accepted = signal(false);

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    void this.loadPreview();
  }

  protected logInToAccept(): void {
    storePendingGuardianInviteToken(this.token);
    void this.auth.login();
  }

  protected async accept(): Promise<void> {
    this.accepting.set(true);
    this.acceptError.set(null);

    try {
      await this.guardians.acceptGuardianInvite(this.token);
      this.accepted.set(true);
    } catch (error) {
      this.acceptError.set(error instanceof HttpErrorResponse && error.status === 403
        ? 'invite.guardianAccept.wrongAccountError'
        : 'invite.guardianAccept.error');
    } finally {
      this.accepting.set(false);
    }
  }

  protected goToChildren(): void {
    void this.router.navigate(['/guardian/admin']);
  }

  private async loadPreview(): Promise<void> {
    this.loading.set(true);
    this.previewError.set(null);

    try {
      const preview = await this.guardians.previewGuardianInvite(this.token);
      this.childGivenName.set(preview.childGivenName);
      this.kind.set(preview.kind);
    } catch {
      this.previewError.set('invite.guardianPreview.notFoundError');
    } finally {
      this.loading.set(false);
    }
  }
}
