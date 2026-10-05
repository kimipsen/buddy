import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { firstAndLast, sortByName } from '../../../core/array-utils';
import {
  CalendarItemKind,
  CalendarOccurrence,
  CalendarSummary,
  CalendarsService,
} from '../../../core/calendars.service';
import {
  parseIsoDate,
  toIsoDate,
  todayIsoDate,
  toIsoDateInTimeZone,
  toTimeInTimeZone,
} from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import {
  AgendaEntry,
  TaskRun,
  groupTaskRuns,
  isTaskRun,
  occurrenceKey,
} from '../../../core/task-run';
import { UserDatePipe } from '../../../core/user-date.pipe';
import { UsersService } from '../../../core/users.service';
import { createAction } from '../../../shared/action-state/action-state';
import { Toggle } from '../../../shared/toggle/toggle';

const TASK_KIND: CalendarItemKind = 1;
const DAYS_AHEAD = 7;

// Meals only carry a date + MealSlot, never a real clock time -- this mirrors the backend's
// MealSlotDefaultTimes.cs fallback (used there only for the iCal feed) purely so a meal can be
// slotted into the right position among real-timestamped occurrences. Note the chronological
// order (Breakfast, Lunch, Snack, Dinner) differs from the enum's declaration order.
const MEAL_SLOT_SORT_TIME: Record<MealSlot, string> = {
  0: '07:00',
  1: '12:00',
  2: '18:00',
  3: '15:00',
};

const MEAL_SLOT_LABELS: Record<MealSlot, string> = {
  0: 'dashboard.mealplan.slots.breakfast',
  1: 'dashboard.mealplan.slots.lunch',
  2: 'dashboard.mealplan.slots.dinner',
  3: 'dashboard.mealplan.slots.snack',
};

// What one week's load produced: the calendars the child can see (for the filter) plus that
// week's occurrences and meal-plan entries.
interface LoadedWeek {
  myCalendars: CalendarSummary[];
  occurrences: CalendarOccurrence[];
  mealEntries: MealPlanEntry[];
}

interface AgendaDay {
  date: string;
  label: string;
}

// A meal plan entry, wrapped so it can sit alongside AgendaEntry (CalendarOccurrence | TaskRun)
// without changing core/task-run.ts, which is shared with the guardian agenda and knows nothing
// about meals.
interface MealRow {
  meal: MealPlanEntry;
}

type ChildAgendaRow = AgendaEntry | MealRow;

function isMealRow(row: ChildAgendaRow): row is MealRow {
  return 'meal' in row;
}

function buildDays(anchorIsoDate: string, locale: string): AgendaDay[] {
  const anchor = parseIsoDate(anchorIsoDate);

  return Array.from({ length: DAYS_AHEAD }, (_, offset) => {
    const date = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offset);

    return {
      date: toIsoDate(date),
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' }),
    };
  });
}

function instantFor(occurrence: CalendarOccurrence): Date {
  return new Date(occurrence.sortAt);
}

// A wall-clock "HH:mm" sort key comparable across both real-timestamped rows (tasks/events, via
// their instant converted into the viewer's own time zone) and meal rows (which only ever have a
// slot, via MEAL_SLOT_SORT_TIME) -- lets groupedOccurrencesFor sort every row kind in one pass.
function sortKeyFor(row: ChildAgendaRow, timeZoneId: string): string {
  if (isMealRow(row)) {
    return MEAL_SLOT_SORT_TIME[row.meal.slot];
  }

  const occurrence = isTaskRun(row) ? row.subtasks[0] : row;
  return toTimeInTimeZone(instantFor(occurrence), timeZoneId);
}

