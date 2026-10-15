import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';

import { CalendarOccurrence } from './calendars.service';
import {
  STARTER_FIRSTS,
  STARTER_THENS,
  THEN_LENGTHS,
  boardForChild,
  hasBoard,
  isBoardChanged,
  isStale,
  isThenTimeUp,
  thenDeadlineMs,
  thenTimerState,
  todaysTaskCards,
} from './first-then';
import { FirstThenBoard } from './first-then.service';
import { FlatOccurrence, nestOccurrence } from '../../testing/occurrence-fixture';

const COPENHAGEN = 'Europe/Copenhagen';
const UNLOCKED_AT = '2026-10-14T15:00:00+00:00';
const UNLOCKED_MS = Date.parse(UNLOCKED_AT);
const MINUTE = 60_000;

function board(overrides: Partial<FirstThenBoard> = {}): FirstThenBoard {
  return {
    revision: 1,
    state: 'Waiting',
    first: { title: 'Homework', icon: '📚' },
    then: { title: 'iPad', icon: '📱' },
    thenMinutes: 20,
    unlockedAt: null,
    lastModifiedAt: '2026-10-14T14:00:00+00:00',
    lastModifiedByName: null,
    ...overrides,
  };
}

const unlocked = (overrides: Partial<FirstThenBoard> = {}) =>
  board({ state: 'Unlocked', unlockedAt: UNLOCKED_AT, ...overrides });

function occurrence(
  overrides: Partial<FlatOccurrence<CalendarOccurrence>> = {},
): CalendarOccurrence {
  return nestOccurrence<CalendarOccurrence>({
    itemId: 'item-1',
    kind: 'Task',
    title: 'Maths p. 12',
    icon: '✏️',
    iconOverride: null,
    color: '#112233',
    startsAt: null,
    endsAt: null,
    dueAt: '2026-10-14T16:00:00Z',
    isAllDay: false,
    isCompleted: false,
    isSkipped: false,
    isRepeating: false,
    createdBy: 'guardian-1',
    lastModifiedBy: 'guardian-1',
    assignedTo: 'child-1',
    calendarId: 'cal-1',
    calendarName: 'Home',
    parentTitle: null,
    subtaskId: null,
    parentIcon: null,
    ...overrides,
  });
}

