import { HomeworkPart } from './homework-plan';
import { HomeworkSession } from './homework-session';

// The child's homework session on this device (docs/frontend/analysis/homework-splitter.md,
// Question 7). Same guarded approach as child-timer-storage.ts: storage can be unavailable or hold
// anything, and either just means "no session", i.e. the plan form.
const KEY = 'buddy_homework_session';

// The stored session for `date` (yyyy-MM-dd); one from another day, or one that doesn't look like
// a session, is thrown away.
export function readHomeworkSession(date: string): HomeworkSession | null {
  try {
    const stored = JSON.parse(
      localStorage.getItem(KEY) ?? 'null',
    ) as Partial<HomeworkSession> | null;

    if (stored?.date !== date || !isSession(stored)) {
      return null;
    }

    return stored;
  } catch {
    return null;
  }
}

export function writeHomeworkSession(session: HomeworkSession): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(session));
  } catch {
    // Not persisted; a reload starts over at the plan form.
  }
}

export function clearHomeworkSession(): void {
  try {
    localStorage.removeItem(KEY);
  } catch {
    // Nothing to clear when storage is unavailable.
  }
}

function isNumberOrNull(value: unknown): boolean {
  return value === null || typeof value === 'number';
}

// Stored JSON is untyped: check each part and step rather than trusting the types, since the page
// reads a part's title and a focus step's "n of m" without further checks.
function isPart(part: unknown): boolean {
  const candidate = part as Partial<Record<keyof HomeworkPart, unknown>> | null;
  return (
    typeof candidate?.id === 'string' &&
    typeof candidate.title === 'string' &&
    typeof candidate.estimateMinutes === 'number'
  );
}

function isStep(step: unknown, partIds: ReadonlySet<unknown>): boolean {
  const candidate = step as {
    kind?: unknown;
    minutes?: unknown;
    partId?: unknown;
    index?: unknown;
    of?: unknown;
  } | null;
  if (typeof candidate?.minutes !== 'number') {
    return false;
  }
  if (candidate.kind === 'break') {
    return true;
  }
  return (
    candidate.kind === 'focus' &&
    partIds.has(candidate.partId) &&
    typeof candidate.index === 'number' &&
    typeof candidate.of === 'number'
  );
}

function isSession(value: Partial<HomeworkSession>): value is HomeworkSession {
  if (!Array.isArray(value.parts) || !(value.parts as unknown[]).every(isPart)) {
    return false;
  }
  const partIds = new Set(value.parts.map((part) => part.id));

  return (
    Array.isArray(value.steps) &&
    (value.steps as unknown[]).every((step) => isStep(step, partIds)) &&
    typeof value.chunks?.focusMinutes === 'number' &&
    typeof value.chunks.breakMinutes === 'number' &&
    typeof value.currentIndex === 'number' &&
    value.currentIndex >= 0 &&
    value.currentIndex <= value.steps.length &&
    isNumberOrNull(value.startedAtMs) &&
    isNumberOrNull(value.pausedAtMs)
  );
}
