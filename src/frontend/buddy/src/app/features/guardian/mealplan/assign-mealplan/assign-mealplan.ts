import {
  CdkDrag,
  CdkDragDrop,
  CdkDragHandle,
  CdkDragPreview,
  CdkDropList,
  CdkDropListGroup,
} from '@angular/cdk/drag-drop';
import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, input, resource, signal } from '@angular/core';

import { firstAndLast } from '../../../../core/array-utils';
import { parseIsoDate, toIsoDate, todayIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  Meal,
  MealPlanEntry,
  MealplanAccessTier,
  MealplanScope,
  MealSlot,
  MealplansService,
} from '../../../../core/mealplans.service';
import { ActionState, createAction } from '../../../../shared/action-state/action-state';
import { MealPicker } from '../meal-picker/meal-picker';
import { Card } from '../../../../shared/card/card';

const SLOT_LABELS: Record<MealSlot, string> = {
  Breakfast: 'mealplan.slots.breakfast',
  Lunch: 'mealplan.slots.lunch',
  Dinner: 'mealplan.slots.dinner',
  Snack: 'mealplan.slots.snack',
};

const SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
const DAYS_AHEAD = 7;
const MANAGE: MealplanAccessTier = 'Manage';

interface PlannerDay {
  date: string;
  label: string;
}

interface SlotRef {
  date: string;
  slot: MealSlot;
}

type EntriesByKey = Partial<Record<string, MealPlanEntry>>;

interface EntriesRequest {
  scope: MealplanScope;
  from: string;
  to: string;
}

function scopeId(scope: MealplanScope): string {
  return scope.kind === 'family' ? `family:${scope.childId}` : `group:${scope.groupId}`;
}

interface NamedRating {
  childName: string;
  stars: number;
  comment: string | null;
}

function buildDays(anchorIsoDate: string, locale: string): PlannerDay[] {
  const anchor = parseIsoDate(anchorIsoDate);

  return Array.from({ length: DAYS_AHEAD }, (_, offset) => {
    const date = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offset);

    return {
      date: toIsoDate(date),
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' }),
    };
  });
}

