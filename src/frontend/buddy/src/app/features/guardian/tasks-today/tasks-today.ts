import { Component, OnInit, inject, signal } from '@angular/core';

import { AssignableMember, CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { toIsoDateInTimeZone } from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AgendaEntry, groupTaskRuns, isTaskRun, occurrenceKey } from '../../../core/task-run';
import { UsersService } from '../../../core/users.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

const TASK_KIND = 1;

// One dashboard row: either a plain task (totalCount 1, toggle-able directly, unchanged from
// before this rolled anything up) or the rollup of every subtask occurrence a template-scheduled
// task produced today (totalCount > 1) -- shown as a single "N of M done" row rather than one row
// per subtask, since this is a summary widget, not the full agenda (see CalendarAgenda for that).
export interface TaskRollup {
  itemId: string;
  calendarId: string;
  calendarName: string;
  title: string;
  icon: string;
  color: string;
  assignedTo: string | null;
  dueAt: string | null;
  isAllDay: boolean;
  completedCount: number;
  totalCount: number;
  occurrences: CalendarOccurrence[];
}

function toRollup(entry: AgendaEntry): TaskRollup {
  if (!isTaskRun(entry)) {
    return {
      itemId: entry.itemId,
      calendarId: entry.calendarId,
      calendarName: entry.calendarName,
      title: entry.title,
      icon: entry.icon,
      color: entry.color,
      assignedTo: entry.assignedTo,
      dueAt: entry.dueAt,
      isAllDay: entry.isAllDay,
      completedCount: entry.isCompleted ? 1 : 0,
      totalCount: 1,
      occurrences: [entry]
    };
  }

  // "Overdue" for the whole run reads off its LAST subtask's due time -- that's when the entire
  // routine should have been finished, not when its first step was due.
  const last = entry.subtasks.reduce((latest, occurrence) => ((occurrence.dueAt ?? '') > (latest.dueAt ?? '') ? occurrence : latest));

  return {
    itemId: entry.itemId,
    calendarId: entry.calendarId,
    calendarName: entry.calendarName,
    title: entry.parentTitle,
    icon: entry.icon,
    color: entry.color,
    assignedTo: last.assignedTo,
    dueAt: last.dueAt,
    isAllDay: last.isAllDay,
    completedCount: entry.subtasks.filter((occurrence) => occurrence.isCompleted).length,
    totalCount: entry.subtasks.length,
    occurrences: entry.subtasks
  };
}

@Component({
  selector: 'app-tasks-today',
  imports: [TranslatePipe, LoadingSpinner],
  templateUrl: './tasks-today.html'
})
export class TasksToday implements OnInit {
  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);

  private currentUserId: string | null = null;

  protected readonly overdue = signal<TaskRollup[]>([]);
  protected readonly dueToday = signal<TaskRollup[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingTaskId = signal<string | null>(null);
  protected readonly memberNamesById = signal<Record<string, string>>({});

  ngOnInit(): void {
    void this.loadTasks();
  }

  protected canToggle(rollup: TaskRollup): boolean {
    return rollup.assignedTo === null || rollup.assignedTo === this.currentUserId;
  }

  // Best-effort: falls back to null (rendered as nothing) when the guardian can only view the
  // task's calendar, since listAssignableMembers -- and so this name -- requires Contributor access.
  protected assigneeNameFor(rollup: TaskRollup): string | null {
    return rollup.assignedTo ? (this.memberNamesById()[rollup.assignedTo] ?? null) : null;
  }

  // Only a rollup with exactly one underlying occurrence is toggle-able directly from this
  // summary widget -- a multi-subtask run's individual completion is left to the full agenda
  // (CalendarAgenda), which has room to show and check off each subtask on its own row.
  protected keyFor(rollup: TaskRollup): string {
    return occurrenceKey(rollup.occurrences[0]);
  }

  protected async toggleTask(rollup: TaskRollup): Promise<void> {
    if (!this.canToggle(rollup) || rollup.totalCount !== 1) {
      return;
    }

    const task = rollup.occurrences[0];
    const isCompleted = !task.isCompleted;
    const date = toIsoDateInTimeZone(new Date(), this.users.timeZoneId());
    const key = occurrenceKey(task);

    this.savingTaskId.set(key);

    try {
      await this.calendars.setTaskCompletion(task.calendarId, task.itemId, date, isCompleted, task.subtaskId ?? null);

      const applyCompletion = (existing: TaskRollup): TaskRollup =>
        existing.itemId === rollup.itemId
          ? { ...existing, completedCount: isCompleted ? 1 : 0, occurrences: [{ ...task, isCompleted }] }
          : existing;

      this.overdue.update((current) => current.map(applyCompletion));
      this.dueToday.update((current) => current.map(applyCompletion));
    } catch {
      this.error.set('dashboard.tasks.taskUpdateError');
    } finally {
      this.savingTaskId.set(null);
    }
  }

  private async loadTasks(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const [me, occurrences] = await Promise.all([this.users.ensureCurrentUser(), this.calendars.listTodayOccurrences()]);
      this.currentUserId = me.id;

      const tasks = occurrences.filter((occurrence) => occurrence.kind === TASK_KIND);
      const rollups = groupTaskRuns(tasks).map(toRollup);
      const now = Date.now();
      const isOverdue = (rollup: TaskRollup) => !rollup.isAllDay && rollup.dueAt !== null && new Date(rollup.dueAt).getTime() < now;

      this.overdue.set(rollups.filter(isOverdue));
      this.dueToday.set(rollups.filter((rollup) => !isOverdue(rollup)));

      void this.loadAssigneeNames(tasks);
    } catch {
      this.error.set('dashboard.tasks.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadAssigneeNames(tasks: CalendarOccurrence[]): Promise<void> {
    const assignedCalendarIds = [...new Set(tasks.filter((task) => task.assignedTo !== null).map((task) => task.calendarId))];
    const memberLists = await Promise.all(
      assignedCalendarIds.map((calendarId) => this.calendars.listAssignableMembers(calendarId).catch((): AssignableMember[] => []))
    );

    this.memberNamesById.set(
      Object.fromEntries(memberLists.flat().map((member) => [member.userId, `${member.givenName} ${member.familyName}`.trim()]))
    );
  }
}
