import { Component, OnInit, inject, signal } from '@angular/core';

import { AssignableMember, CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { toIsoDateInTimeZone } from '../../../core/date-utils';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UsersService } from '../../../core/users.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

const TASK_KIND = 1;

@Component({
  selector: 'app-tasks-today',
  imports: [TranslatePipe, LoadingSpinner],
  templateUrl: './tasks-today.html'
})
export class TasksToday implements OnInit {
  private readonly calendars = inject(CalendarsService);
  private readonly users = inject(UsersService);

  private currentUserId: string | null = null;

  protected readonly overdue = signal<CalendarOccurrence[]>([]);
  protected readonly dueToday = signal<CalendarOccurrence[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingTaskId = signal<string | null>(null);
  protected readonly memberNamesById = signal<Record<string, string>>({});

  ngOnInit(): void {
    void this.loadTasks();
  }

  protected canToggle(task: CalendarOccurrence): boolean {
    return task.assignedTo === null || task.assignedTo === this.currentUserId;
  }

  // Best-effort: falls back to null (rendered as nothing) when the guardian can only view the
  // task's calendar, since listAssignableMembers -- and so this name -- requires Contributor access.
  protected assigneeNameFor(task: CalendarOccurrence): string | null {
    return task.assignedTo ? (this.memberNamesById()[task.assignedTo] ?? null) : null;
  }

  protected async toggleTask(task: CalendarOccurrence): Promise<void> {
    if (!this.canToggle(task)) {
      return;
    }

    const isCompleted = !task.isCompleted;
    const date = toIsoDateInTimeZone(new Date(), this.users.timeZoneId());

    this.savingTaskId.set(task.itemId);

    try {
      await this.calendars.setTaskCompletion(task.calendarId, task.itemId, date, isCompleted);
      const applyCompletion = (existing: CalendarOccurrence) => (existing.itemId === task.itemId ? { ...existing, isCompleted } : existing);

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
      const now = Date.now();
      const isOverdue = (task: CalendarOccurrence) =>
        !task.isAllDay && task.dueAt !== null && new Date(task.dueAt).getTime() < now;

      this.overdue.set(tasks.filter(isOverdue));
      this.dueToday.set(tasks.filter((task) => !isOverdue(task)));

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
