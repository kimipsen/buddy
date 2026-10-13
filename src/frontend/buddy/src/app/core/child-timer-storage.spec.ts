import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { clearTimerAnchor, readTimerAnchor, writeTimerAnchor } from './child-timer-storage';

describe('child-timer-storage', () => {
  const anchor = { subtaskKey: 'run-1:sub-1:2026-10-12', startedAtMs: 1000, pausedAtMs: null };

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('reads nothing when nothing is stored', () => {
    expect(readTimerAnchor('2026-10-12')).toBeNull();
  });

  it('round-trips a running and a paused timer for the same day', () => {
    writeTimerAnchor('run-1', '2026-10-12', anchor);
    expect(readTimerAnchor('2026-10-12')).toEqual({ itemId: 'run-1', anchor });

    const paused = { ...anchor, pausedAtMs: 2000 };
    writeTimerAnchor('run-1', '2026-10-12', paused);
    expect(readTimerAnchor('2026-10-12')).toEqual({ itemId: 'run-1', anchor: paused });
  });

  it('throws away an entry from another day', () => {
    writeTimerAnchor('run-1', '2026-10-11', anchor);

    expect(readTimerAnchor('2026-10-12')).toBeNull();
  });

  it('replaces the stored timer when another run starts', () => {
    writeTimerAnchor('run-1', '2026-10-12', anchor);
    writeTimerAnchor('run-2', '2026-10-12', anchor);

    expect(readTimerAnchor('2026-10-12')?.itemId).toBe('run-2');
  });

  it('clears the stored timer', () => {
    writeTimerAnchor('run-1', '2026-10-12', anchor);
    clearTimerAnchor();

    expect(localStorage.getItem('buddy_child_timer')).toBeNull();
    expect(readTimerAnchor('2026-10-12')).toBeNull();
  });

  it.each([
    ['garbage JSON', '{not json'],
    ['a missing item id', JSON.stringify({ date: '2026-10-12', anchor })],
    ['a missing anchor', JSON.stringify({ itemId: 'run-1', date: '2026-10-12' })],
    [
      'a non-numeric start',
      JSON.stringify({
        itemId: 'run-1',
        date: '2026-10-12',
        anchor: { ...anchor, startedAtMs: 'x' },
      }),
    ],
    [
      'a non-numeric pause',
      JSON.stringify({
        itemId: 'run-1',
        date: '2026-10-12',
        anchor: { ...anchor, pausedAtMs: 'x' },
      }),
    ],
    [
      'a missing subtask key',
      JSON.stringify({ itemId: 'run-1', date: '2026-10-12', anchor: { ...anchor, subtaskKey: 3 } }),
    ],
  ])('reads nothing from %s', (_case, stored) => {
    localStorage.setItem('buddy_child_timer', stored);

    expect(readTimerAnchor('2026-10-12')).toBeNull();
  });

  it('degrades to nothing stored when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() => writeTimerAnchor('run-1', '2026-10-12', anchor)).not.toThrow();
    expect(() => clearTimerAnchor()).not.toThrow();
    expect(readTimerAnchor('2026-10-12')).toBeNull();
  });
});
