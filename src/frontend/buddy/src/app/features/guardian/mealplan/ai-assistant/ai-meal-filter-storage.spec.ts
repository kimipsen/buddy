import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { readLastMealFilter, writeLastMealFilter } from './ai-meal-filter-storage';

describe('ai-meal-filter-storage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('reads no filter when nothing is stored', () => {
    expect(readLastMealFilter()).toEqual({ ratedOnly: false, servedWithin: 0 });
  });

  it('round-trips a stored filter', () => {
    writeLastMealFilter({ ratedOnly: true, servedWithin: 90 });

    expect(readLastMealFilter()).toEqual({ ratedOnly: true, servedWithin: 90 });
  });

  it('falls back field by field for unexpected stored values', () => {
    localStorage.setItem(
      'buddy_ai_meal_filter',
      JSON.stringify({ ratedOnly: 'yes', servedWithin: 45 }),
    );

    expect(readLastMealFilter()).toEqual({ ratedOnly: false, servedWithin: 0 });
  });

  it('reads no filter when the stored value is not JSON', () => {
    localStorage.setItem('buddy_ai_meal_filter', '{not json');

    expect(readLastMealFilter()).toEqual({ ratedOnly: false, servedWithin: 0 });
  });

  it('ignores storage that throws', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() => writeLastMealFilter({ ratedOnly: true, servedWithin: 30 })).not.toThrow();
  });
});