@Component({
  selector: 'app-assign-mealplan',
  imports: [
    MealPicker,
    NgTemplateOutlet,
    TranslatePipe,
    CdkDrag,
    CdkDragHandle,
    CdkDragPreview,
    CdkDropList,
    CdkDropListGroup,
    Card,
  ],
  templateUrl: './assign-mealplan.html',
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
  protected readonly days = computed(() =>
    buildDays(this.anchorDate(), this.translation.language()),
  );

  // Reads straight from the shared service state, so adding a meal in the meal library on the
  // same page shows up here immediately without a manual refetch.
  protected readonly meals = computed<Meal[]>(() =>
    this.mealplans.meals().filter((meal) => !meal.isArchived),
  );
  // Ids are `${scopeId}|${date}|${slot}`, so a save only ever shows against its own scope.
  protected readonly saving = createAction<string>();
  protected readonly scopeSaveState = computed((): ActionState<string> => {
    const state = this.saving.state();
    return state.status === 'idle' || state.id.startsWith(`${scopeId(this.scope())}|`)
      ? state
      : { status: 'idle' };
  });

  // What the entries are fetched for. Compared by value, so relabelling days() (a language switch)
  // doesn't refetch; a save compares it by identity to tell whether its scope and week still show.
  private readonly request = computed(
    (): EntriesRequest => {
      const [first, last] = firstAndLast(this.days());
      return { scope: this.scope(), from: first.date, to: last.date };
    },
    {
      equal: (a, b) => scopeId(a.scope) === scopeId(b.scope) && a.from === b.from && a.to === b.to,
    },
  );

  // The visible week's entries for the scope. Reloads when the scope or the week changes, which also
  // drops a save error left over from the previous view.
  protected readonly entries = resource({
    params: () => this.request(),
    loader: ({ params }) => {
      this.saving.clearError();
      return this.loadEntries(params.scope, params.from, params.to);
    },
  });
  // Empty while loading or after a failed load, so the grid still renders.
  protected readonly entriesByKey = computed<EntriesByKey>(() =>
    this.entries.hasValue() ? this.entries.value() : {},
  );
  // "My children" resolves independently of which scope is currently selected -- it's used only
  // to label sibling ratings by name, not to determine which plan is being viewed. Loaded once.
  private readonly childNamesById = signal<Record<string, string>>({});

  ngOnInit(): void {
    void this.loadChildNames();
  }

  private async loadChildNames(): Promise<void> {
    try {
      const children: ChildSummary[] = await this.guardians.listMyChildren();
      this.childNamesById.set(
        Object.fromEntries(children.map((child) => [child.id, child.name.givenName])),
      );
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
    const shifted = new Date(
      anchor.getFullYear(),
      anchor.getMonth(),
      anchor.getDate() + offsetDays,
    );
    this.anchorDate.set(toIsoDate(shifted));
  }

  protected key(date: string, slot: MealSlot): string {
    return `${date}|${slot}`;
  }

  protected saveId(date: string, slot: MealSlot): string {
    return `${scopeId(this.scope())}|${this.key(date, slot)}`;
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
      comment: rating.comment,
    }));
  }

  protected async onSlotChange(date: string, slot: MealSlot, mealId: string): Promise<void> {
    if (this.readOnly() || this.isPastDay(date)) {
      return;
    }

    const shown = this.request();
    const scope = shown.scope;
    const key = this.key(date, slot);

    await this.saving.run(
      `${scopeId(scope)}|${key}`,
      async () => {
        if (mealId) {
          const entry = await this.mealplans.assignMealToSlot(scope, date, slot, mealId, '');
          this.updateEntries(shown, (current) => ({ ...current, [key]: entry }));
        } else {
          await this.mealplans.clearMealSlot(scope, date, slot);
          this.updateEntries(shown, (current) => {
            const next = { ...current };
            delete next[key];
            return next;
          });
        }
      },
      'mealplan.assign.updateError',
    );
  }

  // Dragging a meal onto an empty cell moves it; dragging it onto an occupied cell swaps the two,
  // since "move this to another day" and "switch these two around" are the same gesture to a user.
  protected async onMealDrop(event: CdkDragDrop<SlotRef>): Promise<void> {
    if (this.readOnly()) {
      return;
    }

    const shown = this.request();
    const scope = shown.scope;
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

    await this.saving.run(
      `${scopeId(scope)}|${sourceKey}`,
      async () => {
        if (targetMealId) {
          // Sequential, not Promise.all: both writes land on the same plan's single event stream,
          // and appending to it concurrently from two requests causes contention.
          const targetEntry = await this.mealplans.assignMealToSlot(
            scope,
            target.date,
            target.slot,
            sourceMealId,
            '',
          );
          const sourceEntry = await this.mealplans.assignMealToSlot(
            scope,
            source.date,
            source.slot,
            targetMealId,
            '',
          );
          this.updateEntries(shown, (current) => ({
            ...current,
            [targetKey]: targetEntry,
            [sourceKey]: sourceEntry,
          }));
        } else {
          const targetEntry = await this.mealplans.assignMealToSlot(
            scope,
            target.date,
            target.slot,
            sourceMealId,
            '',
          );
          await this.mealplans.clearMealSlot(scope, source.date, source.slot);
          this.updateEntries(shown, (current) => {
            const next = { ...current, [targetKey]: targetEntry };
            delete next[sourceKey];
            return next;
          });
        }
      },
      'mealplan.assign.updateError',
    );
  }

  // Applies a save's result only if the grid still shows the scope and week it was made for, and
  // those entries have loaded: setting the resource mid-load would cancel the load.
  private updateEntries(
    shown: EntriesRequest,
    change: (current: EntriesByKey) => EntriesByKey,
  ): void {
    const entries = this.entries;

    if (this.request() === shown && entries.hasValue() && !entries.isLoading()) {
      entries.set(change(entries.value()));
    }
  }

  private async loadEntries(scope: MealplanScope, from: string, to: string): Promise<EntriesByKey> {
    const [, entries] = await Promise.all([
      this.mealplans.listMeals(scope),
      this.mealplans.listMealPlan(scope, from, to),
    ]);

    const byKey: EntriesByKey = {};

    for (const entry of entries) {
      byKey[this.key(entry.date, entry.slot)] = entry;
    }

    return byKey;
  }
}
