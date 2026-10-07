import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { GuardianKind, GuardiansService } from '../../core/guardians.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { storePendingGuardianInviteToken } from '../../core/pending-guardian-invite-token';
import { createAction } from '../../shared/action-state/action-state';

import { isEmailNotVerified } from './email-not-verified';

const KIND_LABELS: Record<GuardianKind, string> = {
  0: 'invite.guardianPreview.kinds.parent',
  1: 'invite.guardianPreview.kinds.guardian',
};

@Component({
  selector: 'app-accept-guardian-invite',
  imports: [TranslatePipe],
  templateUrl: './accept-guardian-invite.html',
})
export class AcceptGuardianInvite {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly guardians = inject(GuardiansService);

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly kindLabels = KIND_LABELS;

  protected readonly preview = resource({
    loader: () => this.guardians.previewGuardianInvite(this.token),
  });

  protected readonly accepting = createAction();
  protected readonly accepted = signal(false);

  protected logInToAccept(): void {
    storePendingGuardianInviteToken(this.token);
    void this.auth.login();
  }

  protected async accept(): Promise<void> {
    await this.accepting.run(
      true,
      async () => {
        await this.guardians.acceptGuardianInvite(this.token);
        this.accepted.set(true);
      },
      (error) => {
        if (isEmailNotVerified(error)) {
          return 'invite.guardianAccept.emailNotVerifiedError';
        }
        return error instanceof HttpErrorResponse && error.status === 403
          ? 'invite.guardianAccept.wrongAccountError'
          : 'invite.guardianAccept.error';
      },
    );
  }

  protected goToChildren(): void {
    void this.router.navigate(['/guardian/admin']);
  }
}
