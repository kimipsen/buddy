import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDragPreview, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { toIsoDate } from '../../../../core/date-utils';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../../core/mealplans.service';
import { MealPicker } from '../meal-picker/meal-picker';

const SLOT_LABELS: Record<MealSlot, string> = {
  0: 'mealplan.slots.breakfast',
  1: 'mealplan.slots.lunch',
  2: 'mealplan.slots.dinner',
  3: 'mealplan.slots.snack'
};

const SLOTS: MealSlot[] = [0, 1, 2, 3];
const DAYS_AHEAD = 7;

interface PlannerDay {
  date: string;
  label: string;
}

interface SlotRef {
  date: string;
  slot: MealSlot;
}

function buildDays(): PlannerDay[] {
  const today = new Date();

  return Array.from({ length: DAYS_AHEAD }, (_, offset) => {
    const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() + offset);

    return {
      date: toIsoDate(date),
      label: date.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })
    };
  });
}

@Component({
  selector: 'app-assign-mealplan',
  imports: [MealPicker, TranslatePipe, CdkDrag, CdkDragHandle, CdkDragPreview, CdkDropList, CdkDropListGroup],
  templateUrl: './assign-mealplan.html'
})
export class AssignMealplan implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly mealplans = inject(MealplansService);

  private childId: string | null = null;

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;
  protected readonly days = buildDays();

  protected readonly hasChildren = signal(true);
  // Reads straight from the shared service state, so adding a meal in the meal library on the
  // same page shows up here immediately without a manual refetch.
  protected readonly meals = computed(() => this.mealplans.meals().filter((meal) => !meal.isArchived));
  protected readonly entriesByKey = signal<Partial<Record<string, MealPlanEntry>>>({});
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingKey = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected key(date: string, slot: MealSlot): string {
    return `${date}|${slot}`;
  }

  protected mealIdFor(date: string, slot: MealSlot): string {
    return this.entriesByKey()[this.key(date, slot)]?.mealId ?? '';
  }

  protected entryFor(date: string, slot: MealSlot): MealPlanEntry | undefined {
    return this.entriesByKey()[this.key(date, slot)];
  }

  protected async onSlotChange(date: string, slot: MealSlot, mealId: string): Promise<void> {
    if (!this.childId) {
      return;
    }

    const key = this.key(date, slot);
    this.savingKey.set(key);
    this.error.set(null);

    try {
      if (mealId) {
        const entry = await this.mealplans.assignMealToSlot(this.childId, date, slot, mealId);
        this.entriesByKey.update((current) => ({ ...current, [key]: entry }));
      } else {
        await this.mealplans.clearMealSlot(this.childId, date, slot);
        this.entriesByKey.update((current) => {
          const next = { ...current };
          delete next[key];
          return next;
        });
      }
    } catch {
      this.error.set('mealplan.assign.updateError');
    } finally {
      this.savingKey.set(null);
    }
  }

  // Dragging a meal onto an empty cell moves it; dragging it onto an occupied cell swaps the two,
  // since "move this to another day" and "switch these two around" are the same gesture to a user.
  protected async onMealDrop(event: CdkDragDrop<SlotRef>): Promise<void> {
    if (!this.childId) {
      return;
    }

    const source = event.item.data as SlotRef;
    const target = event.container.data;

    if (source.date === target.date && source.slot === target.slot) {
      return;
    }

    const sourceMealId = this.mealIdFor(source.date, source.slot);

    if (!sourceMealId) {
      return;
    }

    const targetMealId = this.mealIdFor(target.date, target.slot);
    const sourceKey = this.key(source.date, source.slot);
    const targetKey = this.key(target.date, target.slot);

    this.savingKey.set(sourceKey);
    this.error.set(null);

    try {
      if (targetMealId) {
        // Sequential, not Promise.all: both writes land on the same child's single mealplan
        // event stream, and appending to it concurrently from two requests causes contention.
        const targetEntry = await this.mealplans.assignMealToSlot(this.childId, target.date, target.slot, sourceMealId);
        const sourceEntry = await this.mealplans.assignMealToSlot(this.childId, source.date, source.slot, targetMealId);
        this.entriesByKey.update((current) => ({ ...current, [targetKey]: targetEntry, [sourceKey]: sourceEntry }));
      } else {
        const targetEntry = await this.mealplans.assignMealToSlot(this.childId, target.date, target.slot, sourceMealId);
        await this.mealplans.clearMealSlot(this.childId, source.date, source.slot);
        this.entriesByKey.update((current) => {
          const next = { ...current, [targetKey]: targetEntry };
          delete next[sourceKey];
          return next;
        });
      }
    } catch {
      this.error.set('mealplan.assign.updateError');
    } finally {
      this.savingKey.set(null);
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
      this.childId = children[0].id;

      const [, entries] = await Promise.all([
        this.mealplans.listMeals(this.childId),
        this.mealplans.listMealPlan(this.childId, this.days[0].date, this.days[this.days.length - 1].date)
      ]);

      const byKey: Partial<Record<string, MealPlanEntry>> = {};

      for (const entry of entries) {
        byKey[this.key(entry.date, entry.slot)] = entry;
      }

      this.entriesByKey.set(byKey);
    } catch {
      this.error.set('mealplan.assign.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