// Read-only child counterpart to the guardian's CalendarAgenda: same week-window and
// occurrence-grouping shape, but no create/edit/delete -- see
// docs/frontend/analysis/child-calendar-agenda-plan.md for why those are deliberately absent here.
@Component({
  selector: 'app-child-calendar',
  imports: [RouterLink, TranslatePipe, UserDatePipe, Toggle],
  templateUrl: './child-calendar.html',
})
export class ChildCalendar {
  private readonly calendars = inject(CalendarsService);
  private readonly mealplans = inject(MealplansService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  protected readonly taskKind = TASK_KIND;
  protected readonly mealSlotLabels = MEAL_SLOT_LABELS;

  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly days = computed(() =>
    buildDays(this.anchorDate(), this.translation.language()),
  );

  protected readonly week = resource({
    params: () => this.anchorDate(),
    loader: ({ params }) => this.loadWeek(params),
  });
  // The filter keeps showing the last calendars that loaded while another week loads (or fails),
  // rather than disappearing and reappearing on every week change.
  protected readonly myCalendars = linkedSignal<CalendarSummary[] | undefined, CalendarSummary[]>({
    source: () => (this.week.hasValue() ? this.week.value().myCalendars : undefined),
    computation: (next, previous) => next ?? previous?.value ?? [],
  });
  protected readonly hiddenCalendarIds = signal<Set<string>>(new Set());
  protected readonly savingTask = createAction<string>();

  private readonly occurrences = computed(() =>
    this.week.hasValue() ? this.week.value().occurrences : [],
  );
  private readonly mealEntries = computed(() =>
    this.week.hasValue() ? this.week.value().mealEntries : [],
  );

  protected readonly occurrencesByDate = computed(() => {
    const hidden = this.hiddenCalendarIds();
    const byDate: Record<string, CalendarOccurrence[]> = {};

    for (const occurrence of this.occurrences()) {
      if (hidden.has(occurrence.calendarId)) {
        continue;
      }

      const date = toIsoDateInTimeZone(instantFor(occurrence), this.users.timeZoneId());
      const occurrencesForDate = (byDate[date] ??= []);
      occurrencesForDate.push(occurrence);
    }

    for (const dayOccurrences of Object.values(byDate)) {
      dayOccurrences.sort((a, b) => a.sortAt.localeCompare(b.sortAt));
    }

    return byDate;
  });

  protected readonly mealEntriesByDate = computed(() => {
    const byDate: Record<string, MealPlanEntry[]> = {};

    for (const entry of this.mealEntries()) {
      const entriesForDate = (byDate[entry.date] ??= []);
      entriesForDate.push(entry);
    }

    return byDate;
  });

  // Checked against the currently displayed `days()`, not every key `occurrencesByDate()`/
  // `mealEntriesByDate()` happens to hold -- an occurrence can land on a date outside the window
  // once converted into the viewer's time zone, which would otherwise suppress the empty state
  // without any row actually being rendered.
  protected readonly hasAnyVisibleOccurrence = computed(() => {
    const byDate = this.occurrencesByDate();
    const mealsByDate = this.mealEntriesByDate();
    return this.days().some(
      (day) => (byDate[day.date] ?? []).length > 0 || (mealsByDate[day.date] ?? []).length > 0,
    );
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
    // A task-update error belongs to the week it happened in.
    this.savingTask.clearError();
  }

  protected occurrencesFor(date: string): CalendarOccurrence[] {
    return this.occurrencesByDate()[date] ?? [];
  }

  protected mealEntriesFor(date: string): MealPlanEntry[] {
    return this.mealEntriesByDate()[date] ?? [];
  }

  // Folds a day's occurrences and meal-plan entries into one time-ordered list of agenda rows --
  // a template-scheduled task's subtask occurrences (sharing an itemId, each with a routine) render as
  // one bracketed block instead of one row each, and meals are interleaved among tasks/events by
  // wall-clock slot time (see sortKeyFor). Occurrence grouping mirrors the guardian agenda's
  // identical logic (see core/task-run.ts); meals have no backend equivalent to merge with --
  // see docs/frontend/analysis/child-calendar-mealplan-integration.md for why this stays a
  // frontend-only merge.
  protected groupedOccurrencesFor(date: string): ChildAgendaRow[] {
    const rows: ChildAgendaRow[] = [
      ...groupTaskRuns(this.occurrencesFor(date)),
      ...this.mealEntriesFor(date).map((meal): MealRow => ({ meal })),
    ];

    const timeZoneId = this.users.timeZoneId();
    return rows.sort((a, b) => sortKeyFor(a, timeZoneId).localeCompare(sortKeyFor(b, timeZoneId)));
  }

  protected isRun(entry: ChildAgendaRow): entry is TaskRun {
    return !isMealRow(entry) && isTaskRun(entry);
  }

  protected isMeal(entry: ChildAgendaRow): entry is MealRow {
    return isMealRow(entry);
  }

  // Meal rows have no itemId to track by -- date+slot is their natural stable identity (see
  // MealPlanEntry, keyed the same way in child-mealplan.ts).
  protected rowTrackKey(entry: ChildAgendaRow): string {
    return isMealRow(entry) ? `meal:${entry.meal.date}:${entry.meal.slot}` : `item:${entry.itemId}`;
  }

  // Compound key distinguishing sibling subtask occurrences of the same template-scheduled run
  // (same itemId, different subtaskId) -- see core/task-run.ts's occurrenceKey for why itemId
  // alone is no longer sufficient once a run can produce more than one occurrence per item.
  protected keyFor(occurrence: CalendarOccurrence): string {
    return occurrenceKey(occurrence);
  }

  // Mirrors the backend's SetTaskCompletionHandler rejection of future OccurrenceDates -- this is
  // just the UI affordance so a child never sees an actionable checkbox for a day that hasn't
  // arrived yet, not the source of truth.
  protected canCompleteTask(occurrence: CalendarOccurrence): boolean {
    return toIsoDateInTimeZone(instantFor(occurrence), this.users.timeZoneId()) <= todayIsoDate();
  }

  protected isCalendarHidden(calendarId: string): boolean {
    return this.hiddenCalendarIds().has(calendarId);
  }

  protected toggleCalendarVisibility(calendarId: string): void {
    this.hiddenCalendarIds.update((hidden) => {
      const next = new Set(hidden);

      if (next.has(calendarId)) {
        next.delete(calendarId);
      } else {
        next.add(calendarId);
      }

      return next;
    });
  }

  protected async toggleTask(occurrence: CalendarOccurrence): Promise<void> {
    const isCompleted = !occurrence.isCompleted;

    if (isCompleted && !this.canCompleteTask(occurrence)) {
      return;
    }

    const date = toIsoDateInTimeZone(instantFor(occurrence), this.users.timeZoneId());
    const key = occurrenceKey(occurrence);
    const anchorDate = this.anchorDate();

    await this.savingTask.run(
      key,
      async () => {
        await this.calendars.setTaskCompletion(
          occurrence.calendarId,
          occurrence.itemId,
          date,
          isCompleted,
          occurrence.routine?.subtaskId ?? null,
        );
        // If the child moved to another week meanwhile, that week's own load already has fresh
        // data -- writing this one back would cancel it (while loading) or throw (after an error).
        if (this.anchorDate() !== anchorDate || !this.week.hasValue() || this.week.isLoading()) {
          return;
        }

        const current = this.week.value();
        this.week.set({
          ...current,
          occurrences: current.occurrences.map((existing) =>
            occurrenceKey(existing) === key ? { ...existing, isCompleted } : existing,
          ),
        });
      },
      'child.calendar.taskUpdateError',
    );
  }

  private async loadWeek(anchorDate: string): Promise<LoadedWeek> {
    const [first, last] = firstAndLast(buildDays(anchorDate, this.translation.language()));
    const from = first.date;
    const to = last.date;
    const me = await this.users.ensureCurrentUser();

    const [myCalendars, occurrences, mealEntries] = await Promise.all([
      this.calendars.listMyCalendars().then(sortByName),
      this.calendars.listOccurrencesInRange(from, to),
      this.mealplans.listMealPlan({ kind: 'family', childId: me.id }, from, to),
    ]);

    return { myCalendars, occurrences, mealEntries };
  }
}
