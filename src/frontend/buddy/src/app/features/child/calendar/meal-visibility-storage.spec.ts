import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { readShowMeals, writeShowMeals } from './meal-visibility-storage';

describe('meal-visibility-storage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('shows meals when nothing is stored', () => {
    expect(readShowMeals()).toBe(true);
  });

  it('round-trips a hidden and a shown meal plan', () => {
    writeShowMeals(false);
    expect(localStorage.getItem('buddy_child_calendar_show_meals')).toBe('false');
    expect(readShowMeals()).toBe(false);

    writeShowMeals(true);
    expect(readShowMeals()).toBe(true);
  });

  it('shows meals when storage cannot be read', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(readShowMeals()).toBe(true);
  });

  it('ignores a storage failure when writing', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() => writeShowMeals(false)).not.toThrow();
  });
});
