import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { GroupsService } from '../../core/groups.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { storePendingInviteToken } from '../../core/pending-invite-token';
import { createAction } from '../../shared/action-state/action-state';

@Component({
  selector: 'app-accept-invite',
  imports: [TranslatePipe],
  templateUrl: './accept-invite.html',
})
export class AcceptInvite {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly groups = inject(GroupsService);

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  protected readonly isAuthenticated = this.auth.isAuthenticated;

  protected readonly preview = resource({ loader: () => this.groups.previewInvite(this.token) });

  protected readonly accepting = createAction();
  protected readonly accepted = signal(false);

  protected logInToAccept(): void {
    storePendingInviteToken(this.token);
    void this.auth.login();
  }

  protected async accept(): Promise<void> {
    await this.accepting.run(
      true,
      async () => {
        await this.groups.acceptInvite(this.token);
        this.accepted.set(true);
      },
      (error) =>
        error instanceof HttpErrorResponse && error.status === 403
          ? 'invite.accept.wrongAccountError'
          : 'invite.accept.error',
    );
  }

  protected goToGroups(): void {
    void this.router.navigate(['/guardian/admin']);
  }
}
