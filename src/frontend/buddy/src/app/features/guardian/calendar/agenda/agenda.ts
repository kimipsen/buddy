import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AssignableMember,
  CalendarItemKind,
  CalendarOccurrence,
  CalendarSummary,
  CalendarsService,
  DatePart,
  RecurrenceFrequency,
  RecurrenceRuleRequest
} from '../../../../core/calendars.service';
import {
  addDaysIso,
  buildDateRangeIso,
  buildMonthGridIso,
  parseIsoDate,
  shiftMonthIso,
  startOfWeekIso,
  toIsoDateInTimeZone,
  toTimeInTimeZone,
  todayIsoDate
} from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { UsersService } from '../../../../core/users.service';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { TimeSelect } from '../../../../shared/time-select/time-select';
import { MonthGrid } from './month-grid/month-grid';

const DAYS_AHEAD = 7;
const EVENT_KIND: CalendarItemKind = 0;
const TASK_KIND: CalendarItemKind = 1;
// Owner (0) or Contributor (1) -- the same tiers CalendarAuthorization.CheckContribute accepts.
const MAX_CONTRIBUTE_ROLE = 1;
const DEFAULT_COLOR = '#f43f5e';

export interface AgendaDay {
  date: string;
  label: string;
  // Only meaningful for month-grid days -- undefined everywhere else.
  isCurrentMonth?: boolean;
}

export type ViewMode = 'day' | 'workweek' | 'week' | 'month';

const WORKWEEK_DAYS = 5;

function labeledDay(isoDate: string, locale: string): AgendaDay {
  return {
    date: isoDate,
    label: parseIsoDate(isoDate).toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' })
  };
}

function buildLabeledDays(startIsoDate: string, dayCount: number, locale: string): AgendaDay[] {
  return buildDateRangeIso(startIsoDate, dayCount).map((date) => labeledDay(date, locale));
}

// Rolling window from the anchor date, unchanged from the screen's original (and still default)
// behavior -- deliberately not Monday-aligned, see docs/frontend/analysis/
// guardian-full-calendar-views.md's "Scope decision" for why Week keeps this instead of matching
// Work week/Month's calendar-aligned windowing.
function buildDays(anchorIsoDate: string, locale: string): AgendaDay[] {
  return buildLabeledDays(anchorIsoDate, DAYS_AHEAD, locale);
}

function buildMonthDays(anchorIsoDate: string, locale: string): AgendaDay[] {
  const anchorMonth = parseIsoDate(anchorIsoDate).getMonth();

  return buildMonthGridIso(anchorIsoDate).map((date) => ({
    date,
    label: String(parseIsoDate(date).getDate()),
    isCurrentMonth: parseIsoDate(date).getMonth() === anchorMonth
  }));
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
  imports: [FormsModule, TranslatePipe, UserDatePipe, DateSelect, TimeSelect, MonthGrid],
  templateUrl: './agenda.html'
})
export class CalendarAgenda {
  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  protected readonly eventKind = EVENT_KIND;
  protected readonly taskKind = TASK_KIND;
  protected readonly today = todayIsoDate();

  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly viewMode = signal<ViewMode>('week');
  protected readonly viewModes: readonly ViewMode[] = ['day', 'workweek', 'week', 'month'];

  // The rendered day list for Day/Work week/Week, and the full month grid (including
  // leading/trailing days from adjacent months) for Month -- also the sole source of the fetch
  // range in loadWeek() below, which just reads the first/last date here regardless of mode.
  protected readonly days = computed(() => {
    const anchor = this.anchorDate();
    const locale = this.translation.language();

    switch (this.viewMode()) {
      case 'day':
        return buildLabeledDays(anchor, 1, locale);
      case 'workweek':
        return buildLabeledDays(startOfWeekIso(anchor), WORKWEEK_DAYS, locale);
      case 'week':
        return buildDays(anchor, locale);
      case 'month':
        return buildMonthDays(anchor, locale);
    }
  });

