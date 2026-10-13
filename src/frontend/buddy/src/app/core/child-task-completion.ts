import { CalendarOccurrence, CalendarsService, TaskCompletion } from './calendars.service';
import { todayIsoDate } from './date-utils';

// Ticks (or unticks) one of the child's tasks for today: a plain task, or one subtask of a
// template-scheduled run. The backend awards or revokes that step's star in the same request
// (SetTaskCompletionHandler). Shared by the child home and the focus view; each page still patches
// its own copy of the occurrences, because each page owns its own resource.
export function completeChildTask(
  calendars: CalendarsService,
  task: CalendarOccurrence,
  isCompleted: boolean,
): Promise<TaskCompletion> {
  return calendars.setTaskCompletion(
    task.calendarId,
    task.itemId,
    todayIsoDate(),
    isCompleted,
    task.routine?.subtaskId ?? null,
  );
}
