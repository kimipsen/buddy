import { describe, expect, it, vi } from 'vitest';

import { nestOccurrence } from '../../testing/occurrence-fixture';
import { CalendarOccurrence, CalendarsService, TaskCompletion } from './calendars.service';
import { completeChildTask } from './child-task-completion';

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
    isSkipped: false,
    isRepeating: false,
    createdBy: 'g',
    lastModifiedBy: 'g',
    assignedTo: 'c',
    calendarId: 'cal-1',
    calendarName: 'Home',
  };

  function stub() {
    const completion = { itemId: 'item-1', occurrenceDate: '2026-10-12', isCompleted: true };
    const setTaskCompletion = vi.fn(async () => completion as TaskCompletion);
    return { calendars: { setTaskCompletion } as unknown as CalendarsService, setTaskCompletion };
  }

  it("ticks a routine subtask for the given day by its subtask id and resolves the backend's answer", async () => {
    const { calendars, setTaskCompletion } = stub();
    const task = nestOccurrence<CalendarOccurrence>({
      ...base,
      startsAt: '2026-10-12T07:00:00Z',
      endsAt: '2026-10-12T07:05:00Z',
      subtaskId: 'sub-1',
      parentTitle: 'Morning',
    });

    await expect(completeChildTask(calendars, task, true, '2026-10-12')).resolves.toMatchObject({
      isCompleted: true,
    });

    expect(setTaskCompletion).toHaveBeenCalledWith('cal-1', 'item-1', '2026-10-12', true, 'sub-1');
  });

  it('unticks a plain task with no subtask id', async () => {
    const { calendars, setTaskCompletion } = stub();
    const task = nestOccurrence<CalendarOccurrence>({ ...base, dueAt: '2026-10-12T12:00:00Z' });

    await completeChildTask(calendars, task, false, '2026-10-12');

    expect(setTaskCompletion).toHaveBeenCalledWith('cal-1', 'item-1', '2026-10-12', false, null);
  });

  it('ticks a packing-list item for the date it is given (tomorrow’s list)', async () => {
    const { calendars, setTaskCompletion } = stub();
    const task = nestOccurrence<CalendarOccurrence>({ ...base, dueAt: '2026-10-14T05:45:00Z' });

    await completeChildTask(calendars, task, true, '2026-10-14');

    expect(setTaskCompletion).toHaveBeenCalledWith('cal-1', 'item-1', '2026-10-14', true, null);
  });
});
