import { afterEach, describe, expect, it, vi } from 'vitest';

import { clearOwnTimer, readOwnTimer, writeOwnTimer } from './own-timer-storage';

const KEY = 'buddy_child_own_timer';
const TODAY = '2026-11-05';

describe('own timer storage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("round-trips today's timer, paused or not", () => {
    writeOwnTimer(TODAY, { budgetMs: 300_000, startedAtMs: 10, pausedAtMs: null });
    expect(JSON.parse(localStorage.getItem(KEY)!)).toEqual({
      date: TODAY,
      budgetMs: 300_000,
      startedAtMs: 10,
      pausedAtMs: null,
    });
    expect(readOwnTimer(TODAY)).toEqual({ budgetMs: 300_000, startedAtMs: 10, pausedAtMs: null });

    writeOwnTimer(TODAY, { budgetMs: 300_000, startedAtMs: 10, pausedAtMs: 20 });
    expect(readOwnTimer(TODAY)).toEqual({ budgetMs: 300_000, startedAtMs: 10, pausedAtMs: 20 });
  });

  it('throws away a timer from another day', () => {
    writeOwnTimer('2026-11-04', { budgetMs: 300_000, startedAtMs: 10, pausedAtMs: null });

    expect(readOwnTimer(TODAY)).toBeNull();
  });

  it.each([
    ['nothing stored', null],
    ['not JSON', '{'],
    ['no length', JSON.stringify({ date: TODAY, startedAtMs: 1, pausedAtMs: null })],
    [
      'a zero length',
      JSON.stringify({ date: TODAY, budgetMs: 0, startedAtMs: 1, pausedAtMs: null }),
    ],
    [
      'a text start',
      JSON.stringify({ date: TODAY, budgetMs: 1, startedAtMs: '1', pausedAtMs: null }),
    ],
    ['a text pause', JSON.stringify({ date: TODAY, budgetMs: 1, startedAtMs: 1, pausedAtMs: '2' })],
    ['no pause field', JSON.stringify({ date: TODAY, budgetMs: 1, startedAtMs: 1 })],
  ])('reads %s as no timer', (_, raw) => {
    if (raw !== null) {
      localStorage.setItem(KEY, raw);
    }

    expect(readOwnTimer(TODAY)).toBeNull();
  });

  it('clears the stored timer', () => {
    writeOwnTimer(TODAY, { budgetMs: 300_000, startedAtMs: 10, pausedAtMs: null });

    clearOwnTimer();

    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('carries on without storage', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('denied');
    });
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('denied');
    });

    expect(() =>
      writeOwnTimer(TODAY, { budgetMs: 1, startedAtMs: 1, pausedAtMs: null }),
    ).not.toThrow();
    expect(readOwnTimer(TODAY)).toBeNull();
    expect(() => clearOwnTimer()).not.toThrow();
  });
});
