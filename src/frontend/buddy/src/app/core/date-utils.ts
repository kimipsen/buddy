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
