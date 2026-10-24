import { CalendarOccurrence } from './calendars.service';
import { TaskRun, occurrenceKey } from './task-run';

// The child's visual time-left timer (docs/frontend/analysis/visual-time-left-timer.md). Pure
// functions of (runs, now, anchor) only -- no clock, no storage -- so every case is a plain test.

// A run stays "current" this long after its last subtask's window ended, so a late morning still
// shows time's up on the step that's left, but an unfinished routine doesn't hang around all day.
export const CURRENT_RUN_GRACE_MS = 30 * 60_000;
// A run that hasn't started yet counts as current (in the `upcoming` phase) this long before it
// starts, so the child can see the bedtime routine coming.
export const CURRENT_RUN_LOOK_AHEAD_MS = 15 * 60_000;

// Set once the child taps Start: the current subtask gets its full budget from startedAtMs
// instead of following the schedule. undefined means schedule-anchored.
export interface TimerAnchor {
  subtaskKey: string; // occurrenceKey() of the subtask the budget belongs to
  startedAtMs: number;
  pausedAtMs: number | null;
}

export type TimerPhase = 'upcoming' | 'running' | 'paused' | 'timesUp' | 'done';

export interface TimerState {
  current: CalendarOccurrence | null; // first incomplete subtask, null when the run is done
  next: CalendarOccurrence | null; // the incomplete subtask after `current`
  remainingMs: number; // clamped to [0, budgetMs]
  budgetMs: number; // endsAt - startsAt of `current`
  runRemainingMs: number; // current remaining + budgets of later incomplete subtasks
  runBudgetMs: number; // sum of all subtask budgets
  // The instant `remainingMs` reaches zero while the phase is `running`, else null. A view can
  // count down against it between recalculations without knowing which mode the timer is in.
  deadlineMs: number | null;
  startsAtMs: number | null; // the current subtask's scheduled start, for "Starts at ..."
  phase: TimerPhase;
  childStarted: boolean;
}

interface TimeWindow {
  startMs: number;
  endMs: number;
}

// Only template-scheduled subtasks have a Timed window; a plain task is Due (an instant).
function windowOf(occurrence: CalendarOccurrence): TimeWindow | null {
  const { timing } = occurrence;
  return timing.kind === 0
    ? { startMs: Date.parse(timing.startsAt), endMs: Date.parse(timing.endsAt) }
    : null;
}

function budgetOf(occurrence: CalendarOccurrence): number {
  const window = windowOf(occurrence);
  return window ? Math.max(0, window.endMs - window.startMs) : 0;
}

// The whole run's window, from its first subtask's start to its last subtask's end; null when a
// subtask has no window (nothing to count down).
function runWindow(run: TaskRun): TimeWindow | null {
  const windows = run.subtasks.map(windowOf);

  if (windows.some((window) => window === null)) {
    return null;
  }

  const timed = windows as TimeWindow[];
  return {
    startMs: Math.min(...timed.map((window) => window.startMs)),
    endMs: Math.max(...timed.map((window) => window.endMs)),
  };
}

interface Candidate {
  run: TaskRun;
  window: TimeWindow;
}

function earliest(candidates: Candidate[]): TaskRun | null {
  return (
    candidates.reduce<Candidate | null>(
      (best, candidate) =>
        best === null || candidate.window.startMs < best.window.startMs ? candidate : best,
      null,
    )?.run ?? null
  );
}

// The run the child is on right now. A run the child started a timer for (anchoredItemId) stays
// current until it is finished, whatever the clock says. Otherwise: the earliest started,
// unfinished run whose last subtask ended no more than the grace period ago; failing that, the
// next run starting within the look-ahead.
export function currentRun(
  runs: readonly TaskRun[],
  nowMs: number,
  anchoredItemId?: string | null,
): TaskRun | null {
  const candidates: Candidate[] = [];

  for (const run of runs) {
    const window = runWindow(run);
    if (window !== null && run.subtasks.some((subtask) => !subtask.isCompleted)) {
      candidates.push({ run, window });
    }
  }

  const anchored = candidates.find(({ run }) => run.itemId === anchoredItemId);
  if (anchored) {
    return anchored.run;
  }

  const started = candidates.filter(
    ({ window }) => window.startMs <= nowMs && nowMs <= window.endMs + CURRENT_RUN_GRACE_MS,
  );
  // Laid out so the Stryker comment below covers only the equivalent `>`.
  // prettier-ignore
  const upcoming = candidates.filter(
    ({ window }) =>
      // Stryker disable next-line EqualityOperator: a run starting exactly now is always in `started`, which wins over `upcoming`
      window.startMs > nowMs &&
      window.startMs - nowMs <= CURRENT_RUN_LOOK_AHEAD_MS,
  );

  return earliest(started) ?? earliest(upcoming);
}

