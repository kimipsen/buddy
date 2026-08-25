import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  CalendarItemKind,
  CalendarOccurrence,
  CalendarSummary,
  CalendarsService,
  DatePart,
  RecurrenceFrequency,
  RecurrenceRuleRequest
} from '../../../../core/calendars.service';
import { toIsoDate, todayIsoDate, toIsoDateInTimeZone } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { UsersService } from '../../../../core/users.service';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { TimeSelect } from '../../../../shared/time-select/time-select';

const DAYS_AHEAD = 7;
const EVENT_KIND: CalendarItemKind = 0;
const TASK_KIND: CalendarItemKind = 1;
// Owner (0) or Contributor (1) -- the same tiers CalendarAuthorization.CheckContribute accepts.
const MAX_CONTRIBUTE_ROLE = 1;
const DEFAULT_ICON = '📅';
const DEFAULT_COLOR = '#f43f5e';

interface AgendaDay {
  date: string;
  label: string;
}

// Parsed as local-timezone components rather than `new Date(isoDate)` -- the latter parses an
// unqualified "YYYY-MM-DD" as UTC midnight, which can land on the wrong calendar day once
// formatted back in a timezone behind UTC. Mirrors assign-mealplan.ts's identical helper.
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

function toDatePart(date: string, time: string): DatePart {
  return { date, time: `${time}:00` };
}

@Component({
  selector: 'app-calendar-agenda',
  imports: [FormsModule, TranslatePipe, UserDatePipe, TimeSelect],
  templateUrl: './agenda.html'
})
export class CalendarAgenda {
  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  protected readonly eventKind = EVENT_KIND;
  protected readonly taskKind = TASK_KIND;

  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly days = computed(() => buildDays(this.anchorDate(), this.translation.language()));

  protected readonly myCalendars = signal<CalendarSummary[]>([]);
  protected readonly eligibleCalendars = computed(() => this.myCalendars().filter((calendar) => calendar.role <= MAX_CONTRIBUTE_ROLE));

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

  protected readonly hasAnyVisibleOccurrence = computed(() => Object.values(this.occurrencesByDate()).some((list) => list.length > 0));

  protected readonly newCalendarId = signal('');
  protected readonly newKind = signal<CalendarItemKind>(EVENT_KIND);
  protected readonly newTitle = signal('');
  protected readonly newIcon = signal(DEFAULT_ICON);
  protected readonly newColor = signal(DEFAULT_COLOR);
  protected readonly newStartDate = signal(todayIsoDate());
  protected readonly newStartTime = signal('09:00');
  protected readonly newEndDate = signal(todayIsoDate());
  protected readonly newEndTime = signal('10:00');
  protected readonly newDueDate = signal(todayIsoDate());
  protected readonly newDueTime = signal('09:00');
  protected readonly newRepeat = signal<RecurrenceFrequency | null>(null);
  protected readonly newIntervalCount = signal(1);
  protected readonly newUntil = signal('');
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly canSubmit = computed(() => {
    if (!this.newCalendarId() || !this.newTitle().trim() || !this.newIcon().trim() || !this.newColor().trim()) {
      return false;
    }

    return this.newKind() === EVENT_KIND
      ? this.newStartDate().trim() !== '' && this.newEndDate().trim() !== ''
      : this.newDueDate().trim() !== '';
  });

  constructor() {
    effect(() => {
      // Read anchorDate() here (not just inside loadWeek()) so the effect re-runs when the
      // visible week changes -- same pattern as assign-mealplan.ts.
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

  protected async toggleTaskCompletion(occurrence: CalendarOccurrence): Promise<void> {
    const instant = instantFor(occurrence);

    if (!instant) {
      return;
    }

    const date = toIsoDateInTimeZone(instant, this.users.timeZoneId());
    const isCompleted = !occurrence.isCompleted;

    this.savingTaskId.set(occurrence.itemId);

    try {
      await this.calendars.setTaskCompletion(occurrence.calendarId, occurrence.itemId, date, isCompleted);
      this.occurrences.update((current) =>
        current.map((existing) => (existing.itemId === occurrence.itemId ? { ...existing, isCompleted } : existing))
      );
    } catch {
      this.error.set('calendar.agenda.taskUpdateError');
    } finally {
      this.savingTaskId.set(null);
    }
  }

  protected async createItem(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }

    const calendarId = this.newCalendarId();
    const kind = this.newKind();

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.calendars.createItem(calendarId, {
        kind,
        title: this.newTitle().trim(),
        icon: this.newIcon().trim(),
        color: this.newColor().trim(),
        startsAt: kind === EVENT_KIND ? toDatePart(this.newStartDate(), this.newStartTime()) : null,
        endsAt: kind === EVENT_KIND ? toDatePart(this.newEndDate(), this.newEndTime()) : null,
        dueDate: kind === TASK_KIND ? toDatePart(this.newDueDate(), this.newDueTime()) : null,
        recurrence: this.buildRecurrence()
      });

      this.resetForm();
      await this.loadWeek();
    } catch {
      this.createError.set('calendar.agenda.form.createError');
    } finally {
      this.creating.set(false);
    }
  }

  private buildRecurrence(): RecurrenceRuleRequest | null {
    const frequency = this.newRepeat();

    if (frequency === null) {
      return null;
    }

    return {
      frequency,
      intervalCount: this.newIntervalCount(),
      until: this.newUntil().trim() || null
    };
  }

  private resetForm(): void {
    this.newTitle.set('');
    this.newIcon.set(DEFAULT_ICON);
    this.newColor.set(DEFAULT_COLOR);
    this.newStartDate.set(todayIsoDate());
    this.newStartTime.set('09:00');
    this.newEndDate.set(todayIsoDate());
    this.newEndTime.set('10:00');
    this.newDueDate.set(todayIsoDate());
    this.newDueTime.set('09:00');
    this.newRepeat.set(null);
    this.newIntervalCount.set(1);
    this.newUntil.set('');
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

      if (!this.newCalendarId()) {
        const firstEligible = myCalendars.find((calendar) => calendar.role <= MAX_CONTRIBUTE_ROLE);

        if (firstEligible) {
          this.newCalendarId.set(firstEligible.id);
        }
      }
    } catch {
      this.error.set('calendar.agenda.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
