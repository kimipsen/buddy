import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarRole } from '../../../../core/calendars.service';
import { FeaturesService } from '../../../../core/features.service';
import {
  GroupMember,
  GroupRole,
  GroupSummary,
  GroupsService,
  SentGroupInvite,
} from '../../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { MealplanAccessTier } from '../../../../core/mealplans.service';
import { createAction } from '../../../../shared/action-state/action-state';

const ROLE_LABELS: Record<GroupRole, string> = {
  Owner: 'admin.manageGroups.roles.owner',
  Admin: 'admin.manageGroups.roles.admin',
  Member: 'admin.manageGroups.roles.member',
};

// A group owner/admin can invite Admins or Members, never another Owner (matches the backend's
// InviteToGroup rejection of GroupRole.Owner).
const INVITABLE_ROLES: GroupRole[] = ['Admin', 'Member'];

const CALENDAR_ROLE_LABELS: Record<CalendarRole, string> = {
  Owner: 'admin.manageCalendars.roles.owner',
  Contributor: 'admin.manageCalendars.roles.contributor',
  Viewer: 'admin.manageCalendars.roles.viewer',
};

const CALENDAR_ROLES: CalendarRole[] = ['Owner', 'Contributor', 'Viewer'];

// One row per group role: key indexes the policy dictionary, role the ROLE_LABELS map.
const POLICY_ROWS: { key: GroupRole; role: GroupRole }[] = [
  { key: 'Owner', role: 'Owner' },
  { key: 'Admin', role: 'Admin' },
  { key: 'Member', role: 'Member' },
];

// None, Manage, and View are the three valid group-policy values for meal plans -- Rate is the
// child's own tier and is rejected by the backend, so it's never offered here.
type GroupMealplanTier = Exclude<MealplanAccessTier, 'Rate'>;

const MEALPLAN_TIER_LABELS: Record<GroupMealplanTier, string> = {
  None: 'admin.manageGroups.mealplanPolicy.tiers.none',
  Manage: 'admin.manageGroups.mealplanPolicy.tiers.manage',
  View: 'admin.manageGroups.mealplanPolicy.tiers.view',
};

const MEALPLAN_TIERS: GroupMealplanTier[] = ['None', 'View', 'Manage'];

