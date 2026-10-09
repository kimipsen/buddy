import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type MealSlot = Schemas['MealSlot'];

export type MealplanAccessTier = Schemas['MealplanAccessTier'];

// A family's plan is always addressed by childId; a plan the family has shared with a group is
// additionally reachable by groupId, resolving transparently to the same underlying plan and
// meal library (see docs/backend/analysis/group-owned-mealplans.md). Every read/write method
// below takes a scope instead of a bare childId so callers don't need to know which URL family
// backs a given scope. A group scope carries its resolved accessTier (View or Manage) so
// components can gate write UI without a separate lookup.
export type MealplanScope =
  | { kind: 'family'; childId: string }
  | { kind: 'group'; groupId: string; groupName: string; accessTier: MealplanAccessTier };

export type MealRating = Schemas['MealRating'];

export type MealPlanEntry = Schemas['MealPlanEntry'];

export type MealRatingSummary = Schemas['MealRatingResponse'];

// A Meal is shared by every child in its family (see MealFamilyResolution) -- it's scoped by
// childId/groupId in the URL only to resolve which family's library to read, not because the
// meal belongs to that child or group.
export type Meal = Schemas['MealResponse'];

export type GroupMealplanStatus = Schemas['GroupMealplanStatusResponse'];

export type MealplanIcalTokenSummary = Schemas['MealPlanIcalTokenSummary'];

// Returned exactly once, at creation -- the plaintext token is never retrievable again after this.
export type IssuedMealplanIcalToken = Schemas['MealPlanIcalTokenResponse'];

export type MealDetails = Schemas['UpdateMealDetailsRequest'];

export type ImportLineKind = Schemas['ImportLineKind'];
export type ImportGroupAction = Schemas['ImportGroupAction'];
export type ImportWeekStart = Schemas['ImportWeekStart'];
export type ImportFormat = 'auto' | 'weekly-note' | 'csv';

export type MealPlanImportPreviewRequest = Schemas['PreviewMealPlanImportRequest'];

export type MealPlanImportPreviewLine = Schemas['MealPlanImportPreviewLine'];

// Every line sharing one normalized meal name. matchedMealId is an exact match in the family's
// library; suggested* is only a "Did you mean ...?" hint (an existing meal or a more frequent
// group) and is never applied without the guardian choosing it.
export type MealPlanImportPreviewGroup = Schemas['MealPlanImportPreviewGroup'];

export type MealPlanImportWarning = Schemas['ImportWarning'];

export type MealPlanImportPreview = Schemas['MealPlanImportPreview'];

// Either an existing mealId or a newMealName; entries with the same new name share one new meal.
export type MealPlanImportEntry = Schemas['CommitMealPlanImportEntryRequest'];

export type MealPlanImportCommitRequest = Schemas['CommitMealPlanImportRequest'];

export type MealPlanImportResult = Schemas['MealPlanImportResult'];

export type MealPlanImportSummary = Schemas['MealPlanImportSummary'];

