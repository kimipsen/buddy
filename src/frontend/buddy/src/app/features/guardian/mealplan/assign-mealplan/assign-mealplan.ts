import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDragPreview, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';

import { toIsoDate, todayIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { MealPlanEntry, MealplanAccessTier, MealplanScope, MealSlot, MealplansService } from '../../../../core/mealplans.service';
import { MealPicker } from '../meal-picker/meal-picker';

const SLOT_LABELS: Record<MealSlot, string> = {
  0: 'mealplan.slots.breakfast',
  1: 'mealplan.slots.lunch',
  2: 'mealplan.slots.dinner',
  3: 'mealplan.slots.snack'
};

const SLOTS: MealSlot[] = [0, 1, 2, 3];
const DAYS_AHEAD = 7;
const MANAGE: MealplanAccessTier = 2;

interface PlannerDay {
  date: string;
  label: string;
}

interface SlotRef {
  date: string;
  slot: MealSlot;
}

interface NamedRating {
  childName: string;
  stars: number;
  comment: string | null;
}

// Parsed as local-timezone components rather than `new Date(isoDate)` -- the latter parses an
// unqualified "YYYY-MM-DD" as UTC midnight, which can land on the wrong calendar day once
// formatted back in a timezone behind UTC.
function parseIsoDate(isoDate: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function buildDays(anchorIsoDate: string, locale: string): PlannerDay[] {
  const anchor = parseIsoDate(anchorIsoDate);

  return Array.from({ length: DAYS_AHEAD }, (_, offset) => {
    const date = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offset);

    return {
      date: toIsoDate(date),
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' })
    };
  });
}

@Component({
  selector: 'app-assign-mealplan',
  imports: [MealPicker, TranslatePipe, CdkDrag, CdkDragHandle, CdkDragPreview, CdkDropList, CdkDropListGroup],
  templateUrl: './assign-mealplan.html'
})
export class AssignMealplan implements OnInit {
  private readonly mealplans = inject(MealplansService);
  private readonly guardians = inject(GuardiansService);
  private readonly translation = inject(TranslationService);

  readonly scope = input.required<MealplanScope>();

  // A group scope with a View (not Manage) tier is read-only -- the backend rejects every write
  // with 403 regardless, but the UI disables those controls rather than letting the user hit them.
  protected readonly readOnly = computed(() => {
    const scope = this.scope();
    return scope.kind === 'group' && scope.accessTier !== MANAGE;
  });

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;
  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly days = computed(() => buildDays(this.anchorDate(), this.translation.language()));

  // Reads straight from the shared service state, so adding a meal in the meal library on the
  // same page shows up here immediately without a manual refetch.
  protected readonly meals = computed(() => this.mealplans.meals().filter((meal) => !meal.isArchived));
  protected readonly entriesByKey = signal<Partial<Record<string, MealPlanEntry>>>({});
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingKey = signal<string | null>(null);

  // "My children" resolves independently of which scope is currently selected -- it's used only
  // to label sibling ratings by name, not to determine which plan is being viewed. Loaded once.
  private readonly childNamesById = signal<Record<string, string>>({});

  constructor() {
    effect(() => {
      // Read anchorDate() here (not just inside load()) so the effect re-runs when the visible
      // week changes, not only when the scope does.
      this.anchorDate();
      void this.load(this.scope());
    });
  }

  ngOnInit(): void {
    void this.loadChildNames();
  }

  private async loadChildNames(): Promise<void> {
    try {
      const children: ChildSummary[] = await this.guardians.listMyChildren();
      this.childNamesById.set(Object.fromEntries(children.map((child) => [child.id, child.name.givenName])));
    } catch {
      // Sibling names are a nice-to-have on the historical ratings view -- if this fails, ratings
      // still render (see ratingsFor), just without a resolvable name.
    }
  }

  protected previousWeek(): void {
    this.shiftWeek(-DAYS_AHEAD);
  }

  protected nextWeek(): void {
    this.shiftWeek(DAYS_AHEAD);
  }

  private shiftWeek(offsetDays: number): void {
    const anchor = parseIsoDate(this.anchorDate());
    const shifted = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offsetDays);
    this.anchorDate.set(toIsoDate(shifted));
  }

  protected key(date: string, slot: MealSlot): string {
    return `${date}|${slot}`;
  }

  // A day that has already happened is a record of what was actually planned, not something to
  // keep editing -- the write controls are disabled for it regardless of the scope's access tier.
  protected isPastDay(date: string): boolean {
    return date < todayIsoDate();
  }

  protected mealIdFor(date: string, slot: MealSlot): string {
    return this.entriesByKey()[this.key(date, slot)]?.mealId ?? '';
  }

  protected entryFor(date: string, slot: MealSlot): MealPlanEntry | undefined {
    return this.entriesByKey()[this.key(date, slot)];
  }

  protected starDisplay(stars: number): string {
    return '★'.repeat(stars) + '☆'.repeat(5 - stars);
  }

  // Only meaningful for a past day in the family's own scope -- a group-shared plan is viewed by
  // people outside that family, who have no "my children" list to resolve names against, so it
  // falls back to showing nothing extra there rather than an unresolved id.
  protected ratingsFor(date: string, slot: MealSlot): NamedRating[] {
    if (this.scope().kind !== 'family' || !this.isPastDay(date)) {
      return [];
    }

    const entry = this.entryFor(date, slot);

    if (!entry || entry.allRatings.length === 0) {
      return [];
    }

    const names = this.childNamesById();

    return entry.allRatings.map((rating) => ({
      childName: names[rating.childId] ?? rating.childId,
      stars: rating.stars,
      comment: rating.comment
    }));
  }

  protected async onSlotChange(date: string, slot: MealSlot, mealId: string): Promise<void> {
    if (this.readOnly() || this.isPastDay(date)) {
      return;
    }

    const scope = this.scope();
    const key = this.key(date, slot);
    this.savingKey.set(key);
    this.error.set(null);

    try {
      if (mealId) {
        const entry = await this.mealplans.assignMealToSlot(scope, date, slot, mealId);
        this.entriesByKey.update((current) => ({ ...current, [key]: entry }));
      } else {
        await this.mealplans.clearMealSlot(scope, date, slot);
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
    if (this.readOnly()) {
      return;
    }

    const scope = this.scope();
    const source = event.item.data as SlotRef;
    const target = event.container.data;

    if (source.date === target.date && source.slot === target.slot) {
      return;
    }

    if (this.isPastDay(source.date) || this.isPastDay(target.date)) {
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
        // Sequential, not Promise.all: both writes land on the same plan's single event stream,
        // and appending to it concurrently from two requests causes contention.
        const targetEntry = await this.mealplans.assignMealToSlot(scope, target.date, target.slot, sourceMealId);
        const sourceEntry = await this.mealplans.assignMealToSlot(scope, source.date, source.slot, targetMealId);
        this.entriesByKey.update((current) => ({ ...current, [targetKey]: targetEntry, [sourceKey]: sourceEntry }));
      } else {
        const targetEntry = await this.mealplans.assignMealToSlot(scope, target.date, target.slot, sourceMealId);
        await this.mealplans.clearMealSlot(scope, source.date, source.slot);
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

  private async load(scope: MealplanScope): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.entriesByKey.set({});

    try {
      const [, entries] = await Promise.all([
        this.mealplans.listMeals(scope),
        this.mealplans.listMealPlan(scope, this.days()[0].date, this.days().at(-1)!.date)
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
