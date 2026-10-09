import { Component, computed, effect, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { FeaturesService } from '../../../../core/features.service';
import { firstAndLast } from '../../../../core/array-utils';
import {
  AssignableMember,
  CalendarItemKind,
  CalendarOccurrence,
  CalendarRole,
  CalendarSummary,
  CalendarsService,
  DatePart,
  RecurrenceFrequency,
  RecurrenceRuleRequest,
  Weekday,
} from '../../../../core/calendars.service';
import {
  addDaysIso,
  addMinutesToTime,
  buildDateRangeIso,
  buildMonthGridIso,
  DAYS_OF_WEEK,
  dayOfWeekAt,
  dayOfWeekIndex,
  parseIsoDate,
  shiftMonthIso,
  shiftRangeEnd,
  startOfWeekIso,
  toIsoDateInTimeZone,
  toTimeInTimeZone,
  todayIsoDate,
} from '../../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  AgendaEntry,
  TaskRun,
  compareOccurrences,
  groupTaskRuns,
  isTaskRun,
  occurrenceKey,
} from '../../../../core/task-run';
import { TaskLibraryService, TaskTemplate } from '../../../../core/task-library.service';
import { UsersService } from '../../../../core/users.service';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { createAction } from '../../../../shared/action-state/action-state';
import { ColorSwatchPicker } from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { DateSelect } from '../../../../shared/date-select/date-select';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Stepper } from '../../../../shared/stepper/stepper';
import { TimeSelect } from '../../../../shared/time-select/time-select';
import { Toggle } from '../../../../shared/toggle/toggle';
import { MonthGrid } from './month-grid/month-grid';
import { TaskPicker } from '../../task-library/task-picker/task-picker';
import { Card } from '../../../../shared/card/card';

export type NewTaskSource = 'manual' | 'template';

const DAYS_AHEAD = 7;
const EVENT_KIND = 'Event' satisfies CalendarItemKind;
const TASK_KIND = 'Task' satisfies CalendarItemKind;
// The same tiers CalendarAuthorization.CheckContribute accepts.
const CONTRIBUTE_ROLES: readonly CalendarRole[] = ['Owner', 'Contributor'];
const DEFAULT_COLOR = '#f43f5e';

export interface AgendaDay {
  date: string;
  label: string;
  // Only meaningful for month-grid days -- undefined everywhere else.
  isCurrentMonth?: boolean;
}

export type ViewMode = 'day' | 'workweek' | 'week' | 'month';

// The create form's Repeat choice: 'none' posts no recurrence rule at all.
export type RepeatChoice = RecurrenceFrequency | 'none';

const DAILY: RecurrenceFrequency = 'Daily';
const WEEKLY: RecurrenceFrequency = 'Weekly';

// The daily weekday toggles, Monday first, with the translation keys of their visible
// abbreviation and their accessible full name.
export const WEEKDAY_TOGGLES: readonly { day: Weekday; shortKey: string; nameKey: string }[] = [
  {
    day: 'Monday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.monShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.mon',
  },
  {
    day: 'Tuesday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.tueShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.tue',
  },
  {
    day: 'Wednesday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.wedShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.wed',
  },
  {
    day: 'Thursday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.thuShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.thu',
  },
  {
    day: 'Friday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.friShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.fri',
  },
  {
    day: 'Saturday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.satShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.sat',
  },
  {
    day: 'Sunday',
    shortKey: 'calendar.agenda.form.repeat.weekdays.sunShort',
    nameKey: 'calendar.agenda.form.repeat.weekdays.sun',
  },
];

const ALL_WEEKDAYS: readonly Weekday[] = DAYS_OF_WEEK;

// What one load of the visible range returns: the guardian's calendars and the range's occurrences.
interface LoadedWeek {
  myCalendars: CalendarSummary[];
  occurrences: CalendarOccurrence[];
}

const NOTHING_LOADED: LoadedWeek = { myCalendars: [], occurrences: [] };

const WORKWEEK_DAYS = 5;