  // Mon-Sun headers for the month grid, computed once here since MonthGrid has no locale of its
  // own -- the first seven days() entries in month mode are always a complete Monday-start week.
  protected readonly weekdayLabels = computed(() => {
    if (this.viewMode() !== 'month') {
      return [];
    }

    const locale = this.translation.language();
    return this.days()
      .slice(0, 7)
      .map((day) => parseIsoDate(day.date).toLocaleDateString(locale, { weekday: 'short' }));
  });

  protected readonly viewTitle = computed(() => {
    switch (this.viewMode()) {
      case 'day':
        return this.translation.translate('calendar.agenda.dayTitle', {
          date: parseIsoDate(this.anchorDate()).toLocaleDateString(this.translation.language(), {
            weekday: 'long',
            month: 'long',
            day: 'numeric'
          })
        });
      case 'workweek':
        return this.translation.translate('calendar.agenda.workweekTitle');
      case 'week':
        // Unchanged from the screen's original single view -- see the "Scope decision" note in
        // docs/frontend/analysis/guardian-full-calendar-views.md.
        return this.translation.translate('calendar.agenda.title');
      case 'month':
        return this.translation.translate('calendar.agenda.monthTitle', {
          month: parseIsoDate(this.anchorDate()).toLocaleDateString(this.translation.language(), {
            month: 'long',
            year: 'numeric'
          })
        });
    }
  });

  protected readonly previousLabelKey = computed(
    () => ({ day: 'calendar.agenda.previousDay', workweek: 'calendar.agenda.previousWeek', week: 'calendar.agenda.previousWeek', month: 'calendar.agenda.previousMonth' })[this.viewMode()]
  );

  protected readonly nextLabelKey = computed(
    () => ({ day: 'calendar.agenda.nextDay', workweek: 'calendar.agenda.nextWeek', week: 'calendar.agenda.nextWeek', month: 'calendar.agenda.nextMonth' })[this.viewMode()]
  );

  protected readonly myCalendars = signal<CalendarSummary[]>([]);
  protected readonly eligibleCalendars = computed(() => this.myCalendars().filter((calendar) => calendar.role <= MAX_CONTRIBUTE_ROLE));

  protected readonly occurrences = signal<CalendarOccurrence[]>([]);
  protected readonly hiddenCalendarIds = signal<Set<string>>(new Set());
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingTaskId = signal<string | null>(null);
  protected readonly confirmingDeleteItemId = signal<string | null>(null);
  protected readonly deletingItemId = signal<string | null>(null);

  protected readonly editingItemId = signal<string | null>(null);
  protected readonly editTitle = signal('');
  protected readonly editIcon = signal('');
  protected readonly editColor = signal('');
  protected readonly editStartDate = signal('');
  protected readonly editStartTime = signal('');
  protected readonly editEndDate = signal('');
  protected readonly editEndTime = signal('');
  protected readonly editDueDate = signal('');
  protected readonly editDueTime = signal('');
  protected readonly editIsAllDay = signal(false);
  protected readonly editingKind = signal<CalendarItemKind>(EVENT_KIND);
  protected readonly saving = signal(false);
  protected readonly editError = signal<string | null>(null);

  protected readonly canSubmitEdit = computed(() => {
    if (!this.editTitle().trim() || !this.editColor().trim()) {
      return false;
    }

    return this.editingKind() === EVENT_KIND
      ? this.editStartDate().trim() !== '' && this.editEndDate().trim() !== ''
      : this.editDueDate().trim() !== '';
  });

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

