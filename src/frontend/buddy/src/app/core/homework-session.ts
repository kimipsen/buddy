import { HomeworkChunks, HomeworkPart, HomeworkStep, planHomework } from './homework-plan';
import { TimerState } from './visual-timer';

// A homework session in the child's browser (docs/frontend/analysis/homework-splitter.md,
// Question 4): the planned steps, which one is current, and when its clock started. Pure state
// and pure transitions -- no clock, no storage -- so every case is a plain test.

const MINUTE_MS = 60_000;

export interface HomeworkSession {
  date: string; // yyyy-MM-dd, the child's local date it was started
  parts: HomeworkPart[];
  chunks: HomeworkChunks; // copied in at the start; a guardian's later change waits for the next session
  steps: HomeworkStep[];
  currentIndex: number; // steps.length once finished
  startedAtMs: number | null; // when the current step's clock started; null before the first Start
  pausedAtMs: number | null;
}

export function createSession(
  parts: HomeworkPart[],
  chunks: HomeworkChunks,
  date: string,
): HomeworkSession {
  return {
    date,
    parts,
    chunks,
    steps: planHomework(parts, chunks),
    currentIndex: 0,
    startedAtMs: null,
    pausedAtMs: null,
  };
}

export function currentStep(session: HomeworkSession): HomeworkStep | null {
  return session.steps[session.currentIndex] ?? null;
}

export function isFinished(session: HomeworkSession): boolean {
  return session.currentIndex >= session.steps.length;
}

// The first step's Start. Every later step starts on the tap that moves to it (`next`).
export function start(session: HomeworkSession, nowMs: number): HomeworkSession {
  return session.startedAtMs === null
    ? { ...session, startedAtMs: nowMs, pausedAtMs: null }
    : session;
}

export function pause(session: HomeworkSession, nowMs: number): HomeworkSession {
  return session.startedAtMs !== null && session.pausedAtMs === null
    ? { ...session, pausedAtMs: nowMs }
    : session;
}

// Moves the start forward by the time spent paused, so the step keeps the budget it had left.
export function resume(session: HomeworkSession, nowMs: number): HomeworkSession {
  if (session.startedAtMs === null || session.pausedAtMs === null) {
    return session;
  }

  return {
    ...session,
    startedAtMs: session.startedAtMs + (nowMs - session.pausedAtMs),
    pausedAtMs: null,
  };
}

// Done with the current step (early or on time): the next one starts at once. Nothing calls this
// by itself -- it is always a tap (Take a break, Back to work, Done).
export function next(session: HomeworkSession, nowMs: number): HomeworkSession {
  const currentIndex = Math.min(session.currentIndex + 1, session.steps.length);
  const finished = currentIndex >= session.steps.length;
  return {
    ...session,
    currentIndex,
    startedAtMs: finished ? null : nowMs,
    pausedAtMs: null,
  };
}

// "Done with this part": the part's remaining chunks (and the breaks before them) are dropped,
// then the session moves on as `next` does.
export function finishPart(session: HomeworkSession, nowMs: number): HomeworkSession {
  const step = currentStep(session);
  if (step?.kind !== 'focus') {
    return session;
  }

  const before = session.steps.slice(0, session.currentIndex);
  const after = session.steps.slice(session.currentIndex + 1);
  const kept = after.filter(
    (candidate, i) =>
      !isChunkOf(candidate, step.partId) &&
      !(candidate.kind === 'break' && isChunkOf(after[i + 1], step.partId)),
  );
  const steps = renumber([...before, step, ...kept], step.partId);

  return next({ ...session, steps }, nowMs);
}

// "I need more time": one more chunk of the guardian's focus length for this part, started at
// once, with no break in between -- the child is still in the middle of it.
export function addChunk(session: HomeworkSession, nowMs: number): HomeworkSession {
  const step = currentStep(session);
  if (step?.kind !== 'focus') {
    return session;
  }

  const extra: HomeworkStep = { ...step, minutes: session.chunks.focusMinutes };
  const steps = renumber(
    [
      ...session.steps.slice(0, session.currentIndex + 1),
      extra,
      ...session.steps.slice(session.currentIndex + 1),
    ],
    step.partId,
  );

  return next({ ...session, steps }, nowMs);
}

// How many breaks came before the current step: picks the break idea, the same one after a reload.
export function breaksBefore(session: HomeworkSession): number {
  return session.steps.slice(0, session.currentIndex).filter((step) => step.kind === 'break')
    .length;
}

// Feeds <app-visual-timer>. `current`/`next` stay null: they are calendar occurrences, and the
// homework page names the step itself.
export function homeworkTimerState(session: HomeworkSession, nowMs: number): TimerState {
  const step = currentStep(session);
  const runBudgetMs = session.steps.reduce((sum, s) => sum + s.minutes * MINUTE_MS, 0);

  if (!step) {
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

  const budgetMs = step.minutes * MINUTE_MS;
  const laterMs = session.steps
    .slice(session.currentIndex + 1)
    .reduce((sum, s) => sum + s.minutes * MINUTE_MS, 0);
  const { startedAtMs, pausedAtMs } = session;
  const elapsedMs = startedAtMs === null ? 0 : (pausedAtMs ?? nowMs) - startedAtMs;
  const remainingMs = Math.min(budgetMs, Math.max(0, budgetMs - elapsedMs));
  const phase =
    startedAtMs === null
      ? 'upcoming'
      : pausedAtMs !== null
        ? 'paused'
        : remainingMs > 0
          ? 'running'
          : 'timesUp';

  return {
    current: null,
    next: null,
    remainingMs,
    budgetMs,
    runRemainingMs: remainingMs + laterMs,
    runBudgetMs,
    deadlineMs: phase === 'running' && startedAtMs !== null ? startedAtMs + budgetMs : null,
    startsAtMs: null,
    phase,
    childStarted: startedAtMs !== null,
  };
}

function isChunkOf(step: HomeworkStep | undefined, partId: string): boolean {
  return step?.kind === 'focus' && step.partId === partId;
}

// Renumbers one part's chunks "1 of n" .. "n of n" after one was added or some were dropped.
function renumber(steps: HomeworkStep[], partId: string): HomeworkStep[] {
  const of = steps.filter((step) => isChunkOf(step, partId)).length;
  let index = 0;
  return steps.map((step) =>
    step.kind === 'focus' && step.partId === partId ? { ...step, index: ++index, of } : step,
  );
}
