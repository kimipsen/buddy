import type { Schemas } from './api/schemas';

export type DayOfWeek = Schemas['DayOfWeek'];

// The backend's DayOfWeek names, indexed like Date.getDay() (0 = Sunday).
export const DAYS_OF_WEEK = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
] as const satisfies readonly DayOfWeek[];

// Date.getDay()'s number for a day (0 = Sunday ... 6 = Saturday).
export function dayOfWeekIndex(day: DayOfWeek): number {
  return DAYS_OF_WEEK.indexOf(day);
}

// The day for Date.getDay()'s number (0 = Sunday ... 6 = Saturday).
export function dayOfWeekAt(index: number): DayOfWeek {
  return DAYS_OF_WEEK[((index % 7) + 7) % 7] ?? 'Sunday';
}

// The day an ISO date falls on.
export function dayOfWeekOf(isoDate: string): DayOfWeek {
  return dayOfWeekAt(parseIsoDate(isoDate).getDay());
}

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
  const [year = NaN, month = NaN, day = NaN] = isoDate.split('-').map(Number);
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
  return toIsoDate(
    new Date(
      target.getFullYear(),
      target.getMonth(),
      Math.min(date.getDate(), lastDayOfTargetMonth),
    ),
  );
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
  const totalDays =
    Math.round((parseIsoDate(gridEnd).getTime() - parseIsoDate(gridStart).getTime()) / 86_400_000) +
    1;
  return buildDateRangeIso(gridStart, totalDays);
}

// Intl lists only canonical IANA names, so it leaves out "UTC" -- the backend's default for a new
// user (TimeZoneId.Utc) and what a browser in UTC reports. Without it, a new user's time zone has
// no <option> to show.
const TIME_ZONE_IDS = [...new Set([...Intl.supportedValuesOf('timeZone'), 'UTC'])].sort((a, b) =>
  a.localeCompare(b),
);

export function listTimeZoneIds(): readonly string[] {
  return TIME_ZONE_IDS;
}

export function browserTimeZoneId(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

// Groups a resolved instant (e.g. a calendar occurrence's sortAt) by calendar day in a
// specific IANA time zone -- unlike toIsoDate, which reads the browser's own local time zone via
// Date getters, this must use the zone the occurrence is actually being viewed in (the signed-in
// user's stored time zone, the same one UserDatePipe renders with). "en-CA" formats as
// "yyyy-MM-dd" directly, so no manual field assembly is needed.
export function toIsoDateInTimeZone(date: Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date);
}

// Companion to toIsoDateInTimeZone -- resolves a "HH:mm" (24-hour) wall-clock time in a specific
// IANA time zone, the shape app-time-select and the reschedule/create item APIs use.
export function toTimeInTimeZone(date: Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    timeZone,
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(date);
}

// Adds a whole number of minutes to a "HH:mm" wall-clock time, wrapping across midnight -- used to
// derive a task's expected end time from its scheduled start time and a duration in minutes.
export function addMinutesToTime(time: string, minutes: number): string {
  const [hours = NaN, mins = NaN] = time.split(':').map(Number);
  const wrapped = (((hours * 60 + mins + minutes) % 1440) + 1440) % 1440;
  return `${String(Math.floor(wrapped / 60)).padStart(2, '0')}:${String(wrapped % 60).padStart(2, '0')}`;
}

// ISO-8601 week number (weeks start on Monday; week 1 is the week containing the year's first
// Thursday), so 2026-12-31 is week 53 and 2027-01-04 is week 1. Computed on UTC components so the
// local time zone and DST never shift the day.
export function isoWeekNumber(isoDate: string): number {
  const [year = NaN, month = NaN, day = NaN] = isoDate.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  const weekday = date.getUTCDay() || 7;
  // Move to the Thursday of this week; its year is the ISO week-numbering year.
  date.setUTCDate(date.getUTCDate() + 4 - weekday);
  const yearStart = Date.UTC(date.getUTCFullYear(), 0, 1);
  return Math.ceil(((date.getTime() - yearStart) / 86_400_000 + 1) / 7);
}

// The first date on or after isoDate that falls on `weekday` -- isoDate itself when it already
// matches.
export function nextWeekdayOnOrAfter(isoDate: string, weekday: DayOfWeek): string {
  const offset = (dayOfWeekIndex(weekday) - parseIsoDate(isoDate).getDay() + 7) % 7;
  return addDaysIso(isoDate, offset);
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const TIME = /^\d{2}:\d{2}$/;

// Minutes since the epoch for a wall-clock date and "HH:mm" time, read as UTC so DST never adds
// or drops an hour; null when either part is blank or malformed.
function wallClockMinutes(isoDate: string, time: string): number | null {
  if (!ISO_DATE.test(isoDate) || !TIME.test(time)) {
    return null;
  }
  const [year = NaN, month = NaN, day = NaN] = isoDate.split('-').map(Number);
  const [hours = NaN, minutes = NaN] = time.split(':').map(Number);
  return Date.UTC(year, month - 1, day, hours, minutes) / 60_000;
}

export interface DateTimeParts {
  date: string;
  time: string;
}

// The end of a date + time range after its start moved from `oldStart` to `newStart`, moved by the
// same amount so the range keeps its length (9:00-9:30 with the start moved to 10:00 ends at
// 10:30, crossing into the next day when needed). Left as it is when any part is blank.
export function shiftRangeEnd(
  oldStart: DateTimeParts,
  newStart: DateTimeParts,
  end: DateTimeParts,
): DateTimeParts {
  const from = wallClockMinutes(oldStart.date, oldStart.time);
  const to = wallClockMinutes(newStart.date, newStart.time);
  const endAt = wallClockMinutes(end.date, end.time);
  if (from === null || to === null || endAt === null) {
    return end;
  }
  const shifted = new Date((endAt + to - from) * 60_000);
  return {
    date: shifted.toISOString().slice(0, 10),
    time: shifted.toISOString().slice(11, 16),
  };
}

// shiftRangeEnd for a range of whole days.
export function shiftRangeEndDate(oldStart: string, newStart: string, end: string): string {
  return shiftRangeEnd(
    { date: oldStart, time: '00:00' },
    { date: newStart, time: '00:00' },
    { date: end, time: '00:00' },
  ).date;
}

// shiftRangeEnd for a window of wall-clock times with no date, which may cross midnight.
export function shiftRangeEndTime(oldStart: string, newStart: string, end: string): string {
  if (!TIME.test(oldStart) || !TIME.test(newStart) || !TIME.test(end)) {
    return end;
  }
  const minutesOfDay = (time: string): number => {
    const [hours = NaN, minutes = NaN] = time.split(':').map(Number);
    return hours * 60 + minutes;
  };
  return addMinutesToTime(end, minutesOfDay(newStart) - minutesOfDay(oldStart));
}
