import { HttpErrorResponse } from '@angular/common/http';

import { CalendarOccurrence } from './calendars.service';
import { toIsoDateInTimeZone } from './date-utils';
import { FIRST_THEN_BOARD_CHANGED, FirstThenBoard, FirstThenCard } from './first-then.service';
import { groupTaskRuns, isTaskRun } from './task-run';
import { TimerState } from './visual-timer';

// The First-Then board's pure rules (docs/backend/analysis/first-then-board.md): what counts as a
// current board, the Then countdown and the starter cards. No clock, no HTTP.

const MINUTE_MS = 60_000;

export function hasBoard(board: FirstThenBoard | null | undefined): board is FirstThenBoard {
  return !!board && board.state !== 'None' && !!board.first && !!board.then;
}

// A board last changed before `today` (the child's date) was left over from yesterday: the child's
// pages treat it as ended, the guardian page offers to end it. Nothing is written automatically.
export function isStale(board: FirstThenBoard, today: string, timeZone: string): boolean {
  return (
    !!board.lastModifiedAt && toIsoDateInTimeZone(new Date(board.lastModifiedAt), timeZone) < today
  );
}

// What the child's pages show: a current board from today, else nothing.
export function boardForChild(
  board: FirstThenBoard | null | undefined,
  today: string,
  timeZone: string,
): FirstThenBoard | null {
  return hasBoard(board) && !isStale(board, today, timeZone) ? board : null;
}

// The instant the Then's time runs out, or null when it has no length or isn't unlocked yet.
// Derived from the server's UnlockedAt, so every device shows the same time left.
export function thenDeadlineMs(board: FirstThenBoard): number | null {
  if (board.state !== 'Unlocked' || !board.unlockedAt || !board.thenMinutes) {
    return null;
  }

  return Date.parse(board.unlockedAt) + board.thenMinutes * MINUTE_MS;
}

export function isThenTimeUp(board: FirstThenBoard, nowMs: number): boolean {
  const deadline = thenDeadlineMs(board);
  return deadline !== null && nowMs >= deadline;
}

// The Then countdown as the shared visual-timer disc reads it: a single-step "run".
export function thenTimerState(board: FirstThenBoard, nowMs: number): TimerState | null {
  const deadline = thenDeadlineMs(board);

  if (deadline === null || !board.thenMinutes) {
    return null;
  }

  const budgetMs = board.thenMinutes * MINUTE_MS;
  const remainingMs = Math.min(budgetMs, Math.max(0, deadline - nowMs));
  const running = remainingMs > 0;

  return {
    current: null,
    next: null,
    remainingMs,
    budgetMs,
    runRemainingMs: remainingMs,
    runBudgetMs: budgetMs,
    deadlineMs: running ? deadline : null,
    startsAtMs: null,
    phase: running ? 'running' : 'timesUp',
    childStarted: false,
  };
}

// A write that named a revision a guardian has since replaced.
export function isBoardChanged(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 409 &&
    (error.error as { code?: string } | null)?.code === FIRST_THEN_BOARD_CHANGED
  );
}

// The central optimistic-concurrency 409: two writes raced on the same stream version, so the
// board changed under this caller too (first-then-board.md, Failure table).
export function isConcurrencyConflict(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 409 &&
    (error.error as { code?: string } | null)?.code === 'concurrency_conflict'
  );
}

export interface StarterCard {
  readonly titleKey: string;
  readonly icon: string;
}

// The built-in suggestions (Question 3). Once picked, the translated title is plain text on the
// board, in the guardian's language.
export const STARTER_FIRSTS: readonly StarterCard[] = [
  { titleKey: 'firstThen.starters.first.homework', icon: '📚' },
  { titleKey: 'firstThen.starters.first.tidyRoom', icon: '🧹' },
  { titleKey: 'firstThen.starters.first.shower', icon: '🚿' },
  { titleKey: 'firstThen.starters.first.brushTeeth', icon: '🪥' },
  { titleKey: 'firstThen.starters.first.getDressed', icon: '👕' },
  { titleKey: 'firstThen.starters.first.shoesOn', icon: '👟' },
  { titleKey: 'firstThen.starters.first.eatDinner', icon: '🍽️' },
];

export const STARTER_THENS: readonly StarterCard[] = [
  { titleKey: 'firstThen.starters.then.ipad', icon: '📱' },
  { titleKey: 'firstThen.starters.then.tv', icon: '📺' },
  { titleKey: 'firstThen.starters.then.playOutside', icon: '⚽' },
  { titleKey: 'firstThen.starters.then.lego', icon: '🧱' },
  { titleKey: 'firstThen.starters.then.snack', icon: '🍎' },
  { titleKey: 'firstThen.starters.then.readTogether', icon: '📖' },
  { titleKey: 'firstThen.starters.then.game', icon: '🎮' },
];

// The child's open tasks and routines today, as First suggestions (prefill only: the board keeps
// no link to the task, Question 8). A routine counts once, by its parent title, while any of its
// steps is still open. Distinct by case-insensitive title.
export function todaysTaskCards(
  occurrences: CalendarOccurrence[],
  childId: string,
): FirstThenCard[] {
  const cards: FirstThenCard[] = [];
  const seen = new Set<string>();

  for (const entry of groupTaskRuns(occurrences)) {
    const card = isTaskRun(entry)
      ? entry.subtasks[0].assignedTo === childId && entry.subtasks.some((s) => !s.isCompleted)
        ? { title: entry.parentTitle, icon: entry.icon }
        : null
      : entry.kind === 'Task' && entry.assignedTo === childId && !entry.isCompleted
        ? { title: entry.title, icon: entry.iconOverride ?? entry.icon }
        : null;

    if (card && card.title && !seen.has(card.title.toLowerCase())) {
      seen.add(card.title.toLowerCase());
      cards.push(card);
    }
  }

  return cards;
}

// The guardian form's length choices, in minutes; null is "no time limit".
export const THEN_LENGTHS: readonly (number | null)[] = [null, 5, 10, 15, 20, 30, 45, 60];
