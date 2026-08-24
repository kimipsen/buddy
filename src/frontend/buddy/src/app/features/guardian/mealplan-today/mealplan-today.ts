import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { todayIsoDate } from '../../../core/date-utils';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';

const SLOT_LABELS: Record<MealSlot, string> = {
  0: 'dashboard.mealplan.slots.breakfast',
  1: 'dashboard.mealplan.slots.lunch',
  2: 'dashboard.mealplan.slots.dinner',
  3: 'dashboard.mealplan.slots.snack'
};

const SLOTS: MealSlot[] = [0, 1, 2, 3];

@Component({
  selector: 'app-mealplan-today',
  imports: [RouterLink, TranslatePipe],
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
      this.error.set('dashboard.mealplan.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
