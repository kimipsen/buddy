import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { GroupsService } from '../../core/groups.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { storePendingInviteToken } from '../../core/pending-invite-token';

@Component({
  selector: 'app-accept-invite',
  imports: [TranslatePipe],
  templateUrl: './accept-invite.html'
})
export class AcceptInvite implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly groups = inject(GroupsService);

  private token = '';

  protected readonly isAuthenticated = this.auth.isAuthenticated;

  protected readonly loading = signal(true);
  protected readonly groupName = signal<string | null>(null);
  protected readonly previewError = signal<string | null>(null);

  protected readonly accepting = signal(false);
  protected readonly acceptError = signal<string | null>(null);
  protected readonly accepted = signal(false);

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    void this.loadPreview();
  }

  protected logInToAccept(): void {
    storePendingInviteToken(this.token);
    void this.auth.login();
  }

  protected async accept(): Promise<void> {
    this.accepting.set(true);
    this.acceptError.set(null);

    try {
      await this.groups.acceptInvite(this.token);
      this.accepted.set(true);
    } catch (error) {
      this.acceptError.set(error instanceof HttpErrorResponse && error.status === 403
        ? 'invite.accept.wrongAccountError'
        : 'invite.accept.error');
    } finally {
      this.accepting.set(false);
    }
  }

  protected goToGroups(): void {
    void this.router.navigate(['/guardian/admin']);
  }

  private async loadPreview(): Promise<void> {
    this.loading.set(true);
    this.previewError.set(null);

    try {
      const preview = await this.groups.previewInvite(this.token);
      this.groupName.set(preview.groupName);
    } catch {
      this.previewError.set('invite.preview.notFoundError');
    } finally {
      this.loading.set(false);
    }
  }
}
