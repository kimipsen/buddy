import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toIsoDate, todayIsoDate } from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { UsersService } from '../../../core/users.service';

const SLOT_LABELS: Record<MealSlot, string> = {
  0: 'dashboard.mealplan.slots.breakfast',
  1: 'dashboard.mealplan.slots.lunch',
  2: 'dashboard.mealplan.slots.dinner',
  3: 'dashboard.mealplan.slots.snack'
};

const SLOTS: MealSlot[] = [0, 1, 2, 3];
const DAYS_AHEAD = 7;
const STARS = [1, 2, 3, 4, 5];

interface PlannerDay {
  date: string;
  label: string;
}

// Parsed as local-timezone components rather than `new Date(isoDate)`, matching the same fix in
// the guardian assign-mealplan screen -- an unqualified "YYYY-MM-DD" otherwise parses as UTC
// midnight and can land on the wrong calendar day.
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
  templateUrl: './child-mealplan.html'
})
export class ChildMealplan implements OnInit {
  private readonly mealplans = inject(MealplansService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;
  protected readonly stars = STARS;
  protected readonly anchorDate = signal(defaultAnchor());
  protected readonly days = computed(() => buildDays(this.anchorDate(), this.translation.language()));

  protected readonly entriesByKey = signal<Partial<Record<string, MealPlanEntry>>>({});
  protected readonly hasAnyEntries = computed(() => Object.keys(this.entriesByKey()).length > 0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingKey = signal<string | null>(null);

  // Which entry's rating form is expanded, plus its in-progress comment text -- stars submit
  // immediately on tap (see rate()), but a comment needs an explicit Save so typing doesn't fire a
  // request per keystroke.
  protected readonly editingKey = signal<string | null>(null);
  protected readonly commentDraft = signal('');

  private childId: string | null = null;

  ngOnInit(): void {
    void this.load();
  }

  protected previousWeek(): void {
    this.shiftWeek(-DAYS_AHEAD);
    void this.load();
  }

  protected nextWeek(): void {
    this.shiftWeek(DAYS_AHEAD);
    void this.load();
  }

  private shiftWeek(offsetDays: number): void {
    const anchor = parseIsoDate(this.anchorDate());
    const shifted = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offsetDays);
    this.anchorDate.set(toIsoDate(shifted));
  }

  protected key(date: string, slot: MealSlot): string {
    return `${date}|${slot}`;
  }

  protected entriesForDay(date: string): MealPlanEntry[] {
    return this.slots
      .map((slot) => this.entriesByKey()[this.key(date, slot)])
      .filter((entry): entry is MealPlanEntry => entry !== undefined);
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
    this.commentDraft.set(entry.rating?.comment ?? '');
  }

  protected cancelEditing(): void {
    this.editingKey.set(null);
    this.commentDraft.set('');
  }

  protected setComment(value: string): void {
    this.commentDraft.set(value);
  }

  // Tapping a star rates immediately with whatever comment is already on file -- a quick
  // reaction shouldn't require opening the comment form first.
  protected async rate(entry: MealPlanEntry, starCount: number): Promise<void> {
    await this.submitRating(entry, starCount, entry.rating?.comment ?? null);
  }

  protected async saveComment(entry: MealPlanEntry): Promise<void> {
    const starCount = entry.rating?.stars ?? STARS[STARS.length - 1];
    await this.submitRating(entry, starCount, this.commentDraft().trim() || null);
    this.cancelEditing();
  }

  private async submitRating(entry: MealPlanEntry, starCount: number, comment: string | null): Promise<void> {
    if (!this.childId) {
      return;
    }

    const key = this.key(entry.date, entry.slot);
    this.savingKey.set(key);
    this.error.set(null);

    try {
      const meal = await this.mealplans.rateMeal(this.childId, entry.mealId, starCount, comment);
      const myRating = meal.ratings.find((rating) => rating.childId === this.childId) ?? null;

      this.entriesByKey.update((current) => {
        const next = { ...current };

        for (const [entryKey, existing] of Object.entries(next)) {
          if (existing?.mealId === entry.mealId) {
            next[entryKey] = { ...existing, rating: myRating };
          }
        }

        return next;
      });
    } catch {
      this.error.set('child.mealplan.rateError');
    } finally {
      this.savingKey.set(null);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const me = await this.users.ensureCurrentUser();
      this.childId = me.id;
      const days = this.days();
      const entries = await this.mealplans.listMealPlan({ kind: 'family', childId: me.id }, days[0].date, days.at(-1)!.date);
      const byKey: Partial<Record<string, MealPlanEntry>> = {};

      for (const entry of entries) {
        byKey[this.key(entry.date, entry.slot)] = entry;
      }

      this.entriesByKey.set(byKey);
    } catch {
      this.error.set('child.mealplan.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
