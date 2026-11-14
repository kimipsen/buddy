import { addDaysIso, buildDateRangeIso } from '../../../../core/date-utils';

// ListSleepDiaryEntries allows to - from <= 92. A printout counts nights with both ends included
// and allows 92 of them (to - from <= 91), like the visit summary's period.
export const MAX_PRINT_NIGHTS = 92;

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

// A real calendar date written YYYY-MM-DD: addDaysIso rebuilds the date from its local parts, so an
// impossible one (2026-02-30) or a two-digit-year one (0002-08-01) comes back changed.
export function isIsoDate(value: string): boolean {
  return ISO_DATE.test(value) && addDaysIso(value, 0) === value;
}

export type PrintPeriod =
  | { kind: 'valid'; from: string; to: string; days: string[] }
  // A date field is empty or not a real date.
  | { kind: 'missing' }
  // to is before from.
  | { kind: 'reversed' }
  // More than MAX_PRINT_NIGHTS nights.
  | { kind: 'tooLong' };

// Counts in calendar days (addDaysIso), never with getTime() differences: across a DST change such
// a difference is an hour off, which would reject or drop a night in Danish time.
export function printPeriod(from: string, to: string): PrintPeriod {
  if (!isIsoDate(from) || !isIsoDate(to)) {
    return { kind: 'missing' };
  }

  if (to < from) {
    return { kind: 'reversed' };
  }

  if (addDaysIso(from, MAX_PRINT_NIGHTS) <= to) {
    return { kind: 'tooLong' };
  }

  let nights = 1;

  while (addDaysIso(from, nights) <= to) {
    nights++;
  }

  return { kind: 'valid', from, to, days: buildDateRangeIso(from, nights) };
}