  protected readonly newCalendarId = signal('');
  protected readonly newKind = signal<CalendarItemKind>(EVENT_KIND);
  protected readonly newTitle = signal('');
  // Empty means "inherit the selected calendar's icon" -- see calendarIconFor().
  protected readonly newIcon = signal('');
  protected readonly newColor = signal(DEFAULT_COLOR);
  protected readonly newStartDate = signal(todayIsoDate());
  protected readonly newStartTime = signal('09:00');
  protected readonly newEndDate = signal(todayIsoDate());
  protected readonly newEndTime = signal('10:00');
  protected readonly newDueDate = signal(todayIsoDate());
  protected readonly newDueTime = signal('09:00');
  protected readonly newIsAllDay = signal(false);
  protected readonly newRepeat = signal<RecurrenceFrequency | null>(null);
  protected readonly newIntervalCount = signal(1);
  protected readonly newUntil = signal('');
  protected readonly newAssignedTo = signal('');
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly assignableMembers = signal<AssignableMember[]>([]);
  // Merged across every calendar the guardian has assigned members for, keyed by userId -- used to
  // label an occurrence's assignee in the agenda list, not just the picker on the create form.
  private readonly memberNamesById = signal<Record<string, string>>({});

  protected readonly canSubmit = computed(() => {
    if (!this.newCalendarId() || !this.newTitle().trim() || !this.newColor().trim()) {
      return false;
    }

    return this.newKind() === EVENT_KIND
      ? this.newStartDate().trim() !== '' && this.newEndDate().trim() !== ''
      : this.newDueDate().trim() !== '';
  });

  constructor() {
    effect(() => {
      // Read anchorDate()/viewMode() here (not just inside loadWeek()) so the effect re-runs
      // whenever the visible range changes, in either dimension -- same pattern as
      // assign-mealplan.ts.
      this.anchorDate();
      this.viewMode();
      void this.loadWeek();
    });

    effect(() => {
      // The assignable set is per-calendar (group membership differs by calendar), so a previous
      // selection may no longer be valid once the calendar changes -- clear it here rather than
      // in resetForm(), which only runs after a successful submit.
      const calendarId = this.newCalendarId();
      this.newAssignedTo.set('');
      void this.loadAssignableMembers(calendarId);
    });
  }