// An anchor only counts while it belongs to the current subtask; any other anchor (a step since
// ticked on another device, a template edited mid-routine) falls back to the schedule.
function anchorFor(current: CalendarOccurrence, anchor?: TimerAnchor): TimerAnchor | undefined {
  return anchor?.subtaskKey === occurrenceKey(current) ? anchor : undefined;
}

export function timerState(run: TaskRun, nowMs: number, anchor?: TimerAnchor): TimerState {
  const subtasks = run.subtasks;
  const runBudgetMs = subtasks.reduce((sum, subtask) => sum + budgetOf(subtask), 0);
  const current = subtasks.find((subtask) => !subtask.isCompleted);

  if (current === undefined) {
    return {
      current: null,
      next: null,
      remainingMs: 0,
      budgetMs: 0,
      runRemainingMs: 0,
      runBudgetMs,
      deadlineMs: null,
      startsAtMs: null,
      phase: 'done',
      childStarted: false,
    };
  }

  const later = subtasks
    .slice(subtasks.indexOf(current) + 1)
    .filter((subtask) => !subtask.isCompleted);
  const laterBudgetMs = later.reduce((sum, subtask) => sum + budgetOf(subtask), 0);
  const budgetMs = budgetOf(current);
  const window = windowOf(current);
  const startsAtMs = window?.startMs ?? null;
  const active = anchorFor(current, anchor);

  let phase: TimerPhase;
  let remainingMs: number;
  let deadlineMs: number | null = null;

  if (active) {
    const elapsedMs = (active.pausedAtMs ?? nowMs) - active.startedAtMs;
    remainingMs = clamp(budgetMs - elapsedMs, budgetMs);

    // A budget used up is time's up, even if Pause was tapped after the deadline.
    if (remainingMs <= 0) {
      phase = 'timesUp';
    } else if (active.pausedAtMs !== null) {
      phase = 'paused';
    } else {
      phase = 'running';
      deadlineMs = active.startedAtMs + budgetMs;
    }
  } else if (window === null || nowMs < window.startMs) {
    phase = 'upcoming';
    remainingMs = budgetMs;
  } else if (nowMs >= window.endMs) {
    phase = 'timesUp';
    remainingMs = 0;
  } else {
    phase = 'running';
    remainingMs = window.endMs - nowMs;
    deadlineMs = window.endMs;
  }

  return {
    current,
    next: later[0] ?? null,
    remainingMs,
    budgetMs,
    runRemainingMs: remainingMs + laterBudgetMs,
    runBudgetMs,
    deadlineMs,
    startsAtMs,
    phase,
    childStarted: active !== undefined,
  };
}

function clamp(value: number, max: number): number {
  return Math.min(max, Math.max(0, value));
}

// The anchor for a child tapping Start on the run's current subtask; undefined when the run is done.
export function startAnchor(run: TaskRun, nowMs: number): TimerAnchor | undefined {
  const current = run.subtasks.find((subtask) => !subtask.isCompleted);
  return current
    ? { subtaskKey: occurrenceKey(current), startedAtMs: nowMs, pausedAtMs: null }
    : undefined;
}

export function pauseAnchor(anchor: TimerAnchor, nowMs: number): TimerAnchor {
  return anchor.pausedAtMs === null ? { ...anchor, pausedAtMs: nowMs } : anchor;
}

// Shifts the start by the time spent paused, so the remaining time is what it was at the pause.
export function resumeAnchor(anchor: TimerAnchor, nowMs: number): TimerAnchor {
  return anchor.pausedAtMs === null
    ? anchor
    : {
        ...anchor,
        startedAtMs: anchor.startedAtMs + (nowMs - anchor.pausedAtMs),
        pausedAtMs: null,
      };
}

// After a subtask is ticked or unticked in child-started mode: the new current subtask gets its
// full budget from now (the auto-chain), an unchanged current subtask keeps its anchor, and a
// finished run drops it.
export function reanchor(
  run: TaskRun,
  anchor: TimerAnchor,
  nowMs: number,
): TimerAnchor | undefined {
  const current = run.subtasks.find((subtask) => !subtask.isCompleted);

  if (!current) {
    return undefined;
  }

  return occurrenceKey(current) === anchor.subtaskKey
    ? anchor
    : { subtaskKey: occurrenceKey(current), startedAtMs: nowMs, pausedAtMs: null };
}

// Whole minutes left, rounded up: 4:01 reads as 5, so "0 min" never shows while time is left.
export function minutesLeft(remainingMs: number): number {
  return Math.ceil(Math.max(0, remainingMs) / 60_000);
}
