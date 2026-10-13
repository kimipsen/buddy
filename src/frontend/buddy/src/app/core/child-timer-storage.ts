import { TimerAnchor } from './visual-timer';

// The child-started timer on this device -- a device convenience, not configuration, so it isn't
// synced (docs/frontend/analysis/visual-time-left-timer.md, Question 8). Same guarded approach as
// ai-meal-filter-storage.ts: storage can be unavailable or hold anything, and either just means
// "no anchor", i.e. the schedule-anchored timer.
const KEY = 'buddy_child_timer';

export interface StoredTimer {
  itemId: string;
  anchor: TimerAnchor;
}

// The stored timer for `date` (yyyy-MM-dd); an entry from another day is thrown away.
export function readTimerAnchor(date: string): StoredTimer | null {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? 'null') as {
      itemId?: unknown;
      date?: unknown;
      anchor?: { subtaskKey?: unknown; startedAtMs?: unknown; pausedAtMs?: unknown };
    } | null;

    if (stored?.date !== date || typeof stored.itemId !== 'string') {
      return null;
    }

    const { subtaskKey, startedAtMs, pausedAtMs } = stored.anchor ?? {};

    if (
      typeof subtaskKey !== 'string' ||
      typeof startedAtMs !== 'number' ||
      (pausedAtMs !== null && typeof pausedAtMs !== 'number')
    ) {
      return null;
    }

    return { itemId: stored.itemId, anchor: { subtaskKey, startedAtMs, pausedAtMs } };
  } catch {
    return null;
  }
}

// Only one run is child-started at a time, so a new entry simply replaces the old one.
export function writeTimerAnchor(itemId: string, date: string, anchor: TimerAnchor): void {
  try {
    localStorage.setItem(KEY, JSON.stringify({ itemId, date, anchor }));
  } catch {
    // Not persisted; the timer goes back to the schedule after a reload.
  }
}

export function clearTimerAnchor(): void {
  try {
    localStorage.removeItem(KEY);
  } catch {
    // Nothing to clear when storage is unavailable.
  }
}
