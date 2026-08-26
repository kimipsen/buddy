import { Component, OnInit, inject, signal } from '@angular/core';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

const TASK_KIND = 1;

@Component({
  selector: 'app-tasks-today',
  imports: [TranslatePipe, LoadingSpinner],
  templateUrl: './tasks-today.html'
})
export class TasksToday implements OnInit {
  private readonly calendars = inject(CalendarsService);

  protected readonly overdue = signal<CalendarOccurrence[]>([]);
  protected readonly dueToday = signal<CalendarOccurrence[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadTasks();
  }

  private async loadTasks(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const occurrences = await this.calendars.listTodayOccurrences();
      const tasks = occurrences.filter((occurrence) => occurrence.kind === TASK_KIND);
      const now = Date.now();

      this.overdue.set(tasks.filter((task) => task.dueAt !== null && new Date(task.dueAt).getTime() < now));
      this.dueToday.set(tasks.filter((task) => task.dueAt === null || new Date(task.dueAt).getTime() >= now));
    } catch {
      this.error.set('dashboard.tasks.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
