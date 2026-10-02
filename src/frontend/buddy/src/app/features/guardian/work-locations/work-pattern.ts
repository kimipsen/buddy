import { addDaysIso, parseIsoDate, startOfWeekIso } from '../../../core/date-utils';
import { Weekday } from '../../../core/work-locations.service';

export const MAX_CYCLE_WEEKS = 4;

// Monday-first display order, as backend DayOfWeek ordinals (0 = Sunday).
export const WEEKDAYS_MONDAY_FIRST: readonly Weekday[] = [1, 2, 3, 4, 5, 6, 0];

export const WEEK_NAMES: readonly string[] = ['A', 'B', 'C', 'D'];

function weeksBetween(fromMonday: string, toMonday: string): number {
  const ms = parseIsoDate(toMonday).getTime() - parseIsoDate(fromMonday).getTime();
  // Rounded rather than floored: a DST change makes a local "week" 1 hour shorter or longer.
  return Math.round(ms / (7 * 24 * 60 * 60 * 1000));
}

// Mirrors the backend's WorkPattern.CycleWeekOf: whole weeks from the anchor to the Monday on or
// before isoDate, floored modulo cycleWeeks so dates before the anchor still land correctly.
export function cycleWeekOf(anchorMonday: string, cycleWeeks: number, isoDate: string): number {
  const weeks = weeksBetween(anchorMonday, startOfWeekIso(isoDate));
  return ((weeks % cycleWeeks) + cycleWeeks) % cycleWeeks;
}

// The anchor Monday that makes the week containing todayIso cycle week `currentWeek`.
export function anchorForCurrentWeek(todayIso: string, currentWeek: number): string {
  return addDaysIso(startOfWeekIso(todayIso), -7 * currentWeek);
}

export function patternKey(week: number, day: Weekday): string {
  return `${week}|${day}`;
}
