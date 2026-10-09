import {
  Component,
  computed,
  inject,
  linkedSignal,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { firstAndLast } from '../../../core/array-utils';
import { parseIsoDate, toIsoDate, todayIsoDate } from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { UsersService } from '../../../core/users.service';
import { createAction } from '../../../shared/action-state/action-state';

const SLOT_LABELS: Record<MealSlot, string> = {
  Breakfast: 'dashboard.mealplan.slots.breakfast',
  Lunch: 'dashboard.mealplan.slots.lunch',
  Dinner: 'dashboard.mealplan.slots.dinner',
  Snack: 'dashboard.mealplan.slots.snack',
};

const SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
const DAYS_AHEAD = 7;
const MAX_STARS = 5;
const STARS = Array.from({ length: MAX_STARS }, (_, index) => index + 1);

// What one week's load produced: the signed-in child (whose ratings this screen shows and
// submits) and that week's entries keyed by date and slot.
interface LoadedWeek {
  childId: string;
  entriesByKey: Partial<Record<string, MealPlanEntry>>;
}

interface PlannerDay {
  date: string;
  label: string;
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

// The screen opens one week back rather than on today's forward week -- a child opening "my
// meals" wants to see (and rate) what they already ate, not an empty upcoming week.
function defaultAnchor(): string {
  const today = parseIsoDate(todayIsoDate());
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate() - DAYS_AHEAD);
  return toIsoDate(start);
}

@Component({
  selector: 'app-child-mealplan',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './child-mealplan.html',
})
export class ChildMealplan {
  private readonly mealplans = inject(MealplansService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;
  protected readonly stars = STARS;
  protected readonly anchorDate = signal(defaultAnchor());
  protected readonly days = computed(() =>
    buildDays(this.anchorDate(), this.translation.language()),
  );

  protected readonly week = resource({
    params: () => this.anchorDate(),
    loader: ({ params }) => this.load(params),
  });
  protected readonly hasAnyEntries = computed(
    () => this.week.hasValue() && Object.keys(this.week.value().entriesByKey).length > 0,
  );
  protected readonly rating = createAction<string>();

  // Which entry's rating form is expanded, plus its in-progress comment text -- stars submit
  // immediately on tap (see rate()), but a comment needs an explicit Save so typing doesn't fire a
  // request per keystroke. The draft starts from the entry's comment on file each time a form
  // opens; a rating saved while it's open doesn't overwrite what's being typed.
  protected readonly editingKey = signal<string | null>(null);
  protected readonly commentDraft = linkedSignal(() => {
    const key = this.editingKey();
    return key === null ? '' : (untracked(() => this.entryAt(key))?.rating?.comment ?? '');
  });

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
    // A rating error belongs to the week it happened in.
    this.rating.clearError();
  }

  protected key(date: string, slot: MealSlot): string {
    return `${date}|${slot}`;
  }

  protected entriesForDay(date: string): MealPlanEntry[] {
    return this.slots
      .map((slot) => this.entryAt(this.key(date, slot)))
      .filter((entry): entry is MealPlanEntry => entry !== undefined);
  }

  private entryAt(key: string): MealPlanEntry | undefined {
    return this.week.hasValue() ? this.week.value().entriesByKey[key] : undefined;
  }

  // Nothing to rate before it's actually been served.
  protected canRate(entry: MealPlanEntry): boolean {
    return entry.date <= todayIsoDate();
  }

  protected isEditing(entry: MealPlanEntry): boolean {
    return this.editingKey() === this.key(entry.date, entry.slot);
  }

  protected startEditing(entry: MealPlanEntry): void {
    this.editingKey.set(this.key(entry.date, entry.slot));
  }

  protected cancelEditing(): void {
    this.editingKey.set(null);
  }

  protected setComment(value: string): void {
    this.commentDraft.set(value);
  }

  // Tapping a star rates immediately with whatever comment is already on file -- a quick
  // reaction shouldn't require opening the comment form first.
  protected async rate(entry: MealPlanEntry, starCount: number): Promise<void> {
    await this.submitRating(entry, starCount, entry.rating?.comment ?? '');
  }

  protected async saveComment(entry: MealPlanEntry): Promise<void> {
    const starCount = entry.rating?.stars ?? MAX_STARS;
    await this.submitRating(entry, starCount, this.commentDraft().trim());
    this.cancelEditing();
  }

  private async submitRating(
    entry: MealPlanEntry,
    starCount: number,
    comment: string,
  ): Promise<void> {
    if (!this.week.hasValue()) {
      return;
    }

    const { childId } = this.week.value();
    const key = this.key(entry.date, entry.slot);
    const anchorDate = this.anchorDate();

    await this.rating.run(
      key,
      async () => {
        const meal = await this.mealplans.rateMeal(childId, entry.mealId, starCount, comment);
        const myRating = meal.ratings.find((rating) => rating.childId === childId) ?? null;

        // If the child moved to another week meanwhile, that week's own load already has fresh
        // data -- writing this one back would cancel it (while loading) or throw (after an error).
        if (this.anchorDate() !== anchorDate || !this.week.hasValue() || this.week.isLoading()) {
          return;
        }

        const current = this.week.value();
        const entriesByKey = { ...current.entriesByKey };

        for (const [entryKey, existing] of Object.entries(entriesByKey)) {
          if (existing?.mealId === entry.mealId) {
            entriesByKey[entryKey] = { ...existing, rating: myRating };
          }
        }

        this.week.set({ ...current, entriesByKey });
      },
      'child.mealplan.rateError',
    );
  }

  private async load(anchorDate: string): Promise<LoadedWeek> {
    const me = await this.users.ensureCurrentUser();
    const [first, last] = firstAndLast(buildDays(anchorDate, this.translation.language()));
    const entries = await this.mealplans.listMealPlan(
      { kind: 'family', childId: me.id },
      first.date,
      last.date,
    );
    const entriesByKey: Partial<Record<string, MealPlanEntry>> = {};

    for (const entry of entries) {
      entriesByKey[this.key(entry.date, entry.slot)] = entry;
    }

    return { childId: me.id, entriesByKey };
  }
}
