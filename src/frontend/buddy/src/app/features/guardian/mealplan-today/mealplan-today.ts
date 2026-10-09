import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';

import { todayIsoDate } from '../../../core/date-utils';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { Card } from '../../../shared/card/card';

const SLOT_LABELS: Record<MealSlot, string> = {
  Breakfast: 'dashboard.mealplan.slots.breakfast',
  Lunch: 'dashboard.mealplan.slots.lunch',
  Dinner: 'dashboard.mealplan.slots.dinner',
  Snack: 'dashboard.mealplan.slots.snack',
};

const SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

// What the widget loaded: whether the guardian has children at all, and the first child's meals
// for today keyed by slot.
interface LoadedPlan {
  hasChildren: boolean;
  entriesBySlot: Partial<Record<MealSlot, MealPlanEntry>>;
}

@Component({
  selector: 'app-mealplan-today',
  imports: [RouterLink, TranslatePipe, LoadingSpinner, Card],
  templateUrl: './mealplan-today.html',
})
export class MealplanToday {
  private readonly guardians = inject(GuardiansService);
  private readonly mealplans = inject(MealplansService);

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;

  protected readonly plan = resource({ loader: () => this.loadPlan() });

  private async loadPlan(): Promise<LoadedPlan> {
    const children = await this.guardians.listMyChildren();
    const [firstChild] = children;

    if (!firstChild) {
      return { hasChildren: false, entriesBySlot: {} };
    }

    const today = todayIsoDate();
    const entries = await this.mealplans.listMealPlan(
      { kind: 'family', childId: firstChild.id },
      today,
      today,
    );
    const entriesBySlot: Partial<Record<MealSlot, MealPlanEntry>> = {};

    for (const entry of entries) {
      entriesBySlot[entry.slot] = entry;
    }

    return { hasChildren: true, entriesBySlot };
  }
}
