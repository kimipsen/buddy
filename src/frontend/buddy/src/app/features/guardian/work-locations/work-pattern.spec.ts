import { describe, expect, it } from 'vitest';

import { anchorForCurrentWeek, cycleWeekOf, patternKey } from './work-pattern';

describe('work pattern helpers', () => {
  it.each([
    ['2026-09-28', 0],
    ['2026-10-04', 0],
    ['2026-10-05', 1],
    ['2026-10-12', 0],
    ['2026-09-27', 1],
    ['2026-09-21', 1],
  ])('puts %s in cycle week %i of a two-week cycle anchored on 2026-09-28', (date, expected) => {
    expect(cycleWeekOf('2026-09-28', 2, date)).toBe(expected);
  });

  it('floors before the anchor in longer cycles', () => {
    expect(cycleWeekOf('2026-09-28', 3, '2026-09-14')).toBe(1);
  });

  it('counts real weeks across a DST change', () => {
    // Europe/Copenhagen leaves summer time on 2026-10-25.
    expect(cycleWeekOf('2026-10-19', 2, '2026-10-26')).toBe(1);
  });

  it('picks the anchor that makes this week the chosen cycle week', () => {
    expect(anchorForCurrentWeek('2026-10-01', 0)).toBe('2026-09-28');
    expect(anchorForCurrentWeek('2026-10-01', 1)).toBe('2026-09-21');
    expect(cycleWeekOf(anchorForCurrentWeek('2026-10-01', 3), 4, '2026-10-01')).toBe(3);
  });

  it('builds a stable key per week and weekday', () => {
    expect(patternKey(1, 0)).toBe('1|0');
  });
});
