import { Component, inject, resource, signal } from '@angular/core';

import {
  AssignableMember,
  CalendarOccurrence,
  CalendarsService,
} from '../../../core/calendars.service';
import { NonEmptyArray } from '../../../core/array-utils';
import { toIsoDateInTimeZone } from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import { AgendaEntry, groupTaskRuns, isTaskRun, occurrenceKey } from '../../../core/task-run';
import { UsersService } from '../../../core/users.service';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { Toggle } from '../../../shared/toggle/toggle';

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
  dueAt: string;
  isAllDay: boolean;
  completedCount: number;
  totalCount: number;
  occurrences: NonEmptyArray<CalendarOccurrence>;
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
      dueAt: entry.sortAt,
      isAllDay: entry.isAllDay,
      completedCount: entry.isCompleted ? 1 : 0,
      totalCount: 1,
      occurrences: [entry],
    };
  }

  // "Overdue" for the whole run reads off its LAST subtask's start -- that's when the entire
  // routine should have been under way, not when its first step was.
  const last = entry.subtasks.reduce((latest, occurrence) =>
    occurrence.sortAt > latest.sortAt ? occurrence : latest,
  );

  return {
    itemId: entry.itemId,
    calendarId: entry.calendarId,
    calendarName: entry.calendarName,
    title: entry.parentTitle,
    icon: entry.icon,
    color: entry.color,
    assignedTo: last.assignedTo,
    dueAt: last.sortAt,
    isAllDay: last.isAllDay,
    completedCount: entry.subtasks.filter((occurrence) => occurrence.isCompleted).length,
    totalCount: entry.subtasks.length,
    occurrences: entry.subtasks,
  };
}

// What the widget loaded: the signed-in guardian (to decide which tasks they may toggle) and
// today's tasks split into overdue and still due.
interface LoadedTasks {
  currentUserId: string;
  overdue: TaskRollup[];
  dueToday: TaskRollup[];
}

@Component({
  selector: 'app-tasks-today',
  imports: [TranslatePipe, LoadingSpinner, Toggle],
  templateUrl: './tasks-today.html',
})
export class TasksToday {
  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);

  protected readonly tasks = resource({ loader: () => this.loadTasks() });
  protected readonly saving = createAction<string>();
  protected readonly memberNamesById = signal<Record<string, string>>({});

  protected canToggle(rollup: TaskRollup): boolean {
    return (
      rollup.assignedTo === null ||
      (this.tasks.hasValue() && rollup.assignedTo === this.tasks.value().currentUserId)
    );
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

    await this.saving.run(
      key,
      async () => {
        await this.calendars.setTaskCompletion(
          task.calendarId,
          task.itemId,
          date,
          isCompleted,
          task.routine?.subtaskId ?? null,
        );

        const applyCompletion = (existing: TaskRollup): TaskRollup =>
          existing.itemId === rollup.itemId
            ? {
                ...existing,
                completedCount: isCompleted ? 1 : 0,
                occurrences: [{ ...task, isCompleted }],
              }
            : existing;

        this.tasks.update(
          (current) =>
            current && {
              ...current,
              overdue: current.overdue.map(applyCompletion),
              dueToday: current.dueToday.map(applyCompletion),
            },
        );
      },
      'dashboard.tasks.taskUpdateError',
    );
  }

  private async loadTasks(): Promise<LoadedTasks> {
    const [me, occurrences] = await Promise.all([
      this.users.ensureCurrentUser(),
      this.calendars.listTodayOccurrences(),
    ]);

    const tasks = occurrences.filter((occurrence) => occurrence.kind === TASK_KIND);
    const rollups = groupTaskRuns(tasks).map(toRollup);
    const now = Date.now();
    const isOverdue = (rollup: TaskRollup) =>
      !rollup.isAllDay && new Date(rollup.dueAt).getTime() < now;

    void this.loadAssigneeNames(tasks);

    return {
      currentUserId: me.id,
      overdue: rollups.filter(isOverdue),
      dueToday: rollups.filter((rollup) => !isOverdue(rollup)),
    };
  }

  private async loadAssigneeNames(tasks: CalendarOccurrence[]): Promise<void> {
    const assignedCalendarIds = [
      ...new Set(tasks.filter((task) => task.assignedTo !== null).map((task) => task.calendarId)),
    ];
    const memberLists = await mapWithConcurrency(
      assignedCalendarIds,
      PER_ITEM_REQUEST_CONCURRENCY,
      (calendarId) =>
        this.calendars.listAssignableMembers(calendarId).catch((): AssignableMember[] => []),
    );

    this.memberNamesById.set(
      Object.fromEntries(
        memberLists
          .flat()
          .map((member) => [member.userId, `${member.givenName} ${member.familyName}`.trim()]),
      ),
    );
  }
}
