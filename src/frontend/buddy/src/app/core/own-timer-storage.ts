import { OwnTimer } from './own-timer';

// The child's own timer on this device, so a reload or a visit to another page keeps it running
// (docs/frontend/analysis/child-timer-preferences.md, Question 4). Same guarded approach as
// child-timer-storage.ts: storage can be unavailable or hold anything, and either just means "no
// timer".
const KEY = 'buddy_child_own_timer';

// The stored timer for `date` (yyyy-MM-dd); an entry from another day is thrown away.
export function readOwnTimer(date: string): OwnTimer | null {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? 'null') as {
      date?: unknown;
      budgetMs?: unknown;
      startedAtMs?: unknown;
      pausedAtMs?: unknown;
    } | null;

    if (stored?.date !== date) {
      return null;
    }

    const { budgetMs, startedAtMs, pausedAtMs } = stored;

    if (
      typeof budgetMs !== 'number' ||
      budgetMs <= 0 ||
      typeof startedAtMs !== 'number' ||
      (pausedAtMs !== null && typeof pausedAtMs !== 'number')
    ) {
      return null;
    }

    return { budgetMs, startedAtMs, pausedAtMs };
  } catch {
    return null;
  }
}

export function writeOwnTimer(date: string, timer: OwnTimer): void {
  try {
    localStorage.setItem(KEY, JSON.stringify({ date, ...timer }));
  } catch {
    // Not persisted; the timer still runs until the page is left.
  }
}

export function clearOwnTimer(): void {
  try {
    localStorage.removeItem(KEY);
  } catch {
    // Nothing to clear when storage is unavailable.
  }
}
