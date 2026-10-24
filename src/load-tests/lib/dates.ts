// ISO dates (yyyy-MM-dd) relative to today. UTC is close enough: every seeded task, schedule and
// meal plan starts well before today and repeats daily, so a date a few hours off the family's own
// Europe/Copenhagen day still has an occurrence.

export function daysFrom(offset: number): string {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + offset);
  return date.toISOString().slice(0, 10);
}

export const today = (): string => daysFrom(0);
