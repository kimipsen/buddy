import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarRole } from '../../../../core/calendars.service';
import {
  CalendarPermissionPolicy,
  GroupInvite,
  GroupMember,
  GroupRole,
  GroupRoleName,
  GroupSummary,
  GroupsService,
  MealplanPermissionPolicy
} from '../../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealplanAccessTier } from '../../../../core/mealplans.service';

const ROLE_LABELS: Record<number, string> = {
  0: 'admin.manageGroups.roles.owner',
  1: 'admin.manageGroups.roles.admin',
  2: 'admin.manageGroups.roles.member'
};

// A group owner/admin can invite Admins or Members, never another Owner (matches the backend's
// InviteToGroup rejection of GroupRole.Owner).
const INVITABLE_ROLES: GroupRole[] = [1, 2];

const CALENDAR_ROLE_LABELS: Record<CalendarRole, string> = {
  0: 'admin.manageCalendars.roles.owner',
  1: 'admin.manageCalendars.roles.contributor',
  2: 'admin.manageCalendars.roles.viewer'
};

const CALENDAR_ROLES: CalendarRole[] = [0, 1, 2];

// Pairs each policy dictionary key (a GroupRole name string, per the backend's enum-dictionary-key
// serialization) with its numeric GroupRole ordinal so rows can reuse the existing ROLE_LABELS map.
const POLICY_ROWS: { key: GroupRoleName; role: GroupRole }[] = [
  { key: 'Owner', role: 0 },
  { key: 'Admin', role: 1 },
  { key: 'Member', role: 2 }
];

// None (0), Manage (2), and View (3) are the three valid group-policy values for meal plans --
// Rate (1) is the child's own tier and is rejected by the backend, so it's never offered here.
const MEALPLAN_TIER_LABELS: Record<number, string> = {
  0: 'admin.manageGroups.mealplanPolicy.tiers.none',
  2: 'admin.manageGroups.mealplanPolicy.tiers.manage',
  3: 'admin.manageGroups.mealplanPolicy.tiers.view'
};

const MEALPLAN_TIERS: MealplanAccessTier[] = [0, 3, 2];

@Component({
  selector: 'app-manage-groups',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-groups.html'
})
export class ManageGroups implements OnInit {
  private readonly groups = inject(GroupsService);
  private readonly guardians = inject(GuardiansService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly invitableRoles = INVITABLE_ROLES;
  protected readonly calendarRoleLabels = CALENDAR_ROLE_LABELS;
  protected readonly calendarRoles = CALENDAR_ROLES;
  protected readonly policyRows = POLICY_ROWS;
  protected readonly mealplanTierLabels = MEALPLAN_TIER_LABELS;
  protected readonly mealplanTiers = MEALPLAN_TIERS;

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

  protected readonly myChildren = signal<ChildSummary[]>([]);
  protected readonly expandedChildrenGroupId = signal<string | null>(null);
  protected readonly membersByGroupId = signal<Record<string, GroupMember[]>>({});
  protected readonly membersLoading = signal<string | null>(null);
  protected readonly membersError = signal<string | null>(null);

  protected readonly selectedChildId = signal('');
  protected readonly addingChild = signal(false);
  protected readonly addChildError = signal<string | null>(null);

  protected readonly expandedPolicyGroupId = signal<string | null>(null);
  protected readonly policyDraft = signal<CalendarPermissionPolicy | null>(null);
  protected readonly policyLoading = signal(false);
  protected readonly policyLoadError = signal<string | null>(null);
  protected readonly policySaving = signal(false);
  protected readonly policySaveError = signal<string | null>(null);

  protected readonly expandedMealplanPolicyGroupId = signal<string | null>(null);
  protected readonly mealplanPolicyDraft = signal<MealplanPermissionPolicy | null>(null);
  protected readonly mealplanPolicyLoading = signal(false);
  protected readonly mealplanPolicyLoadError = signal<string | null>(null);
  protected readonly mealplanPolicySaving = signal(false);
  protected readonly mealplanPolicySaveError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadGroups();
    void this.loadMyChildren();
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

  protected toggleChildrenPanel(groupId: string): void {
    if (this.expandedChildrenGroupId() === groupId) {
      this.expandedChildrenGroupId.set(null);
      return;
    }

    this.expandedChildrenGroupId.set(groupId);
    this.selectedChildId.set('');
    this.addChildError.set(null);
    void this.loadMembers(groupId);
  }

  protected availableChildrenFor(groupId: string): ChildSummary[] {
    const memberIds = new Set(this.membersFor(groupId).map((m) => m.userId));
    return this.myChildren().filter((child) => !memberIds.has(child.id));
  }

  protected membersFor(groupId: string): GroupMember[] {
    return this.membersByGroupId()[groupId] ?? [];
  }

  protected async addChild(groupId: string): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    this.addingChild.set(true);
    this.addChildError.set(null);

    try {
      await this.groups.addChildToGroup(groupId, childId);
      this.selectedChildId.set('');
      await this.loadMembers(groupId);
    } catch {
      this.addChildError.set('admin.manageGroups.children.addError');
    } finally {
      this.addingChild.set(false);
    }
  }

