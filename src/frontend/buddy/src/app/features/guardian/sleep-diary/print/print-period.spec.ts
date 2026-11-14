import { describe, expect, it } from 'vitest';

import { isIsoDate, MAX_PRINT_NIGHTS, printPeriod } from './print-period';

describe('isIsoDate', () => {
  it.each(['2026-03-02', '2024-02-29', '2026-12-31'])('accepts %s', (value) => {
    expect(isIsoDate(value)).toBe(true);
  });

  it.each([
    '',
    '2026-3-2',
    '2026-02-30',
    '2026-13-01',
    '2026-00-10',
    '2026-04-31',
    '2025-02-29',
    '0002-08-01',
    '2026-03-02T00:00',
    'not a date',
  ])('rejects %j', (value) => {
    expect(isIsoDate(value)).toBe(false);
  });
});

describe('printPeriod', () => {
  it('lists every night with both ends included', () => {
    expect(printPeriod('2026-03-02', '2026-03-04')).toEqual({
      kind: 'valid',
      from: '2026-03-02',
      to: '2026-03-04',
      days: ['2026-03-02', '2026-03-03', '2026-03-04'],
    });
  });

  it('allows one night', () => {
    const period = printPeriod('2026-03-02', '2026-03-02');
    expect(period).toMatchObject({ kind: 'valid', days: ['2026-03-02'] });
  });

  it('allows exactly 92 nights and refuses 93', () => {
    const ok = printPeriod('2026-01-01', '2026-04-02');
    expect(ok.kind).toBe('valid');
    expect(ok.kind === 'valid' && ok.days).toHaveLength(MAX_PRINT_NIGHTS);
    expect(ok.kind === 'valid' && ok.days.at(-1)).toBe('2026-04-02');
    expect(printPeriod('2026-01-01', '2026-04-03')).toEqual({ kind: 'tooLong' });
  });

  // EX-003: 1 Aug to 31 Oct crosses the 25 Oct DST change and is 92 nights.
  it('counts calendar days across the autumn DST change (Europe/Copenhagen)', () => {
    const period = printPeriod('2026-08-01', '2026-10-31');
    expect(period.kind).toBe('valid');
    expect(period.kind === 'valid' && period.days).toHaveLength(92);
    expect(period.kind === 'valid' && period.days.at(-1)).toBe('2026-10-31');
  });

  // 28 Feb to 31 May is 93 nights across the 29 Mar spring change: too long, not truncated.
  it('refuses 93 nights across the spring DST change', () => {
    expect(printPeriod('2026-02-28', '2026-05-31')).toEqual({ kind: 'tooLong' });
  });

  it('keeps every row across the spring DST change, one date per day', () => {
    const period = printPeriod('2026-03-27', '2026-03-31');
    expect(period.kind === 'valid' && period.days).toEqual([
      '2026-03-27',
      '2026-03-28',
      '2026-03-29',
      '2026-03-30',
      '2026-03-31',
    ]);
  });

  it('reports a reversed period', () => {
    expect(printPeriod('2026-03-05', '2026-03-04')).toEqual({ kind: 'reversed' });
  });

  it.each([
    ['', '2026-03-04'],
    ['2026-03-04', ''],
    ['', ''],
    ['2026-02-30', '2026-03-04'],
    ['2026-03-01', '2026-02-30'],
    ['0002-08-01', '2026-03-04'],
  ])('reports a missing period for %j to %j', (from, to) => {
    expect(printPeriod(from, to)).toEqual({ kind: 'missing' });
  });
});
