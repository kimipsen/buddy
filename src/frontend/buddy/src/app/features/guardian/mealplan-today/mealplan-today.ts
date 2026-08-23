import { Component, OnInit, inject, signal } from '@angular/core';

import { GuardiansService } from '../../../core/guardians.service';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';

const SLOT_LABELS: Record<MealSlot, string> = {
  0: 'Breakfast',
  1: 'Lunch',
  2: 'Dinner',
  3: 'Snack'
};

const SLOTS: MealSlot[] = [0, 1, 2, 3];

function todayIsoDate(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

@Component({
  selector: 'app-mealplan-today',
  templateUrl: './mealplan-today.html'
})
export class MealplanToday implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly mealplans = inject(MealplansService);

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;

  protected readonly entriesBySlot = signal<Partial<Record<MealSlot, MealPlanEntry>>>({});
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasChildren = signal(true);

  ngOnInit(): void {
    void this.loadPlan();
  }

  private async loadPlan(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);

      const today = todayIsoDate();
      const entries = await this.mealplans.listMealPlan(children[0].id, today, today);
      const bySlot: Partial<Record<MealSlot, MealPlanEntry>> = {};

      for (const entry of entries) {
        bySlot[entry.slot] = entry;
      }

      this.entriesBySlot.set(bySlot);
    } catch {
      this.error.set('Unable to load today’s meal plan.');
    } finally {
      this.loading.set(false);
    }
  }
}
