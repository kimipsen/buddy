import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  resource,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { ChildClockService } from '../../../core/child-clock.service';
import { completeChildTask } from '../../../core/child-task-completion';
import { ChildTimerService } from '../../../core/child-timer.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  TaskRun,
  compareOccurrences,
  groupTaskRuns,
  isTaskRun,
  occurrenceKey,
} from '../../../core/task-run';
import { TimerState, timerState } from '../../../core/visual-timer';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { VisualTimer } from '../../../shared/visual-timer/visual-timer';
import { ChildPage } from '../child-page/child-page';
import { Icon } from '../../../shared/icon/icon';

// Same cadence as the child home: the timer component counts the seconds itself, this only moves
// the phase on (e.g. from upcoming to running).
const NOW_REFRESH_INTERVAL_MS = 60_000;

// The "time timer on the table" view of one of today's routines (`/child/focus/:itemId`): one big
// disc for the current step, the step after it, and the Done / Start / Pause controls, nothing
// else. See docs/frontend/analysis/visual-time-left-timer.md, Question 4.
@Component({
  selector: 'app-child-focus',
  imports: [Icon, TranslatePipe, RouterLink, ChildPage, LoadingSpinner, VisualTimer],
  templateUrl: './child-focus.html',
})
export class ChildFocus implements OnInit, OnDestroy {
  private readonly calendars = inject(CalendarsService);
  private readonly clock = inject(ChildClockService);
  private readonly childTimer = inject(ChildTimerService);
  private readonly itemId = inject(ActivatedRoute).snapshot.paramMap.get('itemId') ?? '';

  // Today's occurrences come from the same per-day cache as the child home, so opening this page
  // from there costs no request.
  protected readonly occurrences = resource({
    loader: () => this.calendars.listTodayOccurrences(this.clock.today()),
  });

  protected readonly run = computed<TaskRun | null>(() => {
    if (!this.occurrences.hasValue()) {
      return null;
    }

    const subtasks = this.occurrences
      .value()
      .filter((occurrence) => occurrence.itemId === this.itemId && occurrence.routine !== null)
      .sort(compareOccurrences);
    return groupTaskRuns(subtasks).find(isTaskRun) ?? null;
  });

  private readonly now = signal(Date.now());
  private nowIntervalId: ReturnType<typeof setInterval> | undefined;

  protected readonly state = computed<TimerState | null>(() => {
    const run = this.run();
    return run ? timerState(run, this.now(), this.childTimer.anchorFor(run.itemId)) : null;
  });

  // A finished run, or one that isn't today's (deleted, rescheduled, a stale bookmark), gets the
  // friendly "All done" card rather than an error.
  protected readonly allDone = computed(
    () => this.occurrences.hasValue() && (this.state()?.phase ?? 'done') === 'done',
  );

  protected readonly saving = createAction<string>();

  constructor() {
    // Recalculate the moment the current step's time runs out, so the buttons follow the disc.
    effect((onCleanup) => {
      const deadlineMs = this.state()?.deadlineMs;
      if (deadlineMs === null || deadlineMs === undefined) {
        return;
      }

      const id = setTimeout(() => this.now.set(Date.now()), Math.max(0, deadlineMs - Date.now()));
      onCleanup(() => clearTimeout(id));
    });
  }

  ngOnInit(): void {
    this.nowIntervalId = setInterval(() => this.now.set(Date.now()), NOW_REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    clearInterval(this.nowIntervalId);
  }

  protected keyFor(task: CalendarOccurrence): string {
    return occurrenceKey(task);
  }

  protected async markDone(task: CalendarOccurrence): Promise<void> {
    const key = occurrenceKey(task);

    await this.saving.run(
      key,
      async () => {
        await completeChildTask(this.calendars, task, true, this.clock.today());

        if (this.occurrences.hasValue() && !this.occurrences.isLoading()) {
          this.occurrences.set(
            this.occurrences
              .value()
              .map((existing) =>
                occurrenceKey(existing) === key ? { ...existing, isCompleted: true } : existing,
              ),
          );

          // A child-started timer moves on to the next step with its own full budget.
          const run = this.run();
          if (run) {
            this.childTimer.afterCompletionChange(run);
          }
        }
        this.now.set(Date.now());
      },
      'child.home.loadError',
    );
  }

  protected start(run: TaskRun): void {
    this.childTimer.start(run);
    this.now.set(Date.now());
  }

  protected pause(run: TaskRun): void {
    this.childTimer.pause(run.itemId);
    this.now.set(Date.now());
  }

  protected resume(run: TaskRun): void {
    this.childTimer.resume(run.itemId);
    this.now.set(Date.now());
  }

  protected stop(): void {
    this.childTimer.stop();
    this.now.set(Date.now());
  }
}
