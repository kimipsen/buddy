import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  ChildSummary,
  CreateChildResult,
  GuardianInvite,
  GuardianKind,
  GuardiansService
} from '../../../../core/guardians.service';

const INVITABLE_KINDS: GuardianKind[] = [0, 1];

const KIND_LABELS: Record<GuardianKind, string> = {
  0: 'admin.manageChildren.invite.kinds.parent',
  1: 'admin.manageChildren.invite.kinds.guardian'
};

@Component({
  selector: 'app-manage-children',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-children.html'
})
export class ManageChildren implements OnInit {
  private readonly guardians = inject(GuardiansService);

  protected readonly invitableKinds = INVITABLE_KINDS;
  protected readonly kindLabels = KIND_LABELS;

  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly childrenLoading = signal(true);
  protected readonly childrenError = signal<string | null>(null);

  protected readonly newChildGivenName = signal('');
  protected readonly newChildFamilyName = signal('');
  protected readonly newChildUsername = signal('');
  protected readonly addingChild = signal(false);
  protected readonly addChildError = signal<string | null>(null);
  protected readonly lastCreatedChild = signal<CreateChildResult | null>(null);

  protected readonly revokingChildId = signal<string | null>(null);
  protected readonly confirmingRevokeChildId = signal<string | null>(null);
  protected readonly revokeError = signal<string | null>(null);

  protected readonly passwordCopied = signal(false);

  protected readonly expandedInviteChildId = signal<string | null>(null);
  protected readonly invitesByChildId = signal<Record<string, GuardianInvite[]>>({});
  protected readonly invitesLoading = signal<string | null>(null);
  protected readonly invitesError = signal<string | null>(null);

  protected readonly inviteEmail = signal('');
  protected readonly inviteKind = signal<GuardianKind>(0);
  protected readonly inviting = signal(false);
  protected readonly inviteError = signal<string | null>(null);

  protected readonly revokingInviteId = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected async addChild(): Promise<void> {
    const givenName = this.newChildGivenName().trim();
    const familyName = this.newChildFamilyName().trim();
    const username = this.newChildUsername().trim();

    if (!givenName || !familyName || !username) {
      return;
    }

    this.addingChild.set(true);
    this.addChildError.set(null);

    try {
      const created = await this.guardians.createChild({ givenName, familyName, username });
      this.lastCreatedChild.set(created);
      this.passwordCopied.set(false);
      this.newChildGivenName.set('');
      this.newChildFamilyName.set('');
      this.newChildUsername.set('');
      await this.loadChildren();
    } catch (error) {
      this.addChildError.set(error instanceof HttpErrorResponse && error.status === 409
        ? 'admin.manageChildren.usernameTakenError'
        : 'admin.manageChildren.addError');
    } finally {
      this.addingChild.set(false);
    }
  }

  protected requestRevoke(childId: string): void {
    this.revokeError.set(null);
    this.confirmingRevokeChildId.set(childId);
  }

  protected cancelRevoke(): void {
    this.confirmingRevokeChildId.set(null);
  }

  protected async confirmRevoke(childId: string): Promise<void> {
    this.revokingChildId.set(childId);
    this.revokeError.set(null);

    try {
      await this.guardians.revokeChild(childId);
      this.confirmingRevokeChildId.set(null);
      await this.loadChildren();
    } catch {
      this.revokeError.set('admin.manageChildren.revokeError');
    } finally {
      this.revokingChildId.set(null);
    }
  }

  protected toggleInvitePanel(childId: string): void {
    if (this.expandedInviteChildId() === childId) {
      this.expandedInviteChildId.set(null);
      return;
    }

    this.expandedInviteChildId.set(childId);
    this.inviteEmail.set('');
    this.inviteKind.set(0);
    this.inviteError.set(null);
    void this.loadInvites(childId);
  }

  protected invitesFor(childId: string): GuardianInvite[] {
    return this.invitesByChildId()[childId] ?? [];
  }

  protected async sendGuardianInvite(childId: string): Promise<void> {
    const email = this.inviteEmail().trim();

    if (!email) {
      return;
    }

    this.inviting.set(true);
    this.inviteError.set(null);

    try {
      await this.guardians.inviteGuardian(childId, { email, kind: this.inviteKind() });
      this.inviteEmail.set('');
      await this.loadInvites(childId);
    } catch {
      this.inviteError.set('admin.manageChildren.invite.sendError');
    } finally {
      this.inviting.set(false);
    }
  }

  protected async revokeGuardianInvite(childId: string, inviteId: string): Promise<void> {
    this.revokingInviteId.set(inviteId);
    this.invitesError.set(null);

    try {
      await this.guardians.revokeGuardianInvite(childId, inviteId);
      await this.loadInvites(childId);
    } catch {
      this.invitesError.set('admin.manageChildren.invite.cancelError');
    } finally {
      this.revokingInviteId.set(null);
    }
  }

  private async loadInvites(childId: string): Promise<void> {
    this.invitesLoading.set(childId);
    this.invitesError.set(null);

    try {
      const invites = await this.guardians.listGuardianInvites(childId);
      this.invitesByChildId.update((byChildId) => ({ ...byChildId, [childId]: invites }));
    } catch {
      this.invitesError.set('admin.manageChildren.invite.loadError');
    } finally {
      this.invitesLoading.set(null);
    }
  }

  protected async copyPassword(password: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(password);
      this.passwordCopied.set(true);
    } catch {
      this.passwordCopied.set(false);
    }
  }

  private async loadChildren(): Promise<void> {
    this.childrenLoading.set(true);
    this.childrenError.set(null);

    try {
      this.children.set(await this.guardians.listMyChildren());
    } catch {
      this.childrenError.set('admin.manageChildren.loadError');
    } finally {
      this.childrenLoading.set(false);
    }
  }
}
