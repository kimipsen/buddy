import { AiServedWindow } from '../../../../core/ai-assistant.service';

// The meal filter last used to start an AI session on this device -- a convenience, not
// configuration (see docs/backend/analysis/ai-assistant-meal-filter.md, Question 2), so it isn't
// synced. Same guarded approach as last-template-storage.ts: storage can be unavailable or hold
// anything, and either just means "no filter".
const KEY = 'buddy_ai_meal_filter';

export interface AiMealFilter {
  ratedOnly: boolean;
  servedWithin: AiServedWindow;
}

const NO_FILTER: AiMealFilter = { ratedOnly: false, servedWithin: 'Any' };
const SERVED_WINDOWS: readonly AiServedWindow[] = ['Any', 'Last30Days', 'Last60Days', 'Last90Days'];
// Before the API sent enums by name, the window was stored as its day count.
const LEGACY_SERVED_WINDOWS: Readonly<Record<number, AiServedWindow>> = {
  30: 'Last30Days',
  60: 'Last60Days',
  90: 'Last90Days',
};

export function readLastMealFilter(): AiMealFilter {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? 'null') as {
      ratedOnly?: unknown;
      servedWithin?: unknown;
    } | null;
    const servedWithin = stored?.servedWithin;
    return {
      ratedOnly: stored?.ratedOnly === true,
      servedWithin:
        SERVED_WINDOWS.find((window) => window === servedWithin) ??
        (typeof servedWithin === 'number' ? LEGACY_SERVED_WINDOWS[servedWithin] : undefined) ??
        'Any',
    };
  } catch {
    return NO_FILTER;
  }
}

export function writeLastMealFilter(filter: AiMealFilter): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(filter));
  } catch {
    // Not persisted; the form just starts unfiltered next time.
  }
}
