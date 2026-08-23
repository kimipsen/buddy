import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
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

@Injectable({ providedIn: 'root' })
export class MealplansService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listMealPlan(childId: string, from: string, to: string): Promise<MealPlanEntry[]> {
    return firstValueFrom(
      this.http.get<MealPlanEntry[]>(`${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}/plan`, {
        params: { from, to }
      })
    );
  }
}