@Injectable({ providedIn: 'root' })
export class MealplansService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  // Shared across every component reading `meals` (e.g. the meal library editor and the mealplan
  // grid on the same page), so a create/update/archive in one place is reflected everywhere else
  // immediately, without each component needing to know about the others.
  private readonly mealsState = signal<Meal[]>([]);
  readonly meals = computed(() => sortByName(this.mealsState()));

  private base(scope: MealplanScope): string {
    return scope.kind === 'family'
      ? `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${scope.childId}`
      : `${this.runtimeConfig.apiBaseUrl}/mealplans/groups/${scope.groupId}`;
  }

  listMealPlan(scope: MealplanScope, from: string, to: string): Promise<MealPlanEntry[]> {
    return firstValueFrom(
      this.http.get<MealPlanEntry[]>(`${this.base(scope)}/plan`, { params: { from, to } }),
    );
  }

  // Always a family-side, child-only action -- only the child themself may rate their own meals
  // (MealplanAuthorization.CheckRate), so this is never called with a group scope.
  rateMeal(childId: string, mealId: string, stars: number, comment: string): Promise<Meal> {
    return firstValueFrom(
      this.http.put<Meal>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/meals/${mealId}/rating`,
        { stars, comment },
      ),
    );
  }

  async listMeals(scope: MealplanScope): Promise<Meal[]> {
    const meals = await firstValueFrom(this.http.get<Meal[]>(`${this.base(scope)}/meals`));
    this.mealsState.set(meals);
    return sortByName(meals);
  }

  async createMeal(scope: MealplanScope, request: MealDetails): Promise<Meal> {
    const meal = await firstValueFrom(
      postIdempotent<Meal>(this.http, `${this.base(scope)}/meals`, request),
    );
    this.mealsState.update((current) => [...current, meal]);
    return meal;
  }

  async updateMealDetails(
    scope: MealplanScope,
    mealId: string,
    request: MealDetails,
  ): Promise<Meal> {
    const meal = await firstValueFrom(
      this.http.patch<Meal>(`${this.base(scope)}/meals/${mealId}/details`, request),
    );
    this.mealsState.update((current) =>
      current.map((existing) => (existing.id === meal.id ? meal : existing)),
    );
    return meal;
  }

  async archiveMeal(scope: MealplanScope, mealId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.base(scope)}/meals/${mealId}`));
    this.mealsState.update((current) => current.filter((meal) => meal.id !== mealId));
  }

  assignMealToSlot(
    scope: MealplanScope,
    date: string,
    slot: MealSlot,
    mealId: string,
    notes: string,
  ): Promise<MealPlanEntry> {
    return firstValueFrom(
      this.http.put<MealPlanEntry>(
        `${this.base(scope)}/plan`,
        { mealId, notes },
        { params: { date, slot } },
      ),
    );
  }

  clearMealSlot(scope: MealplanScope, date: string, slot: MealSlot): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base(scope)}/plan`, { params: { date, slot } }),
    );
  }

  // Sharing is always a family-side action (only a guardian, via CheckManage, can decide to share
  // or unshare their child's plan) -- these two and getSharedGroup are never scope-based.
  shareWithGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan/groups/${groupId}`,
        {},
      ),
    );
  }

  unshareFromGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan/groups/${groupId}`,
      ),
    );
  }

  // 200 with the group, or 204 (an empty body, so null) when it isn't shared.
  getSharedGroup(childId: string): Promise<{ groupId: string; groupName: string } | null> {
    return firstValueFrom(
      this.http.get<{ groupId: string; groupName: string } | null>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan/groups`,
      ),
    );
  }

  getGroupMealplanStatus(groupId: string): Promise<GroupMealplanStatus> {
    return firstValueFrom(
      this.http.get<GroupMealplanStatus>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/groups/${groupId}/status`,
      ),
    );
  }

  // iCal subscription tokens are always a family-side action (MealplanAuthorization.CheckManage
  // on the childId, same gate as sharing) -- there's no group-scope equivalent, since a group
  // scope carries no childId for this route to key off.
  listIcalTokens(childId: string): Promise<MealplanIcalTokenSummary[]> {
    return firstValueFrom(
      this.http.get<MealplanIcalTokenSummary[]>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/ical-tokens`,
      ),
    );
  }

  createIcalToken(childId: string): Promise<IssuedMealplanIcalToken> {
    return firstValueFrom(
      postIdempotent<IssuedMealplanIcalToken>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/ical-tokens`,
        {},
      ),
    );
  }

  revokeIcalToken(childId: string, tokenId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/ical-tokens/${tokenId}`,
      ),
    );
  }

  // Parses and matches the text against the family's meal library; writes nothing (a POST only
  // because the text is too long for a query string).
  previewImport(
    scope: MealplanScope,
    request: MealPlanImportPreviewRequest,
  ): Promise<MealPlanImportPreview> {
    return firstValueFrom(
      this.http.post<MealPlanImportPreview>(`${this.base(scope)}/imports/preview`, request),
    );
  }

  commitImport(
    scope: MealplanScope,
    request: MealPlanImportCommitRequest,
  ): Promise<MealPlanImportResult> {
    return firstValueFrom(
      postIdempotent<MealPlanImportResult>(this.http, `${this.base(scope)}/imports`, request),
    );
  }

  listImports(scope: MealplanScope): Promise<MealPlanImportSummary[]> {
    return firstValueFrom(this.http.get<MealPlanImportSummary[]>(`${this.base(scope)}/imports`));
  }

  revertImport(scope: MealplanScope, importId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base(scope)}/imports/${importId}`));
  }

  // subscriptionPath is relative (e.g. "/mealplans/{mealPlanId}/ical/{token}") -- prefix with
  // apiBaseUrl to get the URL a calendar app can subscribe to.
  icalFeedUrl(subscriptionPath: string): string {
    return `${this.runtimeConfig.apiBaseUrl}${subscriptionPath}`;
  }
}
