import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { GroupRoleName, GROUP_ROLE_NAMES, GroupSummary, GroupsService } from '../../../core/groups.service';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MealplanAccessTier, MealplanScope, MealplansService } from '../../../core/mealplans.service';
import { AssignMealplan } from './assign-mealplan/assign-mealplan';
import { ManageMeals } from './manage-meals/manage-meals';

const MANAGE: MealplanAccessTier = 2;
const VIEW: MealplanAccessTier = 3;

type GroupMealplanScope = Extract<MealplanScope, { kind: 'group' }>;

@Component({
  selector: 'app-guardian-mealplan',
  imports: [RouterLink, FormsModule, ManageMeals, AssignMealplan, TranslatePipe],
  templateUrl: './mealplan.html'
})
export class GuardianMealplan implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly groupsService = inject(GroupsService);
  private readonly mealplans = inject(MealplansService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasChildren = signal(true);

  private familyChildId: string | null = null;
  protected readonly familyScope = signal<MealplanScope | null>(null);
  // Groups the guardian's own GroupRole maps to View or Manage tier for via
  // MealplanPermissionPolicy -- shown as switchable scopes regardless of whether a plan has
  // actually been shared with them yet (there's no "list my shared plans" endpoint;
  // ManageMeals/AssignMealplan surface their own error state if a group turns out to have no
  // shared plan).
  protected readonly groupScopes = signal<GroupMealplanScope[]>([]);
  protected readonly selectedScope = signal<MealplanScope | null>(null);

  // Sharing controls -- family scope only, since only a guardian can decide to share/unshare.
  protected readonly manageableGroups = signal<GroupSummary[]>([]);
  protected readonly sharedGroupId = signal<string | null>(null);
  protected readonly sharedGroupName = signal<string | null>(null);
  protected readonly shareTargetGroupId = signal('');
  protected readonly sharing = signal(false);
  protected readonly shareError = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected selectScope(scope: MealplanScope): void {
    this.selectedScope.set(scope);
  }

  protected isReadOnlyGroupScope(scope: GroupMealplanScope): boolean {
    return scope.accessTier !== MANAGE;
  }

  protected isSelected(scope: MealplanScope): boolean {
    const current = this.selectedScope();

    if (!current) {
      return false;
    }

    return current.kind === 'family' && scope.kind === 'family'
      ? current.childId === scope.childId
      : current.kind === 'group' && scope.kind === 'group' && current.groupId === scope.groupId;
  }

  protected async shareWithGroup(): Promise<void> {
    const groupId = this.shareTargetGroupId();
    const groupName = this.manageableGroups().find((group) => group.id === groupId)?.name;

    if (!this.familyChildId || !groupId || !groupName) {
      return;
    }

    this.sharing.set(true);
    this.shareError.set(null);

    try {
      await this.mealplans.shareWithGroup(this.familyChildId, groupId);
      this.sharedGroupId.set(groupId);
      this.sharedGroupName.set(groupName);
      this.shareTargetGroupId.set('');
      await this.loadGroupScopes();
    } catch {
      this.shareError.set('mealplan.sharing.shareError');
    } finally {
      this.sharing.set(false);
    }
  }

  protected async unshare(): Promise<void> {
    const groupId = this.sharedGroupId();

    if (!this.familyChildId || !groupId) {
      return;
    }

    this.sharing.set(true);
    this.shareError.set(null);

    try {
      await this.mealplans.unshareFromGroup(this.familyChildId, groupId);
      this.sharedGroupId.set(null);
      this.sharedGroupName.set(null);

      const current = this.selectedScope();
      if (current?.kind === 'group' && current.groupId === groupId) {
        this.selectedScope.set(this.familyScope());
      }

      await this.loadGroupScopes();
    } catch {
      this.shareError.set('mealplan.sharing.unshareError');
    } finally {
      this.sharing.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.familyChildId = children[0].id;

      const familyScope: MealplanScope = { kind: 'family', childId: children[0].id };
      this.familyScope.set(familyScope);
      this.selectedScope.set(familyScope);

      const [groups, sharedGroup] = await Promise.all([
        this.groupsService.listMyGroups(),
        this.mealplans.getSharedGroup(children[0].id)
      ]);

      // Only Owner/Admin can share/unshare (GroupAuthorization.CheckManage), matching the
      // backend's own gate.
      this.manageableGroups.set(groups.filter((g) => g.role === 0 || g.role === 1));
      this.sharedGroupId.set(sharedGroup?.groupId ?? null);
      this.sharedGroupName.set(sharedGroup?.groupName ?? null);

      await this.loadGroupScopesFrom(groups);
    } catch {
      this.error.set('mealplan.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadGroupScopes(): Promise<void> {
    const groups = await this.groupsService.listMyGroups();
    await this.loadGroupScopesFrom(groups);
  }

  private async loadGroupScopesFrom(groups: GroupSummary[]): Promise<void> {
    const details = await Promise.all(
      groups.map(async (group) => {
        try {
          return await this.groupsService.getGroup(group.id);
        } catch {
          return null;
        }
      })
    );

    const scopes: GroupMealplanScope[] = [];

    groups.forEach((group, index) => {
      const detail = details[index];
      const roleName: GroupRoleName = GROUP_ROLE_NAMES[group.role];
      const accessTier = detail?.mealplanPermissionPolicy[roleName];

      if (accessTier === MANAGE || accessTier === VIEW) {
        scopes.push({ kind: 'group', groupId: group.id, groupName: group.name, accessTier });
      }
    });

    this.groupScopes.set(scopes);
  }
}