@Component({
  selector: 'app-manage-groups',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-groups.html',
})
export class ManageGroups {
  private readonly groups = inject(GroupsService);
  private readonly guardians = inject(GuardiansService);
  private readonly translation = inject(TranslationService);
  protected readonly features = inject(FeaturesService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly invitableRoles = INVITABLE_ROLES;
  protected readonly calendarRoleLabels = CALENDAR_ROLE_LABELS;
  protected readonly calendarRoles = CALENDAR_ROLES;
  protected readonly policyRows = POLICY_ROWS;
  protected readonly mealplanTierLabels = MEALPLAN_TIER_LABELS;
  protected readonly mealplanTiers = MEALPLAN_TIERS;

  protected readonly items = resource({
    loader: () => this.groups.listMyGroups(),
  });

  protected readonly newGroupName = signal('');
  protected readonly creating = createAction();

  protected readonly expandedGroupId = signal<string | null>(null);
  // The pending invites of the group whose invite panel is open; idle while none is.
  protected readonly invites = resource({
    params: () => this.expandedGroupId() ?? undefined,
    loader: ({ params: groupId }) => this.groups.listInvites(groupId),
  });

  // Stryker disable next-line StringLiteral: toggleInvitePanel resets it before the invite form is ever rendered
  protected readonly inviteEmail = signal('');
  protected readonly inviteRole = signal<GroupRole>('Member');
  protected readonly inviting = createAction();
  protected readonly revokingInvite = createAction<string>();
  // The invite just sent from the open panel, whose link the inviter can copy or share themself
  // (SMS, chat). Gone once the panel closes: the server keeps only the token's hash.
  protected readonly sentInvite = signal<SentGroupInvite | null>(null);
  protected readonly inviteLinkCopied = signal(false);
  protected readonly canShareInviteLink = typeof navigator.share === 'function';

  // The children panel simply shows no candidates if this fails -- manage-children already
  // surfaces a dedicated load error for the guardian's own children list.
  protected readonly myChildren = resource({
    loader: () => this.guardians.listMyChildren().catch((): ChildSummary[] => []),
  });

  // The members panel and the add-child panel each load the members of the group they're open on.
  protected readonly expandedMembersGroupId = signal<string | null>(null);
  protected readonly members = resource({
    params: () => this.expandedMembersGroupId() ?? undefined,
    loader: ({ params: groupId }) => this.loadMembers(groupId),
  });

  protected readonly expandedChildrenGroupId = signal<string | null>(null);
  protected readonly childrenPanelMembers = resource({
    params: () => this.expandedChildrenGroupId() ?? undefined,
    loader: ({ params: groupId }) => this.loadMembers(groupId),
  });

  // Stryker disable next-line StringLiteral: toggleChildrenPanel resets it before the add-child form is ever rendered
  protected readonly selectedChildId = signal('');
  protected readonly addingChild = createAction();

  // The loaded policy doubles as the draft the selects edit until it is saved.
  protected readonly expandedPolicyGroupId = signal<string | null>(null);
  protected readonly policyDraft = resource({
    params: () => this.expandedPolicyGroupId() ?? undefined,
    loader: async ({ params: groupId }) => {
      const group = await this.groups.getGroup(groupId);
      return { ...group.calendarPermissionPolicy };
    },
  });
  protected readonly policySaving = createAction();

  protected readonly expandedMealplanPolicyGroupId = signal<string | null>(null);
  protected readonly mealplanPolicyDraft = resource({
    params: () => this.expandedMealplanPolicyGroupId() ?? undefined,
    loader: async ({ params: groupId }) => {
      const group = await this.groups.getGroup(groupId);
      return { ...group.mealplanPermissionPolicy };
    },
  });
  protected readonly mealplanPolicySaving = createAction();

  protected readonly confirmingDeleteGroupId = signal<string | null>(null);
  protected readonly deleting = createAction<string>();

  protected canManage(group: GroupSummary): boolean {
    return group.role === 'Owner' || group.role === 'Admin';
  }

  // Deleting a group is owner-only, unlike the admin-or-owner actions gated by canManage.
  protected isOwner(group: GroupSummary): boolean {
    return group.role === 'Owner';
  }

  protected requestDelete(groupId: string): void {
    this.deleting.clearError();
    this.confirmingDeleteGroupId.set(groupId);
  }

  protected cancelDelete(): void {
    this.confirmingDeleteGroupId.set(null);
  }

  protected async confirmDelete(groupId: string): Promise<void> {
    await this.deleting.run(
      groupId,
      async () => {
        await this.groups.deleteGroup(groupId);
        this.confirmingDeleteGroupId.set(null);
        this.items.reload();
      },
      'admin.manageGroups.delete.error',
    );
  }

  protected async createGroup(): Promise<void> {
    const name = this.newGroupName().trim();

    if (!name) {
      return;
    }

    await this.creating.run(
      true,
      async () => {
        await this.groups.createGroup({ name });
        this.newGroupName.set('');
        this.items.reload();
      },
      'admin.manageGroups.createError',
    );
  }

  protected toggleInvitePanel(groupId: string): void {
    if (this.expandedGroupId() === groupId) {
      this.expandedGroupId.set(null);
      return;
    }

    this.expandedGroupId.set(groupId);
    this.inviteEmail.set('');
    this.inviteRole.set('Member');
    this.sentInvite.set(null);
    this.inviting.clearError();
    this.revokingInvite.clearError();
  }

  protected async sendInvite(groupId: string): Promise<void> {
    const email = this.inviteEmail().trim();

    if (!email) {
      return;
    }

    await this.inviting.run(
      true,
      async () => {
        const invite = await this.groups.inviteToGroup(groupId, { email, role: this.inviteRole() });
        this.sentInvite.set(invite);
        this.inviteLinkCopied.set(false);
        this.inviteEmail.set('');
        this.revokingInvite.clearError();
        this.invites.reload();
      },
      'admin.manageGroups.invite.sendError',
    );
  }

  protected async copyInviteLink(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.inviteLinkCopied.set(true);
    } catch {
      this.inviteLinkCopied.set(false);
    }
  }

