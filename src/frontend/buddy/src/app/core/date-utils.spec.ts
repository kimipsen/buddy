import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { browserTimeZoneId, listTimeZoneIds, toIsoDate, toIsoDateInTimeZone, todayIsoDate, toTimeInTimeZone } from './date-utils';

describe('toIsoDate', () => {
  it('formats a local date as yyyy-MM-dd', () => {
    // Constructed via the local-component Date constructor, so getFullYear/getMonth/getDate
    // return exactly these values regardless of the host machine's own time zone.
    expect(toIsoDate(new Date(2024, 2, 5, 13, 45, 0))).toBe('2024-03-05');
  });

  it('zero-pads single-digit months and days', () => {
    expect(toIsoDate(new Date(2024, 0, 9, 0, 0, 0))).toBe('2024-01-09');
  });

  it('does not roll the date based on the time-of-day component', () => {
    expect(toIsoDate(new Date(2024, 11, 31, 23, 59, 59))).toBe('2024-12-31');
    expect(toIsoDate(new Date(2024, 11, 31, 0, 0, 0))).toBe('2024-12-31');
  });

  it('handles a leap-year boundary', () => {
    expect(toIsoDate(new Date(2024, 1, 29))).toBe('2024-02-29');
  });
});

describe('todayIsoDate', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('reads the current local date', () => {
    // setSystemTime is given the same local-component constructor as the assertion above, so the
    // expectation is independent of whichever time zone the test runner's host happens to use.
    vi.setSystemTime(new Date(2025, 6, 4, 8, 15, 0));
    expect(todayIsoDate()).toBe('2025-07-04');
  });

  it('tracks a different fixed instant', () => {
    vi.setSystemTime(new Date(2025, 11, 31, 23, 0, 0));
    expect(todayIsoDate()).toBe('2025-12-31');
  });
});

describe('listTimeZoneIds', () => {
  it('returns the IANA zone ids sorted ascending by locale comparison', () => {
    const ids = listTimeZoneIds();
    const expected = [...Intl.supportedValuesOf('timeZone')].sort((a, b) => a.localeCompare(b));

    expect(ids).toEqual(expected);
    expect(ids).toContain('Europe/Copenhagen');
    expect(ids).toContain('America/New_York');
  });

  it('returns the same cached array instance on repeated calls', () => {
    expect(listTimeZoneIds()).toBe(listTimeZoneIds());
  });
});

describe('browserTimeZoneId', () => {
  it('returns the resolved Intl time zone for the current environment', () => {
    expect(browserTimeZoneId()).toBe(Intl.DateTimeFormat().resolvedOptions().timeZone);
  });
});

describe('toIsoDateInTimeZone', () => {
  it('renders the calendar date for the given zone (UTC)', () => {
    expect(toIsoDateInTimeZone(new Date('2024-06-15T12:00:00Z'), 'UTC')).toBe('2024-06-15');
  });

  it('rolls forward to the next calendar day in a zone far ahead of UTC', () => {
    // Pacific/Kiritimati is UTC+14, so late evening UTC is already the next day there.
    expect(toIsoDateInTimeZone(new Date('2024-06-15T23:30:00Z'), 'Pacific/Kiritimati')).toBe('2024-06-16');
  });

  it('rolls back to the previous calendar day in a zone far behind UTC', () => {
    // Pacific/Honolulu is UTC-10, so just after UTC midnight is still the previous day there.
    expect(toIsoDateInTimeZone(new Date('2024-01-01T02:00:00Z'), 'Pacific/Honolulu')).toBe('2023-12-31');
  });

  it('reflects the DST offset change for the same wall-clock UTC time across seasons', () => {
    // 22:30 UTC stays within the same Copenhagen day in winter (UTC+1 -> 23:30 local) but crosses
    // into the next Copenhagen day in summer (UTC+2 -> 00:30 local), so the same time-of-day input
    // in different seasons must resolve to different local calendar dates purely from the DST shift.
    expect(toIsoDateInTimeZone(new Date('2024-01-15T22:30:00Z'), 'Europe/Copenhagen')).toBe('2024-01-15');
    expect(toIsoDateInTimeZone(new Date('2024-07-15T22:30:00Z'), 'Europe/Copenhagen')).toBe('2024-07-16');
  });
});

describe('toTimeInTimeZone', () => {
  it('renders a zero-padded 24-hour HH:mm for the given zone', () => {
    expect(toTimeInTimeZone(new Date('2024-06-15T21:45:00Z'), 'Europe/Copenhagen')).toBe('23:45');
  });

  it('crosses midnight into the next local day without leaking a date component', () => {
    // 23:45 UTC in winter Copenhagen (UTC+1) is 00:45 the next local day.
    expect(toTimeInTimeZone(new Date('2024-01-15T23:45:00Z'), 'Europe/Copenhagen')).toBe('00:45');
  });

  it('supports a half-hour UTC offset zone', () => {
    expect(toTimeInTimeZone(new Date('2024-01-01T00:00:00Z'), 'Asia/Calcutta')).toBe('05:30');
  });

  it('zero-pads a single-digit hour', () => {
    expect(toTimeInTimeZone(new Date('2024-01-01T15:05:00Z'), 'Pacific/Honolulu')).toBe('05:05');
  });

  it('renders midnight as 00:00 rather than 24:00', () => {
    expect(toTimeInTimeZone(new Date('2024-01-01T00:07:00Z'), 'UTC')).toBe('00:07');
  });
});
