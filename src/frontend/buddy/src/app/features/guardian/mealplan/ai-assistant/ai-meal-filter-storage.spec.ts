import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { readLastMealFilter, writeLastMealFilter } from './ai-meal-filter-storage';

describe('ai-meal-filter-storage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('reads no filter when nothing is stored', () => {
    expect(readLastMealFilter()).toEqual({ ratedOnly: false, servedWithin: 'Any' });
  });

  it('round-trips a stored filter', () => {
    writeLastMealFilter({ ratedOnly: true, servedWithin: 'Last90Days' });

    expect(readLastMealFilter()).toEqual({ ratedOnly: true, servedWithin: 'Last90Days' });
  });

  it('falls back field by field for unexpected stored values', () => {
    localStorage.setItem(
      'buddy_ai_meal_filter',
      JSON.stringify({ ratedOnly: 'yes', servedWithin: 'Last45Days' }),
    );

    expect(readLastMealFilter()).toEqual({ ratedOnly: false, servedWithin: 'Any' });
  });

  it.each([
    [30, 'Last30Days'],
    [60, 'Last60Days'],
    [90, 'Last90Days'],
  ])('reads a day count stored before enums were sent by name (%i)', (days, window) => {
    localStorage.setItem(
      'buddy_ai_meal_filter',
      JSON.stringify({ ratedOnly: true, servedWithin: days }),
    );

    expect(readLastMealFilter()).toEqual({ ratedOnly: true, servedWithin: window });
  });

  it('falls back to Any for a day count that was never a window', () => {
    localStorage.setItem('buddy_ai_meal_filter', JSON.stringify({ servedWithin: 45 }));

    expect(readLastMealFilter().servedWithin).toBe('Any');
  });

  it('reads no filter when the stored value is not JSON', () => {
    localStorage.setItem('buddy_ai_meal_filter', '{not json');

    expect(readLastMealFilter()).toEqual({ ratedOnly: false, servedWithin: 'Any' });
  });

  it('ignores storage that throws', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() =>
      writeLastMealFilter({ ratedOnly: true, servedWithin: 'Last30Days' }),
    ).not.toThrow();
  });
});
