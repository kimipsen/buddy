export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function todayIsoDate(): string {
  return toIsoDate(new Date());
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
