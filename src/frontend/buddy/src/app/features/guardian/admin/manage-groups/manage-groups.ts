import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { GroupInvite, GroupRole, GroupSummary, GroupsService } from '../../../../core/groups.service';

const ROLE_LABELS: Record<number, string> = {
  0: 'admin.manageGroups.roles.owner',
  1: 'admin.manageGroups.roles.admin',
  2: 'admin.manageGroups.roles.member'
};

// A group owner/admin can invite Admins or Members, never another Owner (matches the backend's
// InviteToGroup rejection of GroupRole.Owner).
const INVITABLE_ROLES: GroupRole[] = [1, 2];

@Component({
  selector: 'app-manage-groups',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-groups.html'
})
export class ManageGroups implements OnInit {
  private readonly groups = inject(GroupsService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly invitableRoles = INVITABLE_ROLES;

  protected readonly items = signal<GroupSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newGroupName = signal('');
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly expandedGroupId = signal<string | null>(null);
  protected readonly invitesByGroupId = signal<Record<string, GroupInvite[]>>({});
  protected readonly invitesLoading = signal<string | null>(null);
  protected readonly invitesError = signal<string | null>(null);

  protected readonly inviteEmail = signal('');
  protected readonly inviteRole = signal<GroupRole>(2);
  protected readonly inviting = signal(false);
  protected readonly inviteError = signal<string | null>(null);

  protected readonly revokingInviteId = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadGroups();
  }

  protected canManage(group: GroupSummary): boolean {
    return group.role === 0 || group.role === 1;
  }

  protected async createGroup(): Promise<void> {
    const name = this.newGroupName().trim();

    if (!name) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.groups.createGroup({ name });
      this.newGroupName.set('');
      await this.loadGroups();
    } catch {
      this.createError.set('admin.manageGroups.createError');
    } finally {
      this.creating.set(false);
    }
  }

  protected toggleInvitePanel(groupId: string): void {
    if (this.expandedGroupId() === groupId) {
      this.expandedGroupId.set(null);
      return;
    }

    this.expandedGroupId.set(groupId);
    this.inviteEmail.set('');
    this.inviteRole.set(2);
    this.inviteError.set(null);
    void this.loadInvites(groupId);
  }

  protected async sendInvite(groupId: string): Promise<void> {
    const email = this.inviteEmail().trim();

    if (!email) {
      return;
    }

    this.inviting.set(true);
    this.inviteError.set(null);

    try {
      await this.groups.inviteToGroup(groupId, { email, role: this.inviteRole() });
      this.inviteEmail.set('');
      await this.loadInvites(groupId);
    } catch {
      this.inviteError.set('admin.manageGroups.invite.sendError');
    } finally {
      this.inviting.set(false);
    }
  }

  protected async revokeInvite(groupId: string, inviteId: string): Promise<void> {
    this.revokingInviteId.set(inviteId);
    this.invitesError.set(null);

    try {
      await this.groups.revokeInvite(groupId, inviteId);
      await this.loadInvites(groupId);
    } catch {
      this.invitesError.set('admin.manageGroups.invite.cancelError');
    } finally {
      this.revokingInviteId.set(null);
    }
  }

  protected invitesFor(groupId: string): GroupInvite[] {
    return this.invitesByGroupId()[groupId] ?? [];
  }

  private async loadInvites(groupId: string): Promise<void> {
    this.invitesLoading.set(groupId);
    this.invitesError.set(null);

    try {
      const invites = await this.groups.listInvites(groupId);
      this.invitesByGroupId.update((byGroupId) => ({ ...byGroupId, [groupId]: invites }));
    } catch {
      this.invitesError.set('admin.manageGroups.invite.loadError');
    } finally {
      this.invitesLoading.set(null);
    }
  }

  private async loadGroups(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.items.set(await this.groups.listMyGroups());
    } catch {
      this.error.set('admin.manageGroups.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