  private async loadMembers(groupId: string): Promise<void> {
    this.membersLoading.set(groupId);
    this.membersError.set(null);

    try {
      const group = await this.groups.getGroup(groupId);
      this.membersByGroupId.update((byGroupId) => ({ ...byGroupId, [groupId]: group.members }));
    } catch {
      this.membersError.set('admin.manageGroups.children.loadError');
    } finally {
      this.membersLoading.set(null);
    }
  }

  private async loadMyChildren(): Promise<void> {
    try {
      this.myChildren.set(await this.guardians.listMyChildren());
    } catch {
      // The children panel simply shows no candidates if this fails -- manage-children already
      // surfaces a dedicated load error for the guardian's own children list.
    }
  }

  protected togglePolicyPanel(groupId: string): void {
    if (this.expandedPolicyGroupId() === groupId) {
      this.expandedPolicyGroupId.set(null);
      this.policyDraft.set(null);
      return;
    }

    this.expandedPolicyGroupId.set(groupId);
    this.policyLoadError.set(null);
    this.policySaveError.set(null);
    void this.loadPolicy(groupId);
  }

  protected setDraftRole(roleKey: GroupRoleName, calendarRole: CalendarRole): void {
    const draft = this.policyDraft();

    if (!draft) {
      return;
    }

    this.policyDraft.set({ ...draft, [roleKey]: calendarRole });
  }

  protected async savePolicy(groupId: string): Promise<void> {
    const draft = this.policyDraft();

    if (!draft) {
      return;
    }

    this.policySaving.set(true);
    this.policySaveError.set(null);

    try {
      await this.groups.updateCalendarPermissionPolicy(groupId, draft);
    } catch {
      this.policySaveError.set('admin.manageGroups.policy.saveError');
    } finally {
      this.policySaving.set(false);
    }
  }

  private async loadPolicy(groupId: string): Promise<void> {
    this.policyLoading.set(true);

    try {
      const group = await this.groups.getGroup(groupId);
      this.policyDraft.set({ ...group.calendarPermissionPolicy });
    } catch {
      this.policyLoadError.set('admin.manageGroups.policy.loadError');
    } finally {
      this.policyLoading.set(false);
    }
  }

  protected toggleMealplanPolicyPanel(groupId: string): void {
    if (this.expandedMealplanPolicyGroupId() === groupId) {
      this.expandedMealplanPolicyGroupId.set(null);
      this.mealplanPolicyDraft.set(null);
      return;
    }

    this.expandedMealplanPolicyGroupId.set(groupId);
    this.mealplanPolicyLoadError.set(null);
    this.mealplanPolicySaveError.set(null);
    void this.loadMealplanPolicy(groupId);
  }

  protected setMealplanDraftTier(roleKey: GroupRoleName, tier: MealplanAccessTier): void {
    const draft = this.mealplanPolicyDraft();

    if (!draft) {
      return;
    }

    this.mealplanPolicyDraft.set({ ...draft, [roleKey]: tier });
  }

  protected async saveMealplanPolicy(groupId: string): Promise<void> {
    const draft = this.mealplanPolicyDraft();

    if (!draft) {
      return;
    }

    this.mealplanPolicySaving.set(true);
    this.mealplanPolicySaveError.set(null);

    try {
      await this.groups.updateMealplanPermissionPolicy(groupId, draft);
    } catch {
      this.mealplanPolicySaveError.set('admin.manageGroups.mealplanPolicy.saveError');
    } finally {
      this.mealplanPolicySaving.set(false);
    }
  }

  private async loadMealplanPolicy(groupId: string): Promise<void> {
    this.mealplanPolicyLoading.set(true);

    try {
      const group = await this.groups.getGroup(groupId);
      this.mealplanPolicyDraft.set({ ...group.mealplanPermissionPolicy });
    } catch {
      this.mealplanPolicyLoadError.set('admin.manageGroups.mealplanPolicy.loadError');
    } finally {
      this.mealplanPolicyLoading.set(false);
    }
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
