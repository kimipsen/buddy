import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CalendarItemKind, CalendarOccurrence, CalendarSummary, CalendarsService } from '../../../core/calendars.service';
import { toIsoDate, todayIsoDate, toIsoDateInTimeZone } from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { UserDatePipe } from '../../../core/user-date.pipe';
import { UsersService } from '../../../core/users.service';

const EVENT_KIND: CalendarItemKind = 0;
const TASK_KIND: CalendarItemKind = 1;
const DAYS_AHEAD = 7;

interface AgendaDay {
  date: string;
  label: string;
}

// Parsed as local-timezone components rather than `new Date(isoDate)` -- the latter parses an
// unqualified "YYYY-MM-DD" as UTC midnight, which can land on the wrong calendar day once
// formatted back in a timezone behind UTC. Mirrors the guardian agenda's identical helper.
function parseIsoDate(isoDate: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function buildDays(anchorIsoDate: string, locale: string): AgendaDay[] {
  const anchor = parseIsoDate(anchorIsoDate);

  return Array.from({ length: DAYS_AHEAD }, (_, offset) => {
    const date = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offset);

    return {
      date: toIsoDate(date),
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' })
    };
  });
}

// Exactly one of startsAt/dueAt is ever set per the backend's Event-vs-Task invariant.
function instantFor(occurrence: CalendarOccurrence): Date | null {
  const value = occurrence.startsAt ?? occurrence.dueAt;
  return value ? new Date(value) : null;
}

// Read-only child counterpart to the guardian's CalendarAgenda: same week-window and
// occurrence-grouping shape, but no create/edit/delete -- see
// docs/frontend/analysis/child-calendar-agenda-plan.md for why those are deliberately absent here.
@Component({
  selector: 'app-child-calendar',
  imports: [RouterLink, TranslatePipe, UserDatePipe],
  templateUrl: './child-calendar.html'
})
export class ChildCalendar {
  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  protected readonly eventKind = EVENT_KIND;
  protected readonly taskKind = TASK_KIND;

  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly days = computed(() => buildDays(this.anchorDate(), this.translation.language()));

  protected readonly myCalendars = signal<CalendarSummary[]>([]);
  protected readonly occurrences = signal<CalendarOccurrence[]>([]);
  protected readonly hiddenCalendarIds = signal<Set<string>>(new Set());
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
      (byDate[date] ??= []).push(occurrence);
    }

    for (const dayOccurrences of Object.values(byDate)) {
      dayOccurrences.sort((a, b) => (a.startsAt ?? a.dueAt ?? '').localeCompare(b.startsAt ?? b.dueAt ?? ''));
    }

    return byDate;
  });

  // Checked against the currently displayed `days()`, not every key `occurrencesByDate()` happens
  // to hold -- `occurrences()` can briefly retain items from an out-of-range fetch (e.g. stale data
  // while navigating between weeks), which would otherwise suppress the empty state without any
  // occurrence actually being rendered.
  protected readonly hasAnyVisibleOccurrence = computed(() => {
    const byDate = this.occurrencesByDate();
    return this.days().some((day) => (byDate[day.date] ?? []).length > 0);
  });

  constructor() {
    effect(() => {
      // Read anchorDate() here (not just inside loadWeek()) so the effect re-runs when the
      // visible week changes.
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
    const shifted = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + offsetDays);
    this.anchorDate.set(toIsoDate(shifted));
  }

  protected occurrencesFor(date: string): CalendarOccurrence[] {
    return this.occurrencesByDate()[date] ?? [];
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

    this.savingTaskId.set(occurrence.itemId);

    try {
      await this.calendars.setTaskCompletion(occurrence.calendarId, occurrence.itemId, date, isCompleted);
      this.occurrences.update((current) =>
        current.map((existing) => (existing.itemId === occurrence.itemId ? { ...existing, isCompleted } : existing))
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
      const from = this.days()[0].date;
      const to = this.days().at(-1)!.date;

      const [myCalendars, occurrences] = await Promise.all([
        this.calendars.listMyCalendars(),
        this.calendars.listOccurrencesInRange(from, to)
      ]);

      this.myCalendars.set(myCalendars);
      this.occurrences.set(occurrences);
    } catch {
      this.error.set('child.calendar.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
