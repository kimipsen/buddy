import { describe, expect, it } from 'vitest';

import { CalendarOccurrence } from './calendars.service';
import { TaskRun, groupTaskRuns, isTaskRun, occurrenceKey } from './task-run';

describe('task-run', () => {
  function occurrence(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'item-1',
      kind: 1,
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
      ...overrides
    };
  }

  describe('groupTaskRuns', () => {
    it('passes an ordinary event through unchanged, ungrouped', () => {
      const event = occurrence({ itemId: 'event-1', kind: 0, parentTitle: null, subtaskId: null });

      const entries = groupTaskRuns([event]);

      expect(entries).toEqual([event]);
    });

    it('passes a plain hand-entered task (no parentTitle) through unchanged, ungrouped', () => {
      const task = occurrence({ itemId: 'task-1', kind: 1, parentTitle: null, subtaskId: null });

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
        endsAt: '2026-08-27T08:05:00Z'
      });
      const subtask2 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-2',
        title: 'Get dressed',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-27T08:05:00Z',
        endsAt: '2026-08-27T08:10:00Z'
      });
      const subtask3 = occurrence({
        itemId: 'run-1',
        subtaskId: 'sub-3',
        title: 'Eat breakfast',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-27T08:10:00Z',
        endsAt: '2026-08-27T08:20:00Z'
      });

      const entries = groupTaskRuns([subtask1, subtask2, subtask3]);

      expect(entries).toHaveLength(1);
      expect(isTaskRun(entries[0])).toBe(true);

      const run = entries[0] as TaskRun;
      expect(run.itemId).toBe('run-1');
      expect(run.parentTitle).toBe('Morning routine');
      expect(run.subtasks).toEqual([subtask1, subtask2, subtask3]);
    });

    it('keeps a run and an unrelated ordinary occurrence as separate entries, in encounter order', () => {
      const event = occurrence({ itemId: 'event-1', kind: 0, parentTitle: null });
      const subtask1 = occurrence({ itemId: 'run-1', subtaskId: 'sub-1', parentTitle: 'Morning routine' });
      const subtask2 = occurrence({ itemId: 'run-1', subtaskId: 'sub-2', parentTitle: 'Morning routine' });

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
        endsAt: '2026-08-27T08:05:00Z'
      });
      const day2 = occurrence({
        itemId: 'recurring-1',
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        startsAt: '2026-08-28T08:00:00Z',
        endsAt: '2026-08-28T08:05:00Z'
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

  describe('occurrenceKey', () => {
    it('keys a plain occurrence (no subtaskId) by its itemId alone', () => {
      expect(occurrenceKey({ itemId: 'item-1', subtaskId: null })).toBe('item-1:');
      expect(occurrenceKey({ itemId: 'item-1', subtaskId: undefined })).toBe('item-1:');
    });

    it('gives two subtask occurrences sharing an itemId distinct keys', () => {
      const keyA = occurrenceKey({ itemId: 'run-1', subtaskId: 'sub-1' });
      const keyB = occurrenceKey({ itemId: 'run-1', subtaskId: 'sub-2' });

      expect(keyA).not.toBe(keyB);
    });

    it('gives the same occurrence the same key on repeated calls', () => {
      const a = occurrenceKey({ itemId: 'run-1', subtaskId: 'sub-1' });
      const b = occurrenceKey({ itemId: 'run-1', subtaskId: 'sub-1' });

      expect(a).toBe(b);
    });
  });
});
