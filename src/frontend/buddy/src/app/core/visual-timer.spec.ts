import { describe, expect, it } from 'vitest';

import { nestOccurrence } from '../../testing/occurrence-fixture';
import { CalendarOccurrence } from './calendars.service';
import { TaskRun, occurrenceKey } from './task-run';
import {
  CURRENT_RUN_GRACE_MS,
  CURRENT_RUN_LOOK_AHEAD_MS,
  TimerAnchor,
  currentRun,
  minutesLeft,
  pauseAnchor,
  reanchor,
  resumeAnchor,
  startAnchor,
  timerState,
} from './visual-timer';

const MINUTE = 60_000;
const DAY = '2026-10-12';

function at(time: string): number {
  return Date.parse(`${DAY}T${time}:00Z`);
}

function subtask(
  itemId: string,
  subtaskId: string,
  start: string,
  end: string,
  isCompleted = false,
): CalendarOccurrence {
  return nestOccurrence<CalendarOccurrence>({
    itemId,
    kind: 'Task',
    title: subtaskId,
    icon: '🧹',
    iconOverride: null,
    color: '#0f0',
    startsAt: `${DAY}T${start}:00Z`,
    endsAt: `${DAY}T${end}:00Z`,
    isAllDay: false,
    isCompleted,
    isSkipped: false,
    isRepeating: false,
    createdBy: 'g',
    lastModifiedBy: 'g',
    assignedTo: 'c',
    calendarId: 'cal',
    calendarName: 'Home',
    parentTitle: 'Morning',
    subtaskId,
  });
}

function run(itemId: string, subtasks: CalendarOccurrence[]): TaskRun {
  return {
    itemId,
    parentTitle: 'Morning',
    calendarId: 'cal',
    calendarName: 'Home',
    color: '#0f0',
    icon: '🌅',
    subtasks: subtasks as TaskRun['subtasks'],
  };
}

// Get dressed 07:00-07:05, Eat breakfast 07:05-07:20, Brush teeth 07:20-07:23.
function morning(done: boolean[] = [false, false, false]): TaskRun {
  return run('morning', [
    subtask('morning', 'dress', '07:00', '07:05', done[0]),
    subtask('morning', 'eat', '07:05', '07:20', done[1]),
    subtask('morning', 'teeth', '07:20', '07:23', done[2]),
  ]);
}

function anchorOn(
  target: TaskRun,
  index: number,
  startedAt: number,
  pausedAt: number | null = null,
) {
  return {
    subtaskKey: occurrenceKey(target.subtasks[index]!),
    startedAtMs: startedAt,
    pausedAtMs: pausedAt,
  } satisfies TimerAnchor;
}

describe('currentRun', () => {
  const evening = run('evening', [subtask('evening', 'bath', '19:00', '19:20')]);

  it('picks a run that has started and is still within its window', () => {
    expect(currentRun([morning(), evening], at('07:02'))?.itemId).toBe('morning');
  });

  it('keeps a run current until the grace period after its last subtask ends, and no longer', () => {
    const end = at('07:23');
    expect(currentRun([morning()], end + CURRENT_RUN_GRACE_MS)?.itemId).toBe('morning');
    expect(currentRun([morning()], end + CURRENT_RUN_GRACE_MS + 1)).toBeNull();
  });

  it('keeps a late run around for half an hour and shows the next one a quarter of an hour early', () => {
    expect(currentRun([morning()], at('07:53'))?.itemId).toBe('morning');
    expect(currentRun([morning()], at('07:54'))).toBeNull();
    expect(currentRun([evening], at('18:45'))?.itemId).toBe('evening');
    expect(currentRun([evening], at('18:44'))).toBeNull();
  });

  it('counts a run as started from the instant its first subtask starts', () => {
    expect(currentRun([morning()], at('07:00'))?.itemId).toBe('morning');
  });

  it('keeps a partly done run current', () => {
    expect(currentRun([morning([true, false, false])], at('07:10'))?.itemId).toBe('morning');
  });

  it('looks ahead to a run starting within the look-ahead, and no further', () => {
    const start = at('19:00');
    expect(currentRun([evening], start - CURRENT_RUN_LOOK_AHEAD_MS)?.itemId).toBe('evening');
    expect(currentRun([evening], start - CURRENT_RUN_LOOK_AHEAD_MS - 1)).toBeNull();
  });

  it('prefers a started run over an upcoming one', () => {
    const soon = run('soon', [subtask('soon', 'x', '07:10', '07:30')]);
    expect(currentRun([soon, morning()], at('07:02'))?.itemId).toBe('morning');
  });

  it('takes the earlier of two overlapping runs', () => {
    const overlapping = run('other', [subtask('other', 'x', '06:55', '07:30')]);
    expect(currentRun([morning(), overlapping], at('07:02'))?.itemId).toBe('other');
    expect(currentRun([overlapping, morning()], at('07:02'))?.itemId).toBe('other');
  });

  it('keeps the first listed of two runs starting together', () => {
    const together = run('together', [subtask('together', 'x', '07:00', '07:30')]);
    expect(currentRun([morning(), together], at('07:02'))?.itemId).toBe('morning');
  });

  it('skips a finished run', () => {
    expect(currentRun([morning([true, true, true])], at('07:02'))).toBeNull();
  });

  it('keeps a child-started run current whatever the clock says', () => {
    expect(currentRun([morning(), evening], at('12:00'), 'morning')?.itemId).toBe('morning');
    expect(currentRun([morning([true, true, true])], at('07:02'), 'morning')).toBeNull();
  });

  it('ignores a run with an untimed subtask', () => {
    const plain = run('plain', [
      nestOccurrence<CalendarOccurrence>({
        ...subtask('plain', 'p', '07:00', '07:05'),
        timing: { kind: 1, dueAt: `${DAY}T07:00:00Z` },
        routine: null,
      }),
    ]);
    expect(currentRun([plain], at('07:01'))).toBeNull();

    const mixed = run('mixed', [subtask('mixed', 'timed', '07:00', '07:05'), ...plain.subtasks]);
    expect(currentRun([mixed], at('07:01'))).toBeNull();
  });
});

