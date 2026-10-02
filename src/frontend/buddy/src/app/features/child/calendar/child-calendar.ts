import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { firstAndLast } from '../../../core/array-utils';
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
import { Toggle } from '../../../shared/toggle/toggle';

const EVENT_KIND: CalendarItemKind = 0;
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

// Exactly one of startsAt/dueAt is ever set per the backend's Event-vs-Task invariant.
function instantFor(occurrence: CalendarOccurrence): Date | null {
  const value = occurrence.startsAt ?? occurrence.dueAt;
  return value ? new Date(value) : null;
}

// A wall-clock "HH:mm" sort key comparable across both real-timestamped rows (tasks/events, via
// their instant converted into the viewer's own time zone) and meal rows (which only ever have a
// slot, via MEAL_SLOT_SORT_TIME) -- lets groupedOccurrencesFor sort every row kind in one pass.
function sortKeyFor(row: ChildAgendaRow, timeZoneId: string): string {
  if (isMealRow(row)) {
    return MEAL_SLOT_SORT_TIME[row.meal.slot];
  }

  const occurrence = isTaskRun(row) ? row.subtasks[0] : row;
  const instant = instantFor(occurrence);
  // Stryker disable next-line StringLiteral: unreachable -- occurrencesByDate drops every occurrence without a startsAt/dueAt, so instant is never null here
  return instant ? toTimeInTimeZone(instant, timeZoneId) : '';
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

  protected readonly eventKind = EVENT_KIND;
  protected readonly taskKind = TASK_KIND;
  protected readonly mealSlotLabels = MEAL_SLOT_LABELS;

  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly days = computed(() =>
    buildDays(this.anchorDate(), this.translation.language()),
  );

  protected readonly myCalendars = signal<CalendarSummary[]>([]);
  protected readonly occurrences = signal<CalendarOccurrence[]>([]);
  protected readonly mealEntries = signal<MealPlanEntry[]>([]);
  protected readonly hiddenCalendarIds = signal<Set<string>>(new Set());
  // Stryker disable next-line BooleanLiteral: the constructor's effect calls loadWeek(), which sets loading(true), before the template first renders
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingTaskId = signal<string | null>(null);

  protected readonly occurrencesByDate = computed(() => {
    const hidden = this.hiddenCalendarIds();
    const byDate: Record<string, CalendarOccurrence[]> = {};

    for (const occurrence of this.occurrences()) {
      if (hidden.has(occurrence.calendarId)) {
        continue;
      }

      const instant = instantFor(occurrence);

      if (!instant) {
        continue;
      }

      const date = toIsoDateInTimeZone(instant, this.users.timeZoneId());
      const occurrencesForDate = (byDate[date] ??= []);
      occurrencesForDate.push(occurrence);
    }

    for (const dayOccurrences of Object.values(byDate)) {
      // Stryker disable next-line StringLiteral: unreachable -- only occurrences with a startsAt or dueAt are grouped above
      dayOccurrences.sort((a, b) =>
        (a.startsAt ?? a.dueAt ?? '').localeCompare(b.startsAt ?? b.dueAt ?? ''),
      );
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
  // `mealEntriesByDate()` happens to hold -- both signals can briefly retain items from an
  // out-of-range fetch (e.g. stale data while navigating between weeks), which would otherwise
  // suppress the empty state without any row actually being rendered.
  protected readonly hasAnyVisibleOccurrence = computed(() => {
    const byDate = this.occurrencesByDate();
    const mealsByDate = this.mealEntriesByDate();
    return this.days().some(
      (day) => (byDate[day.date] ?? []).length > 0 || (mealsByDate[day.date] ?? []).length > 0,
    );
  });

  constructor() {
    effect(() => {
      // Read anchorDate() here (not just inside loadWeek()) so the effect re-runs when the
      // visible week changes.
      // Stryker disable next-line CallExpression: redundant with loadWeek() reading days() (and so anchorDate()) synchronously before its first await
      this.anchorDate();
      void this.loadWeek();
    });
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

  protected occurrencesFor(date: string): CalendarOccurrence[] {
    return this.occurrencesByDate()[date] ?? [];
  }

  protected mealEntriesFor(date: string): MealPlanEntry[] {
    return this.mealEntriesByDate()[date] ?? [];
  }

  // Folds a day's occurrences and meal-plan entries into one time-ordered list of agenda rows --
  // a template-scheduled task's subtask occurrences (sharing an itemId + parentTitle) render as
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
    const instant = instantFor(occurrence);
    return !!instant && toIsoDateInTimeZone(instant, this.users.timeZoneId()) <= todayIsoDate();
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
    const instant = instantFor(occurrence);

    if (!instant) {
      return;
    }

    const isCompleted = !occurrence.isCompleted;

    if (isCompleted && !this.canCompleteTask(occurrence)) {
      return;
    }

    const date = toIsoDateInTimeZone(instant, this.users.timeZoneId());
    const key = occurrenceKey(occurrence);

    this.savingTaskId.set(key);

    try {
      await this.calendars.setTaskCompletion(
        occurrence.calendarId,
        occurrence.itemId,
        date,
        isCompleted,
        occurrence.subtaskId ?? null,
      );
      this.occurrences.update((current) =>
        current.map((existing) =>
          occurrenceKey(existing) === key ? { ...existing, isCompleted } : existing,
        ),
      );
    } catch {
      this.error.set('child.calendar.taskUpdateError');
    } finally {
      this.savingTaskId.set(null);
    }
  }

  private async loadWeek(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const [first, last] = firstAndLast(this.days());
      const from = first.date;
      const to = last.date;
      const me = await this.users.ensureCurrentUser();

      const [myCalendars, occurrences, mealEntries] = await Promise.all([
        this.calendars.listMyCalendars(),
        this.calendars.listOccurrencesInRange(from, to),
        this.mealplans.listMealPlan({ kind: 'family', childId: me.id }, from, to),
      ]);

      this.myCalendars.set(myCalendars);
      this.occurrences.set(occurrences);
      this.mealEntries.set(mealEntries);
    } catch {
      this.error.set('child.calendar.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