function labeledDay(isoDate: string, locale: string): AgendaDay {
  return {
    date: isoDate,
    label: parseIsoDate(isoDate).toLocaleDateString(locale, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    }),
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

function buildMonthDays(anchorIsoDate: string): AgendaDay[] {
  const anchorMonth = parseIsoDate(anchorIsoDate).getMonth();

  return buildMonthGridIso(anchorIsoDate).map((date) => ({
    date,
    label: String(parseIsoDate(date).getDate()),
    isCurrentMonth: parseIsoDate(date).getMonth() === anchorMonth,
  }));
}

function instantFor(occurrence: CalendarOccurrence): Date {
  return new Date(occurrence.sortAt);
}

function toDatePart(date: string, time: string): DatePart {
  return { date, time: `${time}:00` };
}

// Formats a whole-minutes duration for display (e.g. "35m", "1h", "1h 30m") -- same convention as
// ManageTasks's and TaskPicker's own formatDuration, duplicated rather than shared since none of
// the three components import from another.
function formatDuration(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours === 0) {
    return `${minutes}m`;
  }

  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}

@Component({
  selector: 'app-calendar-agenda',
  imports: [
    FormsModule,
    TranslatePipe,
    UserDatePipe,
    ColorSwatchPicker,
    DateSelect,
    SegmentedControl,
    Stepper,
    TimeSelect,
    Toggle,
    MonthGrid,
    TaskPicker,
    Card,
  ],
  templateUrl: './agenda.html',
})
export class CalendarAgenda {
  protected readonly features = inject(FeaturesService);

  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);
  private readonly guardians = inject(GuardiansService);
  private readonly taskLibrary = inject(TaskLibraryService);

  protected readonly eventKind = EVENT_KIND;
  protected readonly taskKind = TASK_KIND;
  protected readonly today = todayIsoDate();

  protected readonly itemKindOptions = computed<SegmentedControlOption<CalendarItemKind>[]>(() => [
    { value: EVENT_KIND, label: this.translation.translate('calendar.agenda.form.kind.event') },
    { value: TASK_KIND, label: this.translation.translate('calendar.agenda.form.kind.task') },
  ]);

  protected readonly anchorDate = signal(todayIsoDate());
  protected readonly viewMode = signal<ViewMode>('week');
  protected readonly viewModes: readonly ViewMode[] = ['day', 'workweek', 'week', 'month'];

  // The rendered day list for Day/Work week/Week, and the full month grid (including
  // leading/trailing days from adjacent months) for Month -- also the sole source of the fetch
  // range in `range` below, which just reads the first/last date here regardless of mode.
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
        return buildMonthDays(anchor);
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
            day: 'numeric',
          }),
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
            year: 'numeric',
          }),
        });
    }
  });

  protected readonly previousLabelKey = computed(
    () =>
      ({
        day: 'calendar.agenda.previousDay',
        workweek: 'calendar.agenda.previousWeek',
        week: 'calendar.agenda.previousWeek',
        month: 'calendar.agenda.previousMonth',
      })[this.viewMode()],
  );

  protected readonly nextLabelKey = computed(
    () =>
      ({
        day: 'calendar.agenda.nextDay',
        workweek: 'calendar.agenda.nextWeek',
        week: 'calendar.agenda.nextWeek',
        month: 'calendar.agenda.nextMonth',
      })[this.viewMode()],
  );

  // The fetched date range. Compared by value so relabelling days() (a language switch) doesn't
  // refetch the same range.
  private readonly range = computed(
    () => {
      const [first, last] = firstAndLast(this.days());
      return { from: first.date, to: last.date };
    },
    { equal: (a, b) => a.from === b.from && a.to === b.to },
  );

  protected readonly week = resource({
    params: () => this.range(),
    loader: ({ params }) => this.loadWeek(params.from, params.to),
  });

  // What the agenda renders: the last range that loaded, kept through a later load or a failed one
  // (so the create form doesn't vanish while navigating), and patched locally after a task toggle
  // or a delete.
  private readonly shown = linkedSignal<LoadedWeek | undefined, LoadedWeek>({
    source: () => (this.week.hasValue() ? this.week.value() : undefined),
    computation: (loaded, previous) => loaded ?? previous?.value ?? NOTHING_LOADED,
  });

  protected readonly myCalendars = computed(() => this.shown().myCalendars);
  protected readonly eligibleCalendars = computed<CalendarSummary[]>(() =>
    this.myCalendars().filter((calendar) => CONTRIBUTE_ROLES.includes(calendar.role)),
  );

  protected readonly occurrences = computed(() => this.shown().occurrences);
  protected readonly hiddenCalendarIds = signal<Set<string>>(new Set());
  // Keyed by occurrenceKey; its error shows above the agenda list.
  protected readonly savingTask = createAction<string>();
  protected readonly confirmingDeleteItemId = signal<string | null>(null);
  // Keyed by itemId; its error shows above the agenda list.
  protected readonly deleting = createAction<string>();

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
  protected readonly savingEdit = createAction<string>();

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

      const date = toIsoDateInTimeZone(instantFor(occurrence), this.users.timeZoneId());
      const occurrencesForDate = (byDate[date] ??= []);
      occurrencesForDate.push(occurrence);
    }

    for (const dayOccurrences of Object.values(byDate)) {
      dayOccurrences.sort(compareOccurrences);
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

  // Defaults to the first calendar the guardian can contribute to, once one has loaded; a pick
  // (or that default) is kept across later loads.
  protected readonly newCalendarId = linkedSignal<CalendarSummary[], string>({
    source: this.eligibleCalendars,
    computation: (eligible, previous) => {
      const picked = previous?.value ?? '';
      return picked !== '' ? picked : (eligible[0]?.id ?? '');
    },
  });
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
  protected readonly newRepeat = signal<RepeatChoice>('none');
  protected readonly newIntervalCount = signal(1);
  protected readonly newUntil = signal('');
  // null until the guardian touches a weekday toggle: a daily rule then runs every day and a
  // weekly one on the start date's weekday (what the backend does with no weekdays). Reset when
  // the Repeat choice changes.
  protected readonly newWeekdays = signal<readonly Weekday[] | null>(null);
  protected readonly weekdayToggles = WEEKDAY_TOGGLES;
  private readonly seedWeekday = computed<Weekday | null>(() => {
    const day = parseIsoDate(
      this.newKind() === EVENT_KIND ? this.newStartDate() : this.newDueDate(),
    ).getDay();

    return Number.isNaN(day) ? null : dayOfWeekAt(day);
  });
  protected readonly shownWeekdays = computed<readonly Weekday[]>(() => {
    const explicit = this.newWeekdays();

    if (explicit) {
      return explicit;
    }

    if (this.newRepeat() === DAILY) {
      return ALL_WEEKDAYS;
    }

    const seed = this.seedWeekday();

    return seed === null ? [] : [seed];
  });
  protected readonly showsWeekdays = computed(
    () => this.newRepeat() === DAILY || this.newRepeat() === WEEKLY,
  );
  // A daily rule limited to some weekdays steps one day at a time (the backend requires it), so
  // the "Every" stepper is hidden while any day is off. A weekly rule keeps it (every N weeks).
  protected readonly weekdaysFiltered = computed(
    () => this.newRepeat() === DAILY && this.shownWeekdays().length < ALL_WEEKDAYS.length,
  );
  protected readonly newAssignedTo = signal('');
  protected readonly creating = createAction();

  // Only meaningful when newKind() === TASK_KIND -- 'template' swaps the free-form title/icon/
  // color entry for a TaskPicker over the selected assignee's TaskLibrary and posts through
  // scheduleTaskFromTemplate instead of createItem. See setTaskSource/onTemplateSelected/createItem.
  protected readonly newTaskSource = signal<NewTaskSource>('manual');
  protected readonly newTaskTemplateId = signal('');
  // Reads straight from the shared service state (like ManageTasks/AssignMealplan), filtered to
  // what's actually schedulable -- an archived template can still be listed here transiently right
  // after this component's own initial fetch races a manage-tasks tab archiving one, but never
  // offered as a pick.
  protected readonly taskTemplates = computed(() =>
    this.taskLibrary.templates().filter((template) => !template.isArchived),
  );

  protected readonly selectedTaskTemplate = computed(() =>
    this.taskTemplates().find((template) => template.id === this.newTaskTemplateId()),
  );

  protected readonly assignableMembers = signal<AssignableMember[]>([]);
  // Used only to tell whether the selected assignee is one of the guardian's own children (and
  // if so, which childId) -- AssignableMember carries no child/guardian discriminator of its own.
  private readonly children = resource({ loader: () => this.loadChildren() });
  // Merged across every calendar the guardian has assigned members for, keyed by userId -- used to
  // label an occurrence's assignee in the agenda list, not just the picker on the create form.
  private readonly memberNamesById = signal<Record<string, string>>({});

  protected readonly canSubmit = computed(() => {
    if (!this.newCalendarId() || !this.newTitle().trim() || !this.newColor().trim()) {
      return false;
    }

    if (this.showsWeekdays() && this.shownWeekdays().length === 0) {
      return false;
    }

    if (this.newKind() === TASK_KIND && this.newTaskSource() === 'template') {
      return this.newTaskTemplateId().trim() !== '' && this.newDueDate().trim() !== '';
    }

    return this.newKind() === EVENT_KIND
      ? this.newStartDate().trim() !== '' && this.newEndDate().trim() !== ''
      : this.newDueDate().trim() !== '';
  });

  constructor() {
    effect(() => {
      // The assignable set is per-calendar (group membership differs by calendar), so a previous
      // selection may no longer be valid once the calendar changes -- clear it here rather than
      // in resetForm(), which only runs after a successful submit.
      const calendarId = this.newCalendarId();
      this.newAssignedTo.set('');
      void this.loadAssignableMembers(calendarId);
    });

    effect(() => {
      // A TaskTemplate belongs to exactly one child, so the picker has to track whichever
      // assignee is currently selected rather than always showing one fixed child's templates.
      // A non-child assignee (a guardian) or "unassigned" has no template library of its own here
      // -- scheduleTaskFromTemplate falls back to the caller (this guardian) as the owning pivot
      // when unassigned, and guardians never own templates themselves -- so those cases clear the
      // list instead of leaving the previous assignee's templates showing.
      const child = (this.children.value() ?? []).find(
        (candidate) => candidate.id === this.newAssignedTo(),
      );

      if (child && this.features.enabled('taskLibrary')) {
        void this.taskLibrary.listTaskTemplates(child.id);
      } else {
        this.taskLibrary.clearTemplates();
      }
    });
  }

  protected formatDuration(totalMinutes: number): string {
    return formatDuration(totalMinutes);
  }

  // The picked template's total subtask duration applied to the currently chosen due time, so the
  // guardian can see when the task is expected to finish before submitting.
  protected templateEndTime(template: TaskTemplate): string {
    return addMinutesToTime(this.newDueTime(), template.totalDurationMinutes);
  }

  protected setViewMode(mode: ViewMode): void {
    this.clearListErrors();
    this.viewMode.set(mode);
  }

  protected viewModeLabelKey(mode: ViewMode): string {
    return {
      day: 'calendar.agenda.view.day',
      workweek: 'calendar.agenda.view.workweek',
      week: 'calendar.agenda.view.week',
      month: 'calendar.agenda.view.month',
    }[mode];
  }

  protected goToToday(): void {
    this.clearListErrors();
    this.anchorDate.set(todayIsoDate());
  }

  protected previousPeriod(): void {
    this.shiftPeriod(-1);
  }

  protected nextPeriod(): void {
    this.shiftPeriod(1);
  }

  private shiftPeriod(direction: 1 | -1): void {
    this.clearListErrors();

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

  // Folds a day's occurrences into agenda rows -- a template-scheduled task's subtask occurrences
  // (sharing an itemId, each with a routine) render as one bracketed TaskRun block instead of one row
  // each; every other occurrence is unaffected. See core/task-run.ts.
  protected groupedOccurrencesFor(date: string): AgendaEntry[] {
    return groupTaskRuns(this.occurrencesFor(date));
  }

  protected isRun(entry: AgendaEntry): entry is TaskRun {
    return isTaskRun(entry);
  }

  // Compound key distinguishing sibling subtask occurrences of the same template-scheduled run
  // (same itemId, different subtaskId) -- see core/task-run.ts's occurrenceKey for why itemId
  // alone is no longer sufficient once a run can produce more than one occurrence per item.
  protected keyFor(occurrence: CalendarOccurrence): string {
    return occurrenceKey(occurrence);
  }

  // Month view is overview/navigation only (see the "Rendering" section in docs/frontend/analysis/
  // guardian-full-calendar-views.md) -- picking a day there always drills into Day view for it.
  protected onMonthDaySelected(date: string): void {
    this.clearListErrors();
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
        ...Object.fromEntries(
          members.map((member) => [
            member.userId,
            `${member.givenName} ${member.familyName}`.trim(),
          ]),
        ),
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
    return toIsoDateInTimeZone(instantFor(occurrence), this.users.timeZoneId()) <= todayIsoDate();
  }

  protected async toggleTaskCompletion(occurrence: CalendarOccurrence): Promise<void> {
    const isCompleted = !occurrence.isCompleted;

    if (isCompleted && !this.canCompleteTask(occurrence)) {
      return;
    }

    const date = toIsoDateInTimeZone(instantFor(occurrence), this.users.timeZoneId());
    const key = occurrenceKey(occurrence);
    const range = this.range();

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
        this.patchOccurrences(range, (occurrences) =>
          occurrences.map((existing) =>
            occurrenceKey(existing) === key ? { ...existing, isCompleted } : existing,
          ),
        );
      },
      'calendar.agenda.taskUpdateError',
    );
  }

  protected requestDeleteItem(itemId: string): void {
    this.clearListErrors();
    this.editingItemId.set(null);
    this.confirmingDeleteItemId.set(itemId);
  }

  protected cancelDeleteItem(): void {
    this.confirmingDeleteItemId.set(null);
  }

  // Accepts anything carrying itemId/calendarId -- both a single CalendarOccurrence and a
  // TaskRun (the grouped view of a template-scheduled item's subtask occurrences) qualify, since
  // deleting always targets the whole scheduled item, never an individual subtask.
  protected async confirmDeleteItem(
    occurrence: Pick<CalendarOccurrence, 'itemId' | 'calendarId'>,
  ): Promise<void> {
    this.clearListErrors();
    const range = this.range();

    await this.deleting.run(
      occurrence.itemId,
      async () => {
        await this.calendars.deleteItem(occurrence.calendarId, occurrence.itemId);
        this.confirmingDeleteItemId.set(null);
        this.patchOccurrences(range, (occurrences) =>
          occurrences.filter((existing) => existing.itemId !== occurrence.itemId),
        );
      },
      'calendar.agenda.delete.error',
    );
  }

  protected startEditItem(occurrence: CalendarOccurrence): void {
    this.clearListErrors();
    this.confirmingDeleteItemId.set(null);
    this.savingEdit.clearError();
    this.editingItemId.set(occurrence.itemId);
    this.editingKind.set(occurrence.kind);
    this.editTitle.set(occurrence.title);
    this.editIcon.set(occurrence.iconOverride ?? '');
    this.editColor.set(occurrence.color);
    this.editIsAllDay.set(occurrence.isAllDay);

    const timeZoneId = this.users.timeZoneId();

    const { timing } = occurrence;

    if (timing.kind === 0) {
      const startsAt = new Date(timing.startsAt);
      this.editStartDate.set(toIsoDateInTimeZone(startsAt, timeZoneId));
      this.editStartTime.set(toTimeInTimeZone(startsAt, timeZoneId));

      const endsAt = new Date(timing.endsAt);
      const endDate = toIsoDateInTimeZone(endsAt, timeZoneId);
      // Stored EndsAt is exclusive for an all-day event -- show the last inclusive day instead.
      this.editEndDate.set(occurrence.isAllDay ? addDaysIso(endDate, -1) : endDate);
      this.editEndTime.set(toTimeInTimeZone(endsAt, timeZoneId));
    } else {
      const dueAt = new Date(timing.dueAt);
      this.editDueDate.set(toIsoDateInTimeZone(dueAt, timeZoneId));
      this.editDueTime.set(toTimeInTimeZone(dueAt, timeZoneId));
    }
  }

  // Moving the start moves the end by the same amount, so the event keeps its length and can't
  // end before it starts. The end fields themselves still change only the end.
  protected setEditStart(date: string, time: string): void {
    const end = shiftRangeEnd(
      { date: this.editStartDate(), time: this.editStartTime() },
      { date, time },
      { date: this.editEndDate(), time: this.editEndTime() },
    );
    this.editStartDate.set(date);
    this.editStartTime.set(time);
    this.editEndDate.set(end.date);
    this.editEndTime.set(end.time);
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

    // The end date shown/entered is inclusive for an all-day event -- store it exclusive.
    const startTime = isAllDay ? '00:00' : this.editStartTime();
    const endTime = isAllDay ? '00:00' : this.editEndTime();
    const endDate = isAllDay ? addDaysIso(this.editEndDate(), 1) : this.editEndDate();
    const dueTime = isAllDay ? '00:00' : this.editDueTime();

    await this.savingEdit.run(
      occurrence.itemId,
      async () => {
        await this.calendars.updateItemDetails(occurrence.calendarId, occurrence.itemId, {
          title,
          icon: icon || null,
          color,
        });
        await this.calendars.rescheduleItem(occurrence.calendarId, occurrence.itemId, {
          schedule:
            kind === EVENT_KIND
              ? {
                  kind: 0,
                  startsAt: toDatePart(this.editStartDate(), startTime),
                  endsAt: toDatePart(endDate, endTime),
                  isAllDay,
                }
              : { kind: 1, dueDate: toDatePart(this.editDueDate(), dueTime), isAllDay },
        });

        this.editingItemId.set(null);
        this.reloadWeek();
      },
      'calendar.agenda.edit.error',
    );
  }

  // 'manual' is the default and only ever needs clearing newTaskTemplateId (so a leftover pick
  // never lingers into a later template-mode visit); switching *into* 'template' additionally
  // forces isAllDay off -- a template-scheduled task is never all-day (see scheduleTaskFromTemplate
  // below), so a stray `true` left over from manual-mode editing would otherwise hide the due-time
  // picker without actually taking effect.
  protected setTaskSource(source: NewTaskSource): void {
    this.newTaskSource.set(source);

    if (source === 'manual') {
      this.newTaskTemplateId.set('');
    } else {
      this.newIsAllDay.set(false);
    }
  }

  // Pre-fills title/icon/color from the picked template -- a one-time copy, not a live binding,
  // so the guardian can still edit them afterward without the picker fighting back.
  protected onTemplateSelected(templateId: string): void {
    this.newTaskTemplateId.set(templateId);

    const template = this.taskTemplates().find((candidate) => candidate.id === templateId);

    if (template) {
      this.newTitle.set(template.name);
      this.newIcon.set(template.icon);
      this.newColor.set(template.color);
    }
  }

  // Same as setEditStart, for the create form.
  protected setNewStart(date: string, time: string): void {
    const end = shiftRangeEnd(
      { date: this.newStartDate(), time: this.newStartTime() },
      { date, time },
      { date: this.newEndDate(), time: this.newEndTime() },
    );
    this.newStartDate.set(date);
    this.newStartTime.set(time);
    this.newEndDate.set(end.date);
    this.newEndTime.set(end.time);
  }

  protected async createItem(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }

    const calendarId = this.newCalendarId();
    const kind = this.newKind();

    await this.creating.run(
      true,
      async () => {
        if (kind === TASK_KIND && this.newTaskSource() === 'template') {
          await this.createTaskFromTemplate(calendarId);
        } else {
          await this.createManualItem(calendarId, kind);
        }

        this.resetForm();
        this.reloadWeek();
      },
      'calendar.agenda.form.createError',
    );
  }

  private async createTaskFromTemplate(calendarId: string): Promise<void> {
    await this.calendars.scheduleTaskFromTemplate(calendarId, {
      taskTemplateId: this.newTaskTemplateId(),
      startDate: this.newDueDate(),
      startTime: `${this.newDueTime()}:00`,
      recurrence: this.buildRecurrence(),
      assignedTo: this.newAssignedTo() || null,
      title: this.newTitle().trim(),
      icon: this.newIcon().trim() || null,
      color: this.newColor().trim(),
    });
  }

  private async createManualItem(calendarId: string, kind: CalendarItemKind): Promise<void> {
    const isAllDay = this.newIsAllDay();

    // The end date shown/entered is inclusive for an all-day event -- store it exclusive.
    const startTime = isAllDay ? '00:00' : this.newStartTime();
    const endTime = isAllDay ? '00:00' : this.newEndTime();
    const endDate = isAllDay ? addDaysIso(this.newEndDate(), 1) : this.newEndDate();
    const dueTime = isAllDay ? '00:00' : this.newDueTime();

    await this.calendars.createItem(calendarId, {
      title: this.newTitle().trim(),
      icon: this.newIcon().trim() || null,
      color: this.newColor().trim(),
      schedule:
        kind === EVENT_KIND
          ? {
              kind: 0,
              startsAt: toDatePart(this.newStartDate(), startTime),
              endsAt: toDatePart(endDate, endTime),
              isAllDay,
            }
          : {
              kind: 1,
              dueDate: toDatePart(this.newDueDate(), dueTime),
              isAllDay,
              assignedTo: this.newAssignedTo() || null,
            },
      recurrence: this.buildRecurrence(),
    });
  }

  private buildRecurrence(): RecurrenceRuleRequest | null {
    const frequency = this.newRepeat();

    if (frequency === 'none') {
      return null;
    }

    const filtered = this.weekdaysFiltered();
    const explicitWeekly = frequency === WEEKLY && this.newWeekdays() !== null;

    return {
      frequency,
      intervalCount: filtered ? 1 : this.newIntervalCount(),
      until: this.newUntil().trim() || null,
      weekdays:
        filtered || explicitWeekly
          ? [...this.shownWeekdays()].sort((a, b) => dayOfWeekIndex(a) - dayOfWeekIndex(b))
          : null,
    };
  }

  protected chooseRepeat(choice: RepeatChoice): void {
    this.newRepeat.set(choice);
    this.newWeekdays.set(null);
  }

  protected isWeekdayOn(day: Weekday): boolean {
    return this.shownWeekdays().includes(day);
  }

  protected toggleWeekday(day: Weekday): void {
    const days = this.shownWeekdays();
    this.newWeekdays.set(days.includes(day) ? days.filter((d) => d !== day) : [...days, day]);
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
    this.newRepeat.set('none');
    this.newIntervalCount.set(1);
    this.newUntil.set('');
    this.newWeekdays.set(null);
    this.newAssignedTo.set('');
    this.newTaskSource.set('manual');
    this.newTaskTemplateId.set('');
  }

  // A fresh load also replaces whatever the last task toggle or delete reported.
  private clearListErrors(): void {
    this.savingTask.clearError();
    this.deleting.clearError();
  }

  // Applies a mutation's result to the shown occurrences, unless the visible range changed while
  // the request ran -- that range's own load then supplies fresh data instead. `range` keeps its
  // identity while the dates are unchanged (its equal function), so identity is the check.
  private patchOccurrences(
    range: ReturnType<typeof this.range>,
    patch: (occurrences: CalendarOccurrence[]) => CalendarOccurrence[],
  ): void {
    if (this.range() !== range) {
      return;
    }

    this.shown.update((current) => ({ ...current, occurrences: patch(current.occurrences) }));
  }

  private reloadWeek(): void {
    this.clearListErrors();
    this.week.reload();
  }

  private async loadChildren(): Promise<ChildSummary[]> {
    try {
      return await this.guardians.listMyChildren();
    } catch {
      // The template picker is a nice-to-have on the create form -- if this fails, manual task
      // creation still works, just without template-based scheduling as an option.
      return [];
    }
  }

  private async loadWeek(from: string, to: string): Promise<LoadedWeek> {
    const [myCalendars, occurrences] = await Promise.all([
      this.calendars.listMyCalendars(),
      this.calendars.listOccurrencesInRange(from, to),
    ]);

    return { myCalendars, occurrences };
  }
}
