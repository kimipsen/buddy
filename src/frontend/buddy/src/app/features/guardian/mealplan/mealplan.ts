import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { FeaturesService } from '../../../core/features.service';
import { GroupSummary, GroupsService } from '../../../core/groups.service';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import {
  MealplanAccessTier,
  MealplanScope,
  MealplansService,
} from '../../../core/mealplans.service';
import { createAction } from '../../../shared/action-state/action-state';
import { AssignMealplan } from './assign-mealplan/assign-mealplan';
import { MealplanIcal } from './mealplan-ical/mealplan-ical';
import { ManageMeals } from './manage-meals/manage-meals';
import { Card } from '../../../shared/card/card';
import { Page } from '../../../shared/page/page';

const MANAGE: MealplanAccessTier = 'Manage';
const VIEW: MealplanAccessTier = 'View';

type GroupMealplanScope = Extract<MealplanScope, { kind: 'group' }>;
type FamilyMealplanScope = Extract<MealplanScope, { kind: 'family' }>;

interface SharedGroup {
  groupId: string;
  groupName: string;
}

// What the page loaded for the family scope: the groups it can be shared with, the group it is
// shared with, and the group scopes to offer.
interface LoadedGroups {
  manageableGroups: GroupSummary[];
  sharedGroup: SharedGroup | null;
  // Groups the guardian's own GroupRole maps to View or Manage tier for via
  // MealplanPermissionPolicy, further filtered down to only those a plan has actually been
  // shared with (via GetGroupMealplanStatus) -- a qualifying-tier group with nothing shared yet
  // has no meals to show, and clicking into it used to 404.
  groupScopes: GroupMealplanScope[];
}

const EMPTY_GROUPS: LoadedGroups = {
  manageableGroups: [],
  sharedGroup: null,
  groupScopes: [],
};

@Component({
  selector: 'app-guardian-mealplan',
  imports: [
    RouterLink,
    FormsModule,
    ManageMeals,
    AssignMealplan,
    MealplanIcal,
    TranslatePipe,
    Card,
    Page,
  ],
  templateUrl: './mealplan.html',
})
export class GuardianMealplan {
  protected readonly features = inject(FeaturesService);
  private readonly guardians = inject(GuardiansService);
  private readonly groupsService = inject(GroupsService);
  private readonly mealplans = inject(MealplansService);

  // The first child's family scope, or null when the guardian has no children.
  protected readonly children = resource({ loader: () => this.loadFamilyScope() });
  protected readonly familyScope = computed((): FamilyMealplanScope | null =>
    this.children.hasValue() ? this.children.value() : null,
  );
  // Loaded separately from the family scope, so a failure here still shows the family's plan
  // next to the load error.
  protected readonly groups = resource({
    params: () => this.familyScope() ?? undefined,
    loader: ({ params: familyScope }) => this.loadGroups(familyScope.childId),
  });
  // Empty while loading or after a failed load, so the sharing section still renders.
  protected readonly loaded = computed((): LoadedGroups =>
    this.groups.hasValue() ? this.groups.value() : EMPTY_GROUPS,
  );
  // The family scope until the guardian picks a group scope.
  protected readonly selectedScope = linkedSignal<MealplanScope | null>(() => this.familyScope());

  // Sharing controls -- family scope only, since only a guardian can decide to share/unshare.
  protected readonly shareTargetGroupId = signal('');
  protected readonly sharing = createAction();

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
    const familyScope = this.familyScope();
    const { manageableGroups } = this.loaded();
    const groupId = this.shareTargetGroupId();
    const groupName = manageableGroups.find((group) => group.id === groupId)?.name;

    if (!familyScope || !groupId || !groupName) {
      return;
    }

    await this.sharing.run(
      true,
      async () => {
        await this.mealplans.shareWithGroup(familyScope.childId, groupId);
        this.updatePage({ sharedGroup: { groupId, groupName } });
        this.shareTargetGroupId.set('');
        this.updatePage({ groupScopes: await this.loadGroupScopes() });
      },
      'mealplan.sharing.shareError',
    );
  }

  protected async unshare(): Promise<void> {
    const familyScope = this.familyScope();
    const { sharedGroup } = this.loaded();

    if (!familyScope || !sharedGroup) {
      return;
    }

    const groupId = sharedGroup.groupId;

    await this.sharing.run(
      true,
      async () => {
        await this.mealplans.unshareFromGroup(familyScope.childId, groupId);
        this.updatePage({ sharedGroup: null });

        const current = this.selectedScope();
        if (current?.kind === 'group' && current.groupId === groupId) {
          this.selectedScope.set(familyScope);
        }

        this.updatePage({ groupScopes: await this.loadGroupScopes() });
      },
      'mealplan.sharing.unshareError',
    );
  }

  private updatePage(change: Partial<LoadedGroups>): void {
    this.groups.update((current) => current && { ...current, ...change });
  }

  private async loadFamilyScope(): Promise<FamilyMealplanScope | null> {
    const [firstChild] = await this.guardians.listMyChildren();
    return firstChild ? { kind: 'family', childId: firstChild.id } : null;
  }

  private async loadGroups(childId: string): Promise<LoadedGroups> {
    const [groups, sharedGroup] = await Promise.all([
      this.groupsService.listMyGroups(),
      this.mealplans.getSharedGroup(childId),
    ]);

    return {
      // Only Owner/Admin can share/unshare (GroupAuthorization.CheckManage), matching the
      // backend's own gate.
      manageableGroups: groups.filter((g) => g.role === 'Owner' || g.role === 'Admin'),
      sharedGroup,
      groupScopes: await this.loadGroupScopesFrom(groups),
    };
  }

  private async loadGroupScopes(): Promise<GroupMealplanScope[]> {
    const groups = await this.groupsService.listMyGroups();
    return this.loadGroupScopesFrom(groups);
  }

  private async loadGroupScopesFrom(groups: GroupSummary[]): Promise<GroupMealplanScope[]> {
    // One GetGroup per group, then one status call per candidate group: both bounded so a
    // guardian in many groups doesn't burst the API. Each is best-effort per group (a failure
    // just drops that group from the scope list), as before.
    const details = await mapWithConcurrency(
      groups,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (group) => {
        try {
          return { group, detail: await this.groupsService.getGroup(group.id) };
        } catch {
          return { group, detail: null };
        }
      },
    );

    const candidates: GroupMealplanScope[] = [];

    details.forEach(({ group, detail }) => {
      const accessTier = detail?.mealplanPermissionPolicy[group.role];

      if (accessTier === MANAGE || accessTier === VIEW) {
        candidates.push({ kind: 'group', groupId: group.id, groupName: group.name, accessTier });
      }
    });

    const statuses = await mapWithConcurrency(
      candidates,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (scope) => {
        const status = await this.mealplans
          .getGroupMealplanStatus(scope.groupId)
          .catch(() => ({ hasSharedPlan: false }));
        return { scope, hasSharedPlan: status.hasSharedPlan };
      },
    );

    return statuses.filter((status) => status.hasSharedPlan).map((status) => status.scope);
  }
}
