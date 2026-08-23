import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// MealSlot values match the backend's MealSlot enum ordinals (no string enum converter is
// registered server-side): 0 = Breakfast, 1 = Lunch, 2 = Dinner, 3 = Snack. Declaration order
// doubles as display order.
export type MealSlot = 0 | 1 | 2 | 3;

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
}

export interface MealRatingSummary {
  childId: string;
  stars: number;
  comment: string | null;
  ratedAt: string;
}

// A Meal is shared by every child in its family (see MealFamilyResolution) -- it's scoped by
// childId in the URL only because the API needs some child to resolve the family through, not
// because the meal belongs to that child.
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

  listMealPlan(childId: string, from: string, to: string): Promise<MealPlanEntry[]> {
    return firstValueFrom(
      this.http.get<MealPlanEntry[]>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan`, {
        params: { from, to }
      })
    );
  }

  async listMeals(childId: string): Promise<Meal[]> {
    const meals = await firstValueFrom(
      this.http.get<Meal[]>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/meals`)
    );
    this.mealsState.set(meals);
    return meals;
  }

  async createMeal(childId: string, request: MealDetails): Promise<Meal> {
    const meal = await firstValueFrom(
      this.http.post<Meal>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/meals`, request)
    );
    this.mealsState.update((current) => [...current, meal]);
    return meal;
  }

  async updateMealDetails(childId: string, mealId: string, request: MealDetails): Promise<Meal> {
    const meal = await firstValueFrom(
      this.http.patch<Meal>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/meals/${mealId}/details`, request)
    );
    this.mealsState.update((current) => current.map((existing) => (existing.id === meal.id ? meal : existing)));
    return meal;
  }

  async archiveMeal(childId: string, mealId: string): Promise<void> {
    await firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/meals/${mealId}`)
    );
    this.mealsState.update((current) => current.filter((meal) => meal.id !== mealId));
  }

  assignMealToSlot(
    childId: string,
    date: string,
    slot: MealSlot,
    mealId: string,
    notes?: string | null
  ): Promise<MealPlanEntry> {
    return firstValueFrom(
      this.http.put<MealPlanEntry>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan`, { mealId, notes }, {
        params: { date, slot: String(slot) }
      })
    );
  }

  clearMealSlot(childId: string, date: string, slot: MealSlot): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan`, {
        params: { date, slot: String(slot) }
      })
    );
  }
}
