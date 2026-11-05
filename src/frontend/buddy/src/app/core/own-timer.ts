import { TimerState } from './visual-timer';

// The child's own timer on /child/timer (docs/frontend/analysis/child-timer-preferences.md,
// Question 4): a length picked from OWN_TIMER_MINUTES, started, paused and resumed by the child.
// Pure functions of (timer, now), like core/visual-timer.ts, so every case is a plain test.

// Lengths a family uses a kitchen timer for; a child who can't read numbers yet taps a disc.
export const OWN_TIMER_MINUTES = [2, 5, 10, 15] as const;

export interface OwnTimer {
  budgetMs: number;
  // When the timer would have started had it never been paused: resuming moves it forward by the
  // paused time, so the time left is always budgetMs - (now - startedAtMs).
  startedAtMs: number;
  pausedAtMs: number | null;
}

export function startOwnTimer(minutes: number, nowMs: number): OwnTimer {
  return { budgetMs: minutes * 60_000, startedAtMs: nowMs, pausedAtMs: null };
}

export function pauseOwnTimer(timer: OwnTimer, nowMs: number): OwnTimer {
  return timer.pausedAtMs === null ? { ...timer, pausedAtMs: nowMs } : timer;
}

export function resumeOwnTimer(timer: OwnTimer, nowMs: number): OwnTimer {
  return timer.pausedAtMs === null
    ? timer
    : { ...timer, startedAtMs: timer.startedAtMs + (nowMs - timer.pausedAtMs), pausedAtMs: null };
}

// The state app-visual-timer draws. There is no routine behind it, so no current or next step.
export function ownTimerState(timer: OwnTimer, nowMs: number): TimerState {
  const elapsedMs = (timer.pausedAtMs ?? nowMs) - timer.startedAtMs;
  const remainingMs = Math.min(timer.budgetMs, Math.max(0, timer.budgetMs - elapsedMs));
  let phase: TimerState['phase'] = timer.pausedAtMs === null ? 'running' : 'paused';
  if (remainingMs === 0) {
    phase = 'timesUp';
  }

  return {
    current: null,
    next: null,
    remainingMs,
    budgetMs: timer.budgetMs,
    runRemainingMs: remainingMs,
    runBudgetMs: timer.budgetMs,
    deadlineMs: phase === 'running' ? timer.startedAtMs + timer.budgetMs : null,
    startsAtMs: null,
    phase,
    childStarted: true,
  };
}
