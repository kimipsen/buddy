import { describe, expect, it, vi } from 'vitest';

import { nestOccurrence } from '../../testing/occurrence-fixture';
import { CalendarOccurrence, CalendarsService, TaskCompletion } from './calendars.service';
import { completeChildTask } from './child-task-completion';
import { todayIsoDate } from './date-utils';

describe('completeChildTask', () => {
  const base = {
    itemId: 'item-1',
    kind: 'Task' as const,
    title: 'Brush teeth',
    icon: '🪥',
    iconOverride: null,
    color: '#0f0',
    isAllDay: false,
    isCompleted: false,
    createdBy: 'g',
    lastModifiedBy: 'g',
    assignedTo: 'c',
    calendarId: 'cal-1',
    calendarName: 'Home',
  };

  function stub() {
    const completion = { itemId: 'item-1', occurrenceDate: todayIsoDate(), isCompleted: true };
    const setTaskCompletion = vi.fn(async () => completion as TaskCompletion);
    return { calendars: { setTaskCompletion } as unknown as CalendarsService, setTaskCompletion };
  }

  it("ticks a routine subtask for today by its subtask id and resolves the backend's answer", async () => {
    const { calendars, setTaskCompletion } = stub();
    const task = nestOccurrence<CalendarOccurrence>({
      ...base,
      startsAt: '2026-10-12T07:00:00Z',
      endsAt: '2026-10-12T07:05:00Z',
      subtaskId: 'sub-1',
      parentTitle: 'Morning',
    });

    await expect(completeChildTask(calendars, task, true)).resolves.toMatchObject({
      isCompleted: true,
    });

    expect(setTaskCompletion).toHaveBeenCalledWith(
      'cal-1',
      'item-1',
      todayIsoDate(),
      true,
      'sub-1',
    );
  });

  it('unticks a plain task with no subtask id', async () => {
    const { calendars, setTaskCompletion } = stub();
    const task = nestOccurrence<CalendarOccurrence>({ ...base, dueAt: '2026-10-12T12:00:00Z' });

    await completeChildTask(calendars, task, false);

    expect(setTaskCompletion).toHaveBeenCalledWith('cal-1', 'item-1', todayIsoDate(), false, null);
  });
});