describe('first-then', () => {
  describe('hasBoard', () => {
    it('is true for a waiting or unlocked board with both cards', () => {
      expect(hasBoard(board())).toBe(true);
      expect(hasBoard(unlocked())).toBe(true);
    });

    it('is false for no board, an ended board, or a board missing a card', () => {
      expect(hasBoard(null)).toBe(false);
      expect(hasBoard(undefined)).toBe(false);
      expect(hasBoard(board({ state: 'None', first: null, then: null }))).toBe(false);
      expect(hasBoard(board({ first: null }))).toBe(false);
      expect(hasBoard(board({ then: null }))).toBe(false);
    });
  });

  describe('isStale', () => {
    it('is false for a board changed today on the child’s clock', () => {
      expect(isStale(board(), '2026-10-14', COPENHAGEN)).toBe(false);
    });

    it('is true for a board last changed before today', () => {
      expect(isStale(board(), '2026-10-15', COPENHAGEN)).toBe(true);
    });

    it('reads the date in the child’s time zone, not UTC', () => {
      // 22:30 UTC on the 14th is already the 15th in Copenhagen (UTC+2).
      const late = board({ lastModifiedAt: '2026-10-14T22:30:00+00:00' });
      expect(isStale(late, '2026-10-15', COPENHAGEN)).toBe(false);
      expect(isStale(late, '2026-10-15', 'UTC')).toBe(true);
    });

    it('is false when the board has never been changed', () => {
      expect(isStale(board({ lastModifiedAt: null }), '2026-10-15', COPENHAGEN)).toBe(false);
    });
  });

  describe('boardForChild', () => {
    it('shows a current board from today', () => {
      const current = board();
      expect(boardForChild(current, '2026-10-14', COPENHAGEN)).toBe(current);
    });

    it('treats a board left over from yesterday, or no board, as nothing', () => {
      expect(boardForChild(board(), '2026-10-15', COPENHAGEN)).toBeNull();
      expect(boardForChild(board({ state: 'None' }), '2026-10-14', COPENHAGEN)).toBeNull();
      expect(boardForChild(null, '2026-10-14', COPENHAGEN)).toBeNull();
    });
  });

  describe('the Then countdown', () => {
    it('has a deadline only for an unlocked board with a length', () => {
      expect(thenDeadlineMs(unlocked())).toBe(UNLOCKED_MS + 20 * MINUTE);
      expect(thenDeadlineMs(board())).toBeNull();
      expect(thenDeadlineMs(unlocked({ thenMinutes: null }))).toBeNull();
      expect(thenDeadlineMs(unlocked({ unlockedAt: null }))).toBeNull();
    });

    it('is time’s up from the deadline on, never before or without a length', () => {
      expect(isThenTimeUp(unlocked(), UNLOCKED_MS + 20 * MINUTE - 1)).toBe(false);
      expect(isThenTimeUp(unlocked(), UNLOCKED_MS + 20 * MINUTE)).toBe(true);
      expect(isThenTimeUp(unlocked({ thenMinutes: null }), UNLOCKED_MS + 999 * MINUTE)).toBe(false);
    });

    it('builds a running single-step timer state counting down to the deadline', () => {
      const state = thenTimerState(unlocked(), UNLOCKED_MS + 5 * MINUTE);

      expect(state).toEqual({
        current: null,
        next: null,
        remainingMs: 15 * MINUTE,
        budgetMs: 20 * MINUTE,
        runRemainingMs: 15 * MINUTE,
        runBudgetMs: 20 * MINUTE,
        deadlineMs: UNLOCKED_MS + 20 * MINUTE,
        startsAtMs: null,
        phase: 'running',
        childStarted: false,
      });
    });

    it('is time’s up with nothing left once the deadline has passed', () => {
      const state = thenTimerState(unlocked(), UNLOCKED_MS + 25 * MINUTE);

      expect(state?.phase).toBe('timesUp');
      expect(state?.remainingMs).toBe(0);
      expect(state?.deadlineMs).toBeNull();
    });

    it('never shows more than the whole length, even on a clock that is behind', () => {
      expect(thenTimerState(unlocked(), UNLOCKED_MS - 5 * MINUTE)?.remainingMs).toBe(20 * MINUTE);
    });

    it('has no timer without a length or before unlocking', () => {
      expect(thenTimerState(unlocked({ thenMinutes: null }), UNLOCKED_MS)).toBeNull();
      expect(thenTimerState(board(), UNLOCKED_MS)).toBeNull();
    });
  });

  describe('isBoardChanged', () => {
    it('recognises the board-changed conflict only', () => {
      const changed = new HttpErrorResponse({
        status: 409,
        error: { code: 'first_then_board_changed' },
      });
      const otherConflict = new HttpErrorResponse({
        status: 409,
        error: { code: 'concurrency_conflict' },
      });

      expect(isBoardChanged(changed)).toBe(true);
      expect(isBoardChanged(otherConflict)).toBe(false);
      expect(isBoardChanged(new HttpErrorResponse({ status: 409, error: null }))).toBe(false);
      expect(
        isBoardChanged(
          new HttpErrorResponse({ status: 400, error: { code: 'first_then_board_changed' } }),
        ),
      ).toBe(false);
      expect(isBoardChanged(new Error('offline'))).toBe(false);
    });
  });

  describe('todaysTaskCards', () => {
    it('offers the child’s open tasks with their effective icon', () => {
      const cards = todaysTaskCards(
        [occurrence(), occurrence({ itemId: 'item-2', title: 'Piano', iconOverride: '🎹' })],
        'child-1',
      );

      expect(cards).toEqual([
        { title: 'Maths p. 12', icon: '✏️' },
        { title: 'Piano', icon: '🎹' },
      ]);
    });

    it('leaves out done tasks, events, and tasks for someone else', () => {
      const cards = todaysTaskCards(
        [
          occurrence({ isCompleted: true }),
          occurrence({ itemId: 'event', kind: 'Event', title: 'Football' }),
          occurrence({ itemId: 'sibling', assignedTo: 'child-2', title: 'Sibling task' }),
          occurrence({ itemId: 'unassigned', assignedTo: null, title: 'Anyone' }),
        ],
        'child-1',
      );

      expect(cards).toEqual([]);
    });

    it('offers a routine once by its parent title while any step is open', () => {
      const step = (subtaskId: string, isCompleted: boolean) =>
        occurrence({
          itemId: 'routine-1',
          title: `Step ${subtaskId}`,
          parentTitle: 'Bedtime',
          parentIcon: '🛏️',
          subtaskId,
          isCompleted,
          startsAt: '2026-10-14T18:00:00Z',
          endsAt: '2026-10-14T18:05:00Z',
          dueAt: null,
        });

      expect(todaysTaskCards([step('a', true), step('b', false)], 'child-1')).toEqual([
        { title: 'Bedtime', icon: '🛏️' },
      ]);
      expect(todaysTaskCards([step('a', true), step('b', true)], 'child-1')).toEqual([]);
    });

    it('is distinct by title, ignoring case', () => {
      const cards = todaysTaskCards(
        [occurrence(), occurrence({ itemId: 'item-2', title: 'MATHS P. 12' })],
        'child-1',
      );

      expect(cards).toEqual([{ title: 'Maths p. 12', icon: '✏️' }]);
    });
  });

  it('ships seven starters on each side and the lengths the form offers', () => {
    expect(STARTER_FIRSTS).toHaveLength(7);
    expect(STARTER_THENS).toHaveLength(7);
    expect(THEN_LENGTHS).toEqual([null, 5, 10, 15, 20, 30, 45, 60]);
  });
});