describe('timerState (schedule-anchored)', () => {
  it('is upcoming with a full disc before the subtask window opens', () => {
    const state = timerState(morning(), at('06:50'));
    expect(state).toMatchObject({
      phase: 'upcoming',
      remainingMs: 5 * MINUTE,
      budgetMs: 5 * MINUTE,
      deadlineMs: null,
      startsAtMs: at('07:00'),
      childStarted: false,
    });
    expect(state.current?.routine?.subtaskId).toBe('dress');
    expect(state.next?.routine?.subtaskId).toBe('eat');
  });

  it('counts down to the window end while running', () => {
    const state = timerState(morning(), at('07:02'));
    expect(state).toMatchObject({
      phase: 'running',
      remainingMs: 3 * MINUTE,
      deadlineMs: at('07:05'),
      runRemainingMs: 3 * MINUTE + 15 * MINUTE + 3 * MINUTE,
      runBudgetMs: 23 * MINUTE,
    });
  });

  it('starts running the instant the window opens, with the whole budget left', () => {
    expect(timerState(morning(), at('07:00'))).toMatchObject({
      phase: 'running',
      remainingMs: 5 * MINUTE,
      deadlineMs: at('07:05'),
    });
  });

  it("is time's up the instant the window closes", () => {
    expect(timerState(morning(), at('07:05'))).toMatchObject({
      phase: 'timesUp',
      remainingMs: 0,
      deadlineMs: null,
    });
  });

  it("is time's up at the window end, without ever skipping the unticked step", () => {
    const state = timerState(morning(), at('07:12'));
    expect(state.phase).toBe('timesUp');
    expect(state.remainingMs).toBe(0);
    expect(state.current?.routine?.subtaskId).toBe('dress');
    expect(state.runRemainingMs).toBe(18 * MINUTE);
  });

  it('moves to the first incomplete subtask, not the one whose window holds now', () => {
    const state = timerState(morning([true, false, false]), at('07:02'));
    expect(state.current?.routine?.subtaskId).toBe('eat');
    expect(state.phase).toBe('upcoming');
    expect(state.remainingMs).toBe(15 * MINUTE);
  });

  it('leaves ticked later subtasks out of the run remaining', () => {
    const state = timerState(morning([false, true, false]), at('07:02'));
    expect(state.runRemainingMs).toBe(3 * MINUTE + 3 * MINUTE);
    expect(state.next?.routine?.subtaskId).toBe('teeth');
  });

  it('is done once every subtask is ticked', () => {
    expect(timerState(morning([true, true, true]), at('07:02'))).toMatchObject({
      phase: 'done',
      current: null,
      next: null,
      remainingMs: 0,
      runRemainingMs: 0,
      runBudgetMs: 23 * MINUTE,
      childStarted: false,
    });
  });

  it('brings an unticked subtask back as the current one', () => {
    expect(timerState(morning([false, true, true]), at('07:02')).current?.routine?.subtaskId).toBe(
      'dress',
    );
  });
});

