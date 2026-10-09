import { describe, expect, it } from 'vitest';

import { CalendarOccurrence } from './calendars.service';
import { TaskRun, compareOccurrences, groupTaskRuns, isTaskRun, occurrenceKey } from './task-run';
import { FlatOccurrence, nestOccurrence } from '../../testing/occurrence-fixture';

describe('task-run', () => {
  function occurrence(
    overrides: Partial<FlatOccurrence<CalendarOccurrence>> = {},
  ): CalendarOccurrence {
    return nestOccurrence<CalendarOccurrence>({
      itemId: 'item-1',
      kind: 'Task',
      title: 'Brush teeth',
      icon: '🪥',
      iconOverride: null,
      color: '#112233',
      startsAt: '2026-08-27T08:00:00Z',
      endsAt: '2026-08-27T08:05:00Z',
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      calendarId: 'cal-1',
      calendarName: 'Home',
      parentTitle: null,
      subtaskId: null,
      parentIcon: null,
      ...overrides,
    });
  }

  describe('groupTaskRuns', () => {
    it('passes an ordinary event through unchanged, ungrouped', () => {
      const event = occurrence({
        itemId: 'event-1',
        kind: 'Event',
        parentTitle: null,
        subtaskId: null,
      });

      const entries = groupTaskRuns([event]);

      expect(entries).toEqual([event]);
    });

    it('passes a plain hand-entered task (no routine) through unchanged, ungrouped', () => {
      const task = occurrence({
        itemId: 'task-1',
        kind: 'Task',
        startsAt: null,
        dueAt: '2026-08-27T09:00:00Z',
        parentTitle: null,
        subtaskId: null,
      });

      const entries = groupTaskRuns([task]);

      expect(entries).toEqual([task]);
    });

    it('groups every subtask occurrence of a 3-subtask run into a single TaskRun', () => {
      const subtask1 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-1',
        title: 'Brush teeth',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-27T08:00:00Z',
        endsAt: '2026-08-27T08:05:00Z',
      });
      const subtask2 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-2',
        title: 'Get dressed',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-27T08:05:00Z',
        endsAt: '2026-08-27T08:10:00Z',
      });
      const subtask3 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-3',
        title: 'Eat breakfast',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-27T08:10:00Z',
        endsAt: '2026-08-27T08:20:00Z',
      });

      const entries = groupTaskRuns([subtask1, subtask2, subtask3]);

      expect(entries).toHaveLength(1);
      expect(isTaskRun(entries[0])).toBe(true);

      const run = entries[0] as TaskRun;
      expect(run.itemId).toBe('run-1');
      expect(run.parentTitle).toBe('Morning routine');
      expect(run.subtasks).toEqual([subtask1, subtask2, subtask3]);
    });

    it("uses the parent item's own icon for the run, not the first subtask's icon", () => {
      const subtask1 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        icon: '🪥',
        parentIcon: '🌞',
      });
      const subtask2 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-2',
        parentTitle: 'Morning routine',
        icon: '👕',
        parentIcon: '🌞',
      });

      const entries = groupTaskRuns([subtask1, subtask2]);

      const run = entries[0] as TaskRun;
      expect(run.icon).toBe('🌞');
    });

    it('keeps a run and an unrelated ordinary occurrence as separate entries, in encounter order', () => {
      const event = occurrence({ itemId: 'event-1', kind: 'Event', parentTitle: null });
      const subtask1 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
      });
      const subtask2 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-2',
        parentTitle: 'Morning routine',
      });

      const entries = groupTaskRuns([event, subtask1, subtask2]);

      expect(entries).toHaveLength(2);
      expect(entries[0]).toBe(event);
      expect(isTaskRun(entries[1])).toBe(true);
    });

    it('does not group two occurrences of the same recurring item on different days into one run', () => {
      const day1 = occurrence({
        itemId: 'recurring-1',
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-27T08:00:00Z',
        endsAt: '2026-08-27T08:05:00Z',
      });
      const day2 = occurrence({
        itemId: 'recurring-1',
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-28T08:00:00Z',
        endsAt: '2026-08-28T08:05:00Z',
      });

      const entries = groupTaskRuns([day1, day2]);

      expect(entries).toHaveLength(2);
      expect(isTaskRun(entries[0])).toBe(true);
      expect(isTaskRun(entries[1])).toBe(true);
      expect((entries[0] as TaskRun).subtasks).toEqual([day1]);
      expect((entries[1] as TaskRun).subtasks).toEqual([day2]);
    });

    it('returns an empty array for an empty input', () => {
      expect(groupTaskRuns([])).toEqual([]);
    });
  });

  describe('compareOccurrences', () => {
    const at = (time: string) => `2026-08-27T${time}:00Z`;

    it('orders by start time first, whatever the names', () => {
      const early = occurrence({ itemId: 'b', title: 'Zebra', startsAt: at('07:00') });
      const late = occurrence({ itemId: 'a', title: 'Apple', startsAt: at('08:00') });

      expect([late, early].sort(compareOccurrences)).toEqual([early, late]);
    });

    it('orders occurrences starting at the same time by name, locale-aware', () => {
      const zebra = occurrence({ itemId: 'z', title: 'Zebra', startsAt: at('08:00') });
      const bee = occurrence({ itemId: 'b', title: 'bee', startsAt: at('08:00') });
      const apple = occurrence({ itemId: 'a', title: 'Apple', startsAt: at('08:00') });

      expect([zebra, bee, apple].sort(compareOccurrences)).toEqual([apple, bee, zebra]);
    });

    it('compares a routine subtask by its routine title, not its own', () => {
      const subtask = occurrence({
        itemId: 'run-1',
        title: 'Aardvark step',
        startsAt: at('08:00'),
        subtaskId: 'sub-1',
        parentTitle: 'Morning',
      });
      const homework = occurrence({ itemId: 'hw', title: 'Homework', startsAt: at('08:00') });

      expect([subtask, homework].sort(compareOccurrences)).toEqual([homework, subtask]);
    });

    it('keeps two subtasks of one routine starting together in their given order', () => {
      const routineStep = (subtaskId: string, title: string) =>
        occurrence({
          itemId: 'run-1',
          title,
          startsAt: at('08:00'),
          subtaskId,
          parentTitle: 'Morning',
        });
      const second = routineStep('sub-2', 'Brush teeth');
      const first = routineStep('sub-1', 'Wake up');

      expect([first, second].sort(compareOccurrences)).toEqual([first, second]);
    });

    it('compares instants, not strings, across different offsets', () => {
      const utc = occurrence({ itemId: 'u', title: 'B', startsAt: '2026-08-27T08:00:00Z' });
      const offset = occurrence({ itemId: 'o', title: 'A', startsAt: '2026-08-27T09:30:00+02:00' });

      expect([utc, offset].sort(compareOccurrences)).toEqual([offset, utc]);
    });
  });

  describe('occurrenceKey', () => {
    const sortAt = '2026-08-27T08:00:00Z';
    const routine = (subtaskId: string) => ({
      subtaskId,
      parentTitle: 'Morning',
      parentIcon: '🌅',
    });

    it('keys a plain occurrence (no routine) by its itemId and date alone', () => {
      expect(occurrenceKey({ itemId: 'item-1', routine: null, sortAt })).toBe('item-1::2026-08-27');
    });

    it('gives two subtask occurrences sharing an itemId distinct keys', () => {
      const keyA = occurrenceKey({ itemId: 'run-1', routine: routine('sub-1'), sortAt });
      const keyB = occurrenceKey({ itemId: 'run-1', routine: routine('sub-2'), sortAt });

      expect(keyA).not.toBe(keyB);
      expect(keyA).toBe('run-1:sub-1:2026-08-27');
    });

    it('gives the same occurrence the same key on repeated calls', () => {
      const base = { itemId: 'run-1', routine: routine('sub-1'), sortAt };

      expect(occurrenceKey(base)).toBe(occurrenceKey(base));
    });

    it('gives two occurrences of the same recurring item on different days distinct keys', () => {
      const day1 = { itemId: 'recurring-1', routine: null, sortAt: '2026-08-27T08:00:00Z' };
      const day2 = { itemId: 'recurring-1', routine: null, sortAt: '2026-08-28T08:00:00Z' };

      expect(occurrenceKey(day1)).not.toBe(occurrenceKey(day2));
    });
  });
});