  protected setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
  }

  protected viewModeLabelKey(mode: ViewMode): string {
    return { day: 'calendar.agenda.view.day', workweek: 'calendar.agenda.view.workweek', week: 'calendar.agenda.view.week', month: 'calendar.agenda.view.month' }[mode];
  }

  protected goToToday(): void {
    this.anchorDate.set(todayIsoDate());
  }

  protected previousPeriod(): void {
    this.shiftPeriod(-1);
  }

  protected nextPeriod(): void {
    this.shiftPeriod(1);
  }

  private shiftPeriod(direction: 1 | -1): void {
    switch (this.viewMode()) {
      case 'day':
        this.anchorDate.set(addDaysIso(this.anchorDate(), direction));
        return;
      case 'workweek':
        this.anchorDate.set(addDaysIso(this.anchorDate(), direction * 7));
        return;
      case 'week':
        // Unchanged from the screen's original single view -- shifts the rolling window by
        // exactly DAYS_AHEAD, same as the original shiftWeek(±DAYS_AHEAD).
        this.anchorDate.set(addDaysIso(this.anchorDate(), direction * DAYS_AHEAD));
        return;
      case 'month':
        this.anchorDate.set(shiftMonthIso(this.anchorDate(), direction));
        return;
    }
  }

  protected occurrencesFor(date: string): CalendarOccurrence[] {
    return this.occurrencesByDate()[date] ?? [];
  }

  // Month view is overview/navigation only (see the "Rendering" section in docs/frontend/analysis/
  // guardian-full-calendar-views.md) -- picking a day there always drills into Day view for it.
  protected onMonthDaySelected(date: string): void {
    this.anchorDate.set(date);
    this.viewMode.set('day');
  }

  // What a blank icon input resolves to -- shown as its placeholder so leaving it empty visibly
  // means "use the calendar's icon". Falls back to the backend's own default (Calendar.DefaultIcon)
  // for the brief window before myCalendars() has loaded.
  protected calendarIconFor(calendarId: string): string {
    return this.myCalendars().find((calendar) => calendar.id === calendarId)?.icon ?? '📅';
  }

  // Best-effort: resolves an assignee's name for display in the agenda list. Falls back to null
  // (rendered as nothing) for an occurrence whose calendar the guardian can only view, since
  // listAssignableMembers -- and so this name -- is only fetched for calendars they contribute to.
  protected assigneeNameFor(userId: string | null): string | null {
    return userId ? (this.memberNamesById()[userId] ?? null) : null;
  }

  private async loadAssignableMembers(calendarId: string): Promise<void> {
    if (!calendarId) {
      this.assignableMembers.set([]);
      return;
    }

    try {
      const members = await this.calendars.listAssignableMembers(calendarId);
      this.assignableMembers.set(members);
      this.memberNamesById.update((current) => ({
        ...current,
        ...Object.fromEntries(members.map((member) => [member.userId, `${member.givenName} ${member.familyName}`.trim()]))
      }));
    } catch {
      // The assignee picker is a nice-to-have on the create form -- if this fails, task creation
      // still works, just without the option to assign it to someone.
      this.assignableMembers.set([]);
    }
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

  // Mirrors the backend's SetTaskCompletionHandler rejection of future OccurrenceDates -- this is
  // just the UI affordance so a guardian never sees an actionable checkbox for a day that hasn't
  // arrived yet, not the source of truth.
  protected canCompleteTask(occurrence: CalendarOccurrence): boolean {
    const instant = instantFor(occurrence);
    return !!instant && toIsoDateInTimeZone(instant, this.users.timeZoneId()) <= todayIsoDate();
  }

  protected async toggleTaskCompletion(occurrence: CalendarOccurrence): Promise<void> {
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
      this.error.set('calendar.agenda.taskUpdateError');
    } finally {
      this.savingTaskId.set(null);
    }
  }

  protected requestDeleteItem(itemId: string): void {
    this.error.set(null);
    this.editingItemId.set(null);
    this.confirmingDeleteItemId.set(itemId);
  }

  protected cancelDeleteItem(): void {
    this.confirmingDeleteItemId.set(null);
  }

  protected async confirmDeleteItem(occurrence: CalendarOccurrence): Promise<void> {
    this.deletingItemId.set(occurrence.itemId);
    this.error.set(null);

    try {
      await this.calendars.deleteItem(occurrence.calendarId, occurrence.itemId);
      this.confirmingDeleteItemId.set(null);
      this.occurrences.update((current) => current.filter((existing) => existing.itemId !== occurrence.itemId));
    } catch {
      this.error.set('calendar.agenda.delete.error');
    } finally {
      this.deletingItemId.set(null);
    }
  }

  protected startEditItem(occurrence: CalendarOccurrence): void {
    this.error.set(null);
    this.confirmingDeleteItemId.set(null);
    this.editError.set(null);
    this.editingItemId.set(occurrence.itemId);
    this.editingKind.set(occurrence.kind);
    this.editTitle.set(occurrence.title);
    this.editIcon.set(occurrence.iconOverride ?? '');
    this.editColor.set(occurrence.color);
    this.editIsAllDay.set(occurrence.isAllDay);

    const timeZoneId = this.users.timeZoneId();

    if (occurrence.startsAt) {
      const startsAt = new Date(occurrence.startsAt);
      this.editStartDate.set(toIsoDateInTimeZone(startsAt, timeZoneId));
      this.editStartTime.set(toTimeInTimeZone(startsAt, timeZoneId));
    }

    if (occurrence.endsAt) {
      const endsAt = new Date(occurrence.endsAt);
      const endDate = toIsoDateInTimeZone(endsAt, timeZoneId);
      // Stored EndsAt is exclusive for an all-day event -- show the last inclusive day instead.
      this.editEndDate.set(occurrence.isAllDay ? addDaysIso(endDate, -1) : endDate);
      this.editEndTime.set(toTimeInTimeZone(endsAt, timeZoneId));
    }

    if (occurrence.dueAt) {
      const dueAt = new Date(occurrence.dueAt);
      this.editDueDate.set(toIsoDateInTimeZone(dueAt, timeZoneId));
      this.editDueTime.set(toTimeInTimeZone(dueAt, timeZoneId));
    }
  }

  protected cancelEditItem(): void {
    this.editingItemId.set(null);
  }

  // A recurring item's schedule is anchored on the item itself (see StartsAt.cs), not per
  // occurrence -- rescheduling here shifts the whole series, matching RescheduleItemHandler.
  protected async saveEditItem(occurrence: CalendarOccurrence): Promise<void> {
    if (!this.canSubmitEdit()) {
      return;
    }

    const kind = this.editingKind();
    const title = this.editTitle().trim();
    const icon = this.editIcon().trim();
    const color = this.editColor().trim();
    const isAllDay = this.editIsAllDay();

    this.saving.set(true);
    this.editError.set(null);

    // The end date shown/entered is inclusive for an all-day event -- store it exclusive.
    const startTime = isAllDay ? '00:00' : this.editStartTime();
    const endTime = isAllDay ? '00:00' : this.editEndTime();
    const endDate = isAllDay ? addDaysIso(this.editEndDate(), 1) : this.editEndDate();
    const dueTime = isAllDay ? '00:00' : this.editDueTime();

    try {
      await this.calendars.updateItemDetails(occurrence.calendarId, occurrence.itemId, { title, icon: icon || null, color });
      await this.calendars.rescheduleItem(occurrence.calendarId, occurrence.itemId, {
        startsAt: kind === EVENT_KIND ? toDatePart(this.editStartDate(), startTime) : null,
        endsAt: kind === EVENT_KIND ? toDatePart(endDate, endTime) : null,
        dueDate: kind === TASK_KIND ? toDatePart(this.editDueDate(), dueTime) : null,
        isAllDay
      });

      this.editingItemId.set(null);
      await this.loadWeek();
    } catch {
      this.editError.set('calendar.agenda.edit.error');
    } finally {
      this.saving.set(false);
    }
  }

  protected async createItem(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }

    const calendarId = this.newCalendarId();
    const kind = this.newKind();
    const isAllDay = this.newIsAllDay();

    this.creating.set(true);
    this.createError.set(null);

    // The end date shown/entered is inclusive for an all-day event -- store it exclusive.
    const startTime = isAllDay ? '00:00' : this.newStartTime();
    const endTime = isAllDay ? '00:00' : this.newEndTime();
    const endDate = isAllDay ? addDaysIso(this.newEndDate(), 1) : this.newEndDate();
    const dueTime = isAllDay ? '00:00' : this.newDueTime();

    try {
      await this.calendars.createItem(calendarId, {
        kind,
        title: this.newTitle().trim(),
        icon: this.newIcon().trim() || null,
        color: this.newColor().trim(),
        startsAt: kind === EVENT_KIND ? toDatePart(this.newStartDate(), startTime) : null,
        endsAt: kind === EVENT_KIND ? toDatePart(endDate, endTime) : null,
        dueDate: kind === TASK_KIND ? toDatePart(this.newDueDate(), dueTime) : null,
        isAllDay,
        recurrence: this.buildRecurrence(),
        assignedTo: kind === TASK_KIND && this.newAssignedTo() ? this.newAssignedTo() : null
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
    this.newIcon.set('');
    this.newColor.set(DEFAULT_COLOR);
    this.newStartDate.set(todayIsoDate());
    this.newStartTime.set('09:00');
    this.newEndDate.set(todayIsoDate());
    this.newEndTime.set('10:00');
    this.newDueDate.set(todayIsoDate());
    this.newDueTime.set('09:00');
    this.newIsAllDay.set(false);
    this.newRepeat.set(null);
    this.newIntervalCount.set(1);
    this.newUntil.set('');
    this.newAssignedTo.set('');
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