describe('timerState (child-started)', () => {
  it('gives the current subtask its full budget from the tap', () => {
    const target = morning();
    const state = timerState(target, at('07:12'), anchorOn(target, 0, at('07:11')));
    expect(state).toMatchObject({
      phase: 'running',
      remainingMs: 4 * MINUTE,
      deadlineMs: at('07:16'),
      childStarted: true,
    });
  });

  it("is time's up once the budget is used", () => {
    const target = morning();
    const state = timerState(target, at('07:20'), anchorOn(target, 0, at('07:11')));
    expect(state).toMatchObject({ phase: 'timesUp', remainingMs: 0, deadlineMs: null });
  });

  it('freezes the remaining time while paused', () => {
    const target = morning();
    const anchor = anchorOn(target, 0, at('07:11'), at('07:13'));
    expect(timerState(target, at('09:00'), anchor)).toMatchObject({
      phase: 'paused',
      remainingMs: 3 * MINUTE,
      deadlineMs: null,
    });
  });

  it("is time's up, not paused, when Pause came after the budget ran out", () => {
    const target = morning();
    const anchor = anchorOn(target, 0, at('07:00'), at('07:06'));
    expect(timerState(target, at('07:07'), anchor)).toMatchObject({
      phase: 'timesUp',
      remainingMs: 0,
    });
  });

  it('falls back to the schedule when the anchor belongs to another subtask', () => {
    const target = morning([true, false, false]);
    const state = timerState(target, at('07:10'), anchorOn(target, 0, at('07:09')));
    expect(state.childStarted).toBe(false);
    expect(state.deadlineMs).toBe(at('07:20'));
  });
});

describe('anchor transitions', () => {
  it('starts on the first incomplete subtask, and not at all on a finished run', () => {
    const target = morning([true, false, false]);
    expect(startAnchor(target, 5)).toEqual(anchorOn(target, 1, 5));
    expect(startAnchor(morning([true, true, true]), 5)).toBeUndefined();
  });

  it('keeps the remaining time across pause and resume', () => {
    const target = morning();
    const started = anchorOn(target, 0, at('07:00'));
    const paused = pauseAnchor(started, at('07:02'));
    expect(pauseAnchor(paused, at('07:30'))).toBe(paused);

    const resumed = resumeAnchor(paused, at('07:10'));
    expect(resumed).toEqual({ ...started, startedAtMs: at('07:08'), pausedAtMs: null });
    expect(resumeAnchor(resumed, at('07:20'))).toBe(resumed);
    expect(timerState(target, at('07:10'), resumed).remainingMs).toBe(3 * MINUTE);
  });

  it('chains to the next subtask with a full budget once the current one is ticked', () => {
    const anchor = anchorOn(morning(), 0, at('07:00'));
    const ticked = morning([true, false, false]);
    const next = reanchor(ticked, anchor, at('07:03'));
    expect(next).toEqual(anchorOn(ticked, 1, at('07:03')));
    expect(timerState(ticked, at('07:04'), next)).toMatchObject({
      phase: 'running',
      remainingMs: 14 * MINUTE,
    });
  });

  it('keeps the anchor when the current subtask did not change, and drops it when the run is done', () => {
    const target = morning();
    const anchor = anchorOn(target, 0, at('07:00'));
    expect(reanchor(morning([false, true, false]), anchor, at('07:03'))).toBe(anchor);
    expect(reanchor(morning([true, true, true]), anchor, at('07:03'))).toBeUndefined();
  });

  it('gives an unticked subtask a fresh budget from the untick', () => {
    const anchor = anchorOn(morning([true, false, false]), 1, at('07:00'));
    const unticked = morning();
    expect(reanchor(unticked, anchor, at('07:04'))).toEqual(anchorOn(unticked, 0, at('07:04')));
  });
});

describe('minutesLeft', () => {
  it('rounds up to whole minutes and never goes below zero', () => {
    expect(minutesLeft(4 * MINUTE + 1)).toBe(5);
    expect(minutesLeft(4 * MINUTE)).toBe(4);
    expect(minutesLeft(1)).toBe(1);
    expect(minutesLeft(0)).toBe(0);
    expect(minutesLeft(-5)).toBe(0);
  });
});
