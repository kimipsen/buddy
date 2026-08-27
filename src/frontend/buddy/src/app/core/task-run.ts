import { CalendarOccurrence } from './calendars.service';

// A "run" is every subtask occurrence a single ScheduleTaskFromTemplate-created CalendarItem
// produces for one calendar day -- e.g. a 3-subtask morning routine template shows up as 3
// CalendarItemOccurrence rows sharing the same itemId (and parentTitle, the template-scheduled
// item's own Title), one per subtask. Rendered separately, that reads as three unrelated events;
// TaskRun exists purely to group them back into one visual block. Grouping is a rendering
// concern only -- it never changes how completion is tracked or targeted (see occurrenceKey
// below), and the ungrouped occurrence array from the backend remains the single source of truth.
export interface TaskRun {
  itemId: string;
  parentTitle: string;
  calendarId: string;
  calendarName: string;
  color: string;
  icon: string;
  subtasks: CalendarOccurrence[];
}

// One row in a grouped agenda: either a run of subtask occurrences, or any other occurrence
// (an event, or a plain hand-entered task) passed through unchanged.
export type AgendaEntry = CalendarOccurrence | TaskRun;

export function isTaskRun(entry: AgendaEntry): entry is TaskRun {
  return Array.isArray((entry as TaskRun).subtasks);
}

// The date portion of whichever instant the occurrence is anchored on. Both startsAt and dueAt
// are full ISO instants ("...T09:00:00Z") whose first 10 characters are always the calendar date
// they're stored against -- slicing is enough here, no timezone conversion needed, since this is
// only used to keep two occurrences of the same *recurring* template-scheduled item (same itemId,
// different calendar day) from being folded into a single run.
function dateKeyOf(occurrence: Pick<CalendarOccurrence, 'startsAt' | 'dueAt'>): string {
  return (occurrence.startsAt ?? occurrence.dueAt ?? '').slice(0, 10);
}

// Groups a list of occurrences (typically one calendar day's worth, already the caller's unit of
// display) into agenda rows. An occurrence with a non-null parentTitle is one subtask of a
// template-scheduled task -- every such occurrence sharing an itemId and calendar day folds into
// a single TaskRun, in encounter order, at the position its first subtask appeared. Every other
// occurrence (an event, or a plain hand-entered task -- parentTitle null) passes through
// unchanged, preserving today's one-row-per-occurrence rendering exactly.
export function groupTaskRuns(occurrences: CalendarOccurrence[]): AgendaEntry[] {
  const entries: AgendaEntry[] = [];
  const runsByKey = new Map<string, TaskRun>();

  for (const occurrence of occurrences) {
    if (!occurrence.parentTitle) {
      entries.push(occurrence);
      continue;
    }

    const key = `${occurrence.itemId}|${dateKeyOf(occurrence)}`;
    let run = runsByKey.get(key);

    if (!run) {
      run = {
        itemId: occurrence.itemId,
        parentTitle: occurrence.parentTitle,
        calendarId: occurrence.calendarId,
        calendarName: occurrence.calendarName,
        color: occurrence.color,
        // The parent's own effective icon, not the (possibly subtask-specific) icon of whichever
        // subtask happens to appear first -- see CalendarItemOccurrence.parentIcon.
        icon: occurrence.parentIcon ?? occurrence.icon,
        subtasks: []
      };
      runsByKey.set(key, run);
      entries.push(run);
    }

    run.subtasks.push(occurrence);
  }

  return entries;
}

// Stable per-toggle key for a task occurrence. Two subtask occurrences of the same
// template-scheduled run share an itemId but never a subtaskId, so keying on itemId alone (as
// every "is this task currently being toggled/completed" signal did before subtask occurrences
// existed) makes completing one subtask visually toggle every sibling subtask sharing that
// itemId -- this is the fix for that. subtaskId is undefined/null for a plain (non-template) task,
// so its key reduces to `${itemId}:`, distinct per itemId exactly as before.
//
// The date suffix (via dateKeyOf) matters once a caller's in-memory occurrence array can span more
// than one calendar day -- a week/workweek view, for instance. Without it, a recurring item's
// itemId (and, for a plain recurring task, empty subtaskId) repeats once per day it occurs, so the
// optimistic "mark done" update below -- which patches every occurrence whose key matches -- would
// flip every day's occurrence of that item at once instead of just the one that was toggled.
export function occurrenceKey(occurrence: Pick<CalendarOccurrence, 'itemId' | 'subtaskId' | 'startsAt' | 'dueAt'>): string {
  return `${occurrence.itemId}:${occurrence.subtaskId ?? ''}:${dateKeyOf(occurrence)}`;
}
