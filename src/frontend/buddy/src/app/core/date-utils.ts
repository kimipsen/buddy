export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function todayIsoDate(): string {
  return toIsoDate(new Date());
}

// Parsed as local-timezone components rather than `new Date(isoDate)` -- the latter parses an
// unqualified "YYYY-MM-DD" as UTC midnight, which can land on the wrong calendar day once
// formatted back in a timezone behind UTC.
export function parseIsoDate(isoDate: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function addDaysIso(isoDate: string, days: number): string {
  const date = parseIsoDate(isoDate);
  return toIsoDate(new Date(date.getFullYear(), date.getMonth(), date.getDate() + days));
}

// Monday of the week containing isoDate -- getDay() is 0 (Sun) through 6 (Sat); shifting back by
// (day + 6) % 7 walks to the preceding Monday (0 for a Monday itself).
export function startOfWeekIso(isoDate: string): string {
  const offset = (parseIsoDate(isoDate).getDay() + 6) % 7;
  return addDaysIso(isoDate, -offset);
}

export function startOfMonthIso(isoDate: string): string {
  const date = parseIsoDate(isoDate);
  return toIsoDate(new Date(date.getFullYear(), date.getMonth(), 1));
}

// Adds a whole number of months, clamping the day-of-month into the target month (e.g. Jan 31 + 1
// month lands on the last day of February, not March 3rd).
export function shiftMonthIso(isoDate: string, months: number): string {
  const date = parseIsoDate(isoDate);
  const target = new Date(date.getFullYear(), date.getMonth() + months, 1);
  const lastDayOfTargetMonth = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
  return toIsoDate(new Date(target.getFullYear(), target.getMonth(), Math.min(date.getDate(), lastDayOfTargetMonth)));
}

export function buildDateRangeIso(startIsoDate: string, dayCount: number): string[] {
  return Array.from({ length: dayCount }, (_, offset) => addDaysIso(startIsoDate, offset));
}

// Every Monday-start week that intersects the calendar month containing isoDate, including the
// leading/trailing days from the previous/next month needed to fill complete rows -- a 4-, 5-, or
// 6-row grid depending on the month, not a fixed 42-cell grid.
export function buildMonthGridIso(isoDate: string): string[] {
  const gridStart = startOfWeekIso(startOfMonthIso(isoDate));
  const lastOfMonth = addDaysIso(shiftMonthIso(startOfMonthIso(isoDate), 1), -1);
  const gridEnd = addDaysIso(startOfWeekIso(lastOfMonth), 6);
  const totalDays = Math.round((parseIsoDate(gridEnd).getTime() - parseIsoDate(gridStart).getTime()) / 86_400_000) + 1;
  return buildDateRangeIso(gridStart, totalDays);
}

const TIME_ZONE_IDS = Intl.supportedValuesOf('timeZone').sort((a, b) => a.localeCompare(b));

export function listTimeZoneIds(): readonly string[] {
  return TIME_ZONE_IDS;
}

export function browserTimeZoneId(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

// Groups a resolved instant (e.g. a calendar occurrence's startsAt/dueAt) by calendar day in a
// specific IANA time zone -- unlike toIsoDate, which reads the browser's own local time zone via
// Date getters, this must use the zone the occurrence is actually being viewed in (the signed-in
// user's stored time zone, the same one UserDatePipe renders with). "en-CA" formats as
// "yyyy-MM-dd" directly, so no manual field assembly is needed.
export function toIsoDateInTimeZone(date: Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(date);
}

// Companion to toIsoDateInTimeZone -- resolves a "HH:mm" (24-hour) wall-clock time in a specific
// IANA time zone, the shape app-time-select and the reschedule/create item APIs use.
export function toTimeInTimeZone(date: Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-GB', { timeZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(date);
}
