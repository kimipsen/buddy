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

const NO_FILTER: AiMealFilter = { ratedOnly: false, servedWithin: 0 };
const SERVED_WINDOWS: readonly AiServedWindow[] = [0, 30, 60, 90];

export function readLastMealFilter(): AiMealFilter {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Partial<AiMealFilter> | null;
    return {
      ratedOnly: stored?.ratedOnly === true,
      servedWithin: SERVED_WINDOWS.find((window) => window === stored?.servedWithin) ?? 0,
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
