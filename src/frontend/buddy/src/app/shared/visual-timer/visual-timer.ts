import { Component, computed, effect, inject, input, signal } from '@angular/core';

import { TranslationService } from '../../core/i18n/translation.service';
import { UsersService } from '../../core/users.service';
import { TimerPhase, TimerState, minutesLeft } from '../../core/visual-timer';

const TICK_MS = 1_000;
const MINUTE_MS = 60_000;
// The disc is drawn in a 100x100 viewBox: a slate track circle and the remaining time as a wedge
// from 12 o'clock, clockwise.
const CENTER = 50;
const RADIUS = 47;

// The child's time-left timer (docs/frontend/analysis/visual-time-left-timer.md, Question 6):
// a shrinking wedge (`disc`) or a shrinking bar (`bar`, the whole run) in the run's own colour on
// a slate track, always with a text label, so colour never carries the meaning alone.
// Presentational: the page computes the TimerState (core/visual-timer.ts); this component only
// counts down against its deadline between the page's recalculations, once a second, and only
// while it's running.
@Component({
  selector: 'app-visual-timer',
  imports: [],
  templateUrl: './visual-timer.html',
})
export class VisualTimer {
  private readonly translation = inject(TranslationService);
  private readonly users = inject(UsersService);

  readonly state = input.required<TimerState>();
  readonly color = input('#34d399');
  readonly variant = input<'disc' | 'bar'>('disc');
  // The disc's width and height as a CSS length; ignored by the bar.
  readonly size = input('28px');
  // The focus view's big disc: the label goes under it, in large type.
  readonly large = input(false);
  // The run's title, for the bar's "x min left of {title}".
  readonly title = input('');
  // Whether this timer speaks changes of phase and step through its live region. A page showing
  // two timers for one run lets only one of them speak -- the one that stays mounted when the step
  // changes (the child home's run bar), so "Next step" isn't lost with the old step's disc.
  readonly announce = input(true);

  private readonly now = signal(Date.now());

  // Reduced motion: the disc still shows the time left (it carries information), but only redraws
  // on whole minutes.
  private readonly reducedMotion =
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // The page's phase, except that a running timer whose deadline has passed reads as time's up
  // without waiting for the page to recalculate.
  protected readonly phase = computed<TimerPhase>(() => {
    const state = this.state();
    return state.phase === 'running' && state.deadlineMs !== null && this.now() >= state.deadlineMs
      ? 'timesUp'
      : state.phase;
  });

  private readonly remainingMs = computed(() => {
    const state = this.state();
    const isBar = this.variant() === 'bar';
    const fallback = isBar ? state.runRemainingMs : state.remainingMs;

    if (this.phase() === 'timesUp' && !isBar) {
      return 0;
    }

    if (state.phase !== 'running' || state.deadlineMs === null) {
      return fallback;
    }

    // Later incomplete subtasks follow the current one, so the bar counts down with it.
    const laterMs = state.runRemainingMs - state.remainingMs;
    const currentMs = Math.max(0, state.deadlineMs - this.now());
    return isBar ? currentMs + laterMs : currentMs;
  });

  private readonly budgetMs = computed(() =>
    this.variant() === 'bar' ? this.state().runBudgetMs : this.state().budgetMs,
  );

  // The share of the budget left, 0..1, in whole minutes under reduced motion.
  protected readonly fraction = computed(() => {
    const budget = this.budgetMs();
    if (budget <= 0) {
      return 0;
    }

    const remaining = this.reducedMotion
      ? Math.min(budget, minutesLeft(this.remainingMs()) * MINUTE_MS)
      : this.remainingMs();
    return Math.min(1, Math.max(0, remaining / budget));
  });

  protected readonly percent = computed(() => this.fraction() * 100);

  protected readonly wedgePath = computed(() => wedge(this.fraction()));
  protected readonly isFull = computed(() => this.fraction() >= 1);

  protected readonly center = CENTER;
  protected readonly radius = RADIUS;

  // The visible label, which is also the timer's accessible name. Rounded up to whole minutes:
  // children don't need seconds, and a seconds counter adds pressure.
  protected readonly label = computed(() => {
    const phase = this.phase();
    const state = this.state();
    const t = (key: string, params?: Record<string, string | number>) =>
      this.translation.translate(key, params);

    if (phase === 'timesUp' && this.variant() === 'disc') {
      return t('child.home.timer.timesUp');
    }

    if (phase === 'paused' && this.variant() === 'disc') {
      return t('child.home.timer.paused');
    }

    if (phase === 'upcoming' && this.variant() === 'disc' && state.startsAtMs !== null) {
      return t('child.home.timer.startsAt', { time: this.formatTime(state.startsAtMs) });
    }

    const minutes = minutesLeft(this.remainingMs());

    if (this.variant() === 'bar') {
      return t('child.home.timer.runLeft', { count: minutes, title: this.title() });
    }

    return this.remainingMs() < MINUTE_MS
      ? t('child.home.timer.underAMinute')
      : t('child.home.timer.minutesLeft', { count: minutes });
  });

  // The live region speaks only on a change of phase or step, never every minute -- it would keep
  // talking over the child.
  protected readonly announcement = signal('');
  private lastAnnounced: {
    phase: TimerPhase;
    subtask: string | null;
    childStarted: boolean;
  } | null = null;

  constructor() {
    // The tick only runs while the timer is running; the cleanup also runs on destroy.
    effect((onCleanup) => {
      if (this.state().phase !== 'running') {
        return;
      }

      this.now.set(Date.now());
      const id = setInterval(() => this.now.set(Date.now()), TICK_MS);
      onCleanup(() => clearInterval(id));
    });

    effect(() => {
      const phase = this.phase();
      const { current, childStarted } = this.state();
      const subtask = current?.routine?.subtaskId ?? null;
      const previous = this.lastAnnounced;
      this.lastAnnounced = { phase, subtask, childStarted };

      if (!this.announce() || previous === null) {
        return;
      }

      const key =
        subtask !== previous.subtask && current
          ? 'child.home.timer.announce.next'
          : phase !== previous.phase
            ? ANNOUNCEMENTS[phase]
            : childStarted && !previous.childStarted
              ? ANNOUNCEMENTS.running
              : undefined;

      if (key) {
        this.announcement.set(this.translation.translate(key, { title: current?.title ?? '' }));
      }
    });
  }

  private formatTime(ms: number): string {
    return new Intl.DateTimeFormat(this.translation.language(), {
      timeStyle: 'short',
      timeZone: this.users.timeZoneId(),
    }).format(new Date(ms));
  }
}

const ANNOUNCEMENTS: Partial<Record<TimerPhase, string>> = {
  running: 'child.home.timer.announce.started',
  paused: 'child.home.timer.announce.paused',
  timesUp: 'child.home.timer.announce.timesUp',
};

// An SVG path for a pie wedge covering `fraction` of the disc, clockwise from 12 o'clock. Empty
// for nothing left; a full disc is drawn as a circle instead (an arc can't close on itself).
function wedge(fraction: number): string {
  if (fraction <= 0 || fraction >= 1) {
    return '';
  }

  const angle = fraction * 2 * Math.PI;
  const x = CENTER + RADIUS * Math.sin(angle);
  const y = CENTER - RADIUS * Math.cos(angle);
  const largeArc = fraction > 0.5 ? 1 : 0;
  return `M ${CENTER} ${CENTER} L ${CENTER} ${CENTER - RADIUS} A ${RADIUS} ${RADIUS} 0 ${largeArc} 1 ${x.toFixed(3)} ${y.toFixed(3)} Z`;
}