  protected async shareInviteLink(url: string, groupName: string): Promise<void> {
    try {
      await navigator.share({
        text: this.translation.translate('admin.manageGroups.invite.shareText', {
          group: groupName,
        }),
        url,
      });
    } catch {
      // Dismissing the share sheet rejects too; the link is still there to copy.
    }
  }

  protected async revokeInvite(groupId: string, inviteId: string): Promise<void> {
    await this.revokingInvite.run(
      inviteId,
      async () => {
        await this.groups.revokeInvite(groupId, inviteId);
        this.invites.reload();
      },
      'admin.manageGroups.invite.cancelError',
    );
  }

  protected toggleChildrenPanel(groupId: string): void {
    if (this.expandedChildrenGroupId() === groupId) {
      this.expandedChildrenGroupId.set(null);
      return;
    }

    this.expandedChildrenGroupId.set(groupId);
    this.selectedChildId.set('');
    this.addingChild.clearError();
  }

  protected availableChildren(members: GroupMember[]): ChildSummary[] {
    const memberIds = new Set(members.map((m) => m.userId));
    return (this.myChildren.value() ?? []).filter((child) => !memberIds.has(child.id));
  }

  protected guardianMembers(members: GroupMember[]): GroupMember[] {
    return members.filter((m) => !m.isChild);
  }

  protected childMembers(members: GroupMember[]): GroupMember[] {
    return members.filter((m) => m.isChild);
  }

  protected toggleMembersPanel(groupId: string): void {
    if (this.expandedMembersGroupId() === groupId) {
      this.expandedMembersGroupId.set(null);
      return;
    }

    this.expandedMembersGroupId.set(groupId);
  }

  protected async addChild(groupId: string): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    await this.addingChild.run(
      true,
      async () => {
        await this.groups.addChildToGroup(groupId, childId);
        this.selectedChildId.set('');
        this.childrenPanelMembers.reload();

        // The members panel shows the new child too when it's open on the same group.
        if (this.expandedMembersGroupId() === groupId) {
          this.members.reload();
        }
      },
      'admin.manageGroups.children.addError',
    );
  }

  private async loadMembers(groupId: string): Promise<GroupMember[]> {
    const group = await this.groups.getGroup(groupId);
    return group.members;
  }

  protected togglePolicyPanel(groupId: string): void {
    if (this.expandedPolicyGroupId() === groupId) {
      this.expandedPolicyGroupId.set(null);
      return;
    }

    this.expandedPolicyGroupId.set(groupId);
    this.policySaving.clearError();
  }

  protected setDraftRole(roleKey: GroupRole, calendarRole: CalendarRole): void {
    this.policyDraft.update((draft) => draft && { ...draft, [roleKey]: calendarRole });
  }

  protected async savePolicy(groupId: string): Promise<void> {
    if (!this.policyDraft.hasValue()) {
      return;
    }

    const draft = this.policyDraft.value();

    await this.policySaving.run(
      true,
      () => this.groups.updateCalendarPermissionPolicy(groupId, draft),
      'admin.manageGroups.policy.saveError',
    );
  }

  protected toggleMealplanPolicyPanel(groupId: string): void {
    if (this.expandedMealplanPolicyGroupId() === groupId) {
      this.expandedMealplanPolicyGroupId.set(null);
      return;
    }

    this.expandedMealplanPolicyGroupId.set(groupId);
    this.mealplanPolicySaving.clearError();
  }

  protected setMealplanDraftTier(roleKey: GroupRole, tier: MealplanAccessTier): void {
    this.mealplanPolicyDraft.update((draft) => draft && { ...draft, [roleKey]: tier });
  }

  protected async saveMealplanPolicy(groupId: string): Promise<void> {
    if (!this.mealplanPolicyDraft.hasValue()) {
      return;
    }

    const draft = this.mealplanPolicyDraft.value();

    await this.mealplanPolicySaving.run(
      true,
      () => this.groups.updateMealplanPermissionPolicy(groupId, draft),
      'admin.manageGroups.mealplanPolicy.saveError',
    );
  }
}
