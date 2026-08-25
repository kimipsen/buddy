import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// MealSlot values match the backend's MealSlot enum ordinals (no string enum converter is
// registered server-side): 0 = Breakfast, 1 = Lunch, 2 = Dinner, 3 = Snack. Declaration order
// doubles as display order.
export type MealSlot = 0 | 1 | 2 | 3;

// MealplanAccessTier values match the backend's enum ordinals: 0 = None, 1 = Rate, 2 = Manage,
// 3 = View. None/View/Manage are the three meaningful values for a group's
// MealplanPermissionPolicy -- Rate is the child's own tier and is rejected by the backend if
// submitted here.
export type MealplanAccessTier = 0 | 1 | 2 | 3;

// A family's plan is always addressed by childId; a plan the family has shared with a group is
// additionally reachable by groupId, resolving transparently to the same underlying plan and
// meal library (see docs/backend/analysis/group-owned-mealplans.md). Every read/write method
// below takes a scope instead of a bare childId so callers don't need to know which URL family
// backs a given scope. A group scope carries its resolved accessTier (View or Manage) so
// components can gate write UI without a separate lookup.
export type MealplanScope =
  | { kind: 'family'; childId: string }
  | { kind: 'group'; groupId: string; groupName: string; accessTier: MealplanAccessTier };

export interface MealRating {
  stars: number;
  comment: string | null;
  ratedAt: string;
}

export interface MealPlanEntry {
  date: string;
  slot: MealSlot;
  mealId: string;
  mealName: string;
  icon: string;
  color: string;
  rating: MealRating | null;
  notes: string | null;
  assignedBy: string;
  allRatings: MealRatingSummary[];
}

export interface MealRatingSummary {
  childId: string;
  stars: number;
  comment: string | null;
  ratedAt: string;
}

// A Meal is shared by every child in its family (see MealFamilyResolution) -- it's scoped by
// childId/groupId in the URL only to resolve which family's library to read, not because the
// meal belongs to that child or group.
export interface Meal {
  id: string;
  name: string;
  description: string | null;
  icon: string;
  color: string;
  isArchived: boolean;
  ratings: MealRatingSummary[];
  createdBy: string;
  lastModifiedBy: string;
}

export interface MealDetails {
  name: string;
  description?: string | null;
  icon: string;
  color: string;
}

@Injectable({ providedIn: 'root' })
export class MealplansService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  // Shared across every component reading `meals` (e.g. the meal library editor and the mealplan
  // grid on the same page), so a create/update/archive in one place is reflected everywhere else
  // immediately, without each component needing to know about the others.
  private readonly mealsState = signal<Meal[]>([]);
  readonly meals = this.mealsState.asReadonly();

  private base(scope: MealplanScope): string {
    return scope.kind === 'family'
      ? `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${scope.childId}`
      : `${this.runtimeConfig.apiBaseUrl}/mealplans/groups/${scope.groupId}`;
  }

  listMealPlan(scope: MealplanScope, from: string, to: string): Promise<MealPlanEntry[]> {
    return firstValueFrom(this.http.get<MealPlanEntry[]>(`${this.base(scope)}/plan`, { params: { from, to } }));
  }

  // Always a family-side, child-only action -- only the child themself may rate their own meals
  // (MealplanAuthorization.CheckRate), so this is never called with a group scope.
  rateMeal(childId: string, mealId: string, stars: number, comment?: string | null): Promise<Meal> {
    return firstValueFrom(
      this.http.put<Meal>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/meals/${mealId}/rating`, { stars, comment })
    );
  }

  async listMeals(scope: MealplanScope): Promise<Meal[]> {
    const meals = await firstValueFrom(this.http.get<Meal[]>(`${this.base(scope)}/meals`));
    this.mealsState.set(meals);
    return meals;
  }

  async createMeal(scope: MealplanScope, request: MealDetails): Promise<Meal> {
    const meal = await firstValueFrom(this.http.post<Meal>(`${this.base(scope)}/meals`, request));
    this.mealsState.update((current) => [...current, meal]);
    return meal;
  }

  async updateMealDetails(scope: MealplanScope, mealId: string, request: MealDetails): Promise<Meal> {
    const meal = await firstValueFrom(this.http.patch<Meal>(`${this.base(scope)}/meals/${mealId}/details`, request));
    this.mealsState.update((current) => current.map((existing) => (existing.id === meal.id ? meal : existing)));
    return meal;
  }

  async archiveMeal(scope: MealplanScope, mealId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.base(scope)}/meals/${mealId}`));
    this.mealsState.update((current) => current.filter((meal) => meal.id !== mealId));
  }

  assignMealToSlot(scope: MealplanScope, date: string, slot: MealSlot, mealId: string, notes?: string | null): Promise<MealPlanEntry> {
    return firstValueFrom(
      this.http.put<MealPlanEntry>(`${this.base(scope)}/plan`, { mealId, notes }, { params: { date, slot: String(slot) } })
    );
  }

  clearMealSlot(scope: MealplanScope, date: string, slot: MealSlot): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base(scope)}/plan`, { params: { date, slot: String(slot) } }));
  }

  // Sharing is always a family-side action (only a guardian, via CheckManage, can decide to share
  // or unshare their child's plan) -- these two and getSharedGroup are never scope-based.
  shareWithGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan/groups/${groupId}`, {}));
  }

  unshareFromGroup(childId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan/groups/${groupId}`)
    );
  }

  async getSharedGroup(childId: string): Promise<{ groupId: string; groupName: string } | null> {
    const response = await firstValueFrom(
      this.http.get<{ groupId: string | null; groupName: string | null }>(
        `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan/groups`
      )
    );
    return response.groupId && response.groupName ? { groupId: response.groupId, groupName: response.groupName } : null;
  }
}
