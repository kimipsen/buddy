import { describe, expect, it } from 'vitest';

import { ownTimerState, pauseOwnTimer, resumeOwnTimer, startOwnTimer } from './own-timer';

const MINUTE = 60_000;

describe('own timer', () => {
  it('runs from the full length down to its deadline', () => {
    const timer = startOwnTimer(5, 1_000);

    expect(timer).toEqual({ budgetMs: 5 * MINUTE, startedAtMs: 1_000, pausedAtMs: null });
    expect(ownTimerState(timer, 1_000)).toEqual({
      current: null,
      next: null,
      remainingMs: 5 * MINUTE,
      budgetMs: 5 * MINUTE,
      runRemainingMs: 5 * MINUTE,
      runBudgetMs: 5 * MINUTE,
      deadlineMs: 1_000 + 5 * MINUTE,
      startsAtMs: null,
      phase: 'running',
      childStarted: true,
    });

    const later = ownTimerState(timer, 1_000 + 2 * MINUTE);
    expect(later.remainingMs).toBe(3 * MINUTE);
    expect(later.runRemainingMs).toBe(3 * MINUTE);
  });

  it("is time's up once the length has passed, never below zero", () => {
    const timer = startOwnTimer(2, 0);

    expect(ownTimerState(timer, 2 * MINUTE - 1).phase).toBe('running');

    const up = ownTimerState(timer, 3 * MINUTE);
    expect(up.phase).toBe('timesUp');
    expect(up.remainingMs).toBe(0);
    expect(up.deadlineMs).toBeNull();
  });

  it('never shows more than the full length, even with a clock that went back', () => {
    expect(ownTimerState(startOwnTimer(2, 10_000), 0).remainingMs).toBe(2 * MINUTE);
  });

  it('stands still while paused and continues from there when resumed', () => {
    const paused = pauseOwnTimer(startOwnTimer(10, 0), 4 * MINUTE);

    expect(paused.pausedAtMs).toBe(4 * MINUTE);
    const whilePaused = ownTimerState(paused, 9 * MINUTE);
    expect(whilePaused.phase).toBe('paused');
    expect(whilePaused.remainingMs).toBe(6 * MINUTE);
    expect(whilePaused.deadlineMs).toBeNull();

    const resumed = resumeOwnTimer(paused, 9 * MINUTE);
    expect(resumed).toEqual({ budgetMs: 10 * MINUTE, startedAtMs: 5 * MINUTE, pausedAtMs: null });
    expect(ownTimerState(resumed, 9 * MINUTE).remainingMs).toBe(6 * MINUTE);
    expect(ownTimerState(resumed, 9 * MINUTE).deadlineMs).toBe(15 * MINUTE);
  });

  it('ignores a second pause and a resume of a running timer', () => {
    const paused = pauseOwnTimer(startOwnTimer(10, 0), MINUTE);
    const running = startOwnTimer(10, 0);

    expect(pauseOwnTimer(paused, 2 * MINUTE)).toBe(paused);
    expect(resumeOwnTimer(running, 2 * MINUTE)).toBe(running);
  });
});
