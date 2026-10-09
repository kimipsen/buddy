import { firstAndLast } from '../../../core/array-utils';
import {
  addDaysIso,
  buildDateRangeIso,
  isoWeekNumber,
  parseIsoDate,
  toIsoDateInTimeZone,
  toTimeInTimeZone,
} from '../../../core/date-utils';
import { CalendarItemKind, CalendarItemOccurrence } from '../../../core/calendars.service';
import { PickupOccurrence, PickupSlot } from '../../../core/pickups.service';
import {
  PRINT_ROW_KIND,
  PrintTemplate,
  PrintTemplateRow,
} from '../../../core/print-templates.service';
import { compareOccurrences } from '../../../core/task-run';
import { workDayLocation } from '../../../core/work-locations.service';
import {
  WeekPlanCell,
  WeekPlanCheckItem,
  WeekPlanDay,
  WeekPlanItem,
  WeekPlanModel,
  WeekPlanOptions,
  WeekPlanRow,
  WeekPlanSources,
  WeekPlanText,
  mealSourceKey,
} from './week-plan-model';

export const WEEK_PLAN_DAY_COUNT = 7;

const TASK_KIND = 'Task' satisfies CalendarItemKind;
const DROP_OFF = 'DropOff' satisfies PickupSlot;
const PICK_UP = 'PickUp' satisfies PickupSlot;
const PICKUP_GUARDIAN = 0;
const PICKUP_SELF_ESCORT = 1;
const PICKUP_SIBLING = 2;
const PICKUP_PLAYDATE = 3;
const PICKUP_BABYSITTER = 4;

const BLANK: WeekPlanCell = { type: 'blank' };
const MARK: WeekPlanCell = { type: 'mark' };

// Pure: a template, a start date and the fetched sources in, one printable page out. Every rule
// that decides what lands on paper lives here so it can be unit-tested without Angular.
export function assembleWeekPlan(
  template: PrintTemplate,
  sources: WeekPlanSources,
  options: WeekPlanOptions,
): WeekPlanModel {
  const dates = buildDateRangeIso(options.start, WEEK_PLAN_DAY_COUNT);
  const colors = new Map(template.guardianColors.map((c) => [c.guardianId, c.color]));
  const babysitterColors = new Map(
    template.babysitterColors.map((c) => [babysitterKey(c.guardianId, c.babysitterId), c.color]),
  );
  const context: RowContext = { dates, sources, options, colors, babysitterColors };

  return {
    paperSize: template.paperSize,
    weekLabel: template.showWeekNumber ? weekLabel(dates, options.labels.week) : null,
    days: dates.map((date) => day(date, options.locale)),
    rows: template.rows.map((row) => assembleRow(row, context)),
  };
}

interface RowContext {
  dates: string[];
  sources: WeekPlanSources;
  options: WeekPlanOptions;
  colors: ReadonlyMap<string, string>;
  babysitterColors: ReadonlyMap<string, string>;
}

// Babysitter ids are only unique within one guardian's list, so colors are keyed by the pair.
export function babysitterKey(guardianId: string, babysitterId: string): string {
  return `${guardianId}:${babysitterId}`;
}

// "Uge 40" for a Monday start; any other start spans two ISO weeks: "Uge 40–41".
function weekLabel(dates: string[], word: string): string {
  const [firstDate, lastDate] = firstAndLast(dates);
  const first = isoWeekNumber(firstDate);
  const last = isoWeekNumber(lastDate);
  return first === last ? `${word} ${first}` : `${word} ${first}–${last}`;
}

function day(date: string, locale: string): WeekPlanDay {
  const parsed = parseIsoDate(date);
  return {
    date,
    weekday: parsed.toLocaleDateString(locale, { weekday: 'long' }),
    dayLabel: parsed.toLocaleDateString(locale, { day: 'numeric', month: 'short' }),
  };
}

function assembleRow(row: PrintTemplateRow, context: RowContext): WeekPlanRow {
  const base = { kind: row.kind, label: row.label, heightWeight: row.heightWeight };
  const cells = assembleCells(row, context);

  return cells === null
    ? { ...base, unavailable: true, cells: context.dates.map(() => BLANK) }
    : { ...base, unavailable: false, cells };
}

// null = a source this row needs is unavailable.
function assembleCells(row: PrintTemplateRow, context: RowContext): WeekPlanCell[] | null {
  switch (row.kind) {
    case PRINT_ROW_KIND.meal:
      return mealCells(row, context);
    case PRINT_ROW_KIND.pickup:
      return pickupCells(row, context);
    case PRINT_ROW_KIND.workLocation:
      return workLocationCells(row, context);
    case PRINT_ROW_KIND.calendarMarker:
    case PRINT_ROW_KIND.calendarEvents:
    case PRINT_ROW_KIND.taskChecklist:
      return calendarCells(row, context);
    default:
      return context.dates.map(() => BLANK);
  }
}

// A row that doesn't name its source yet (still being built in the editor) is simply blank,
// not "unavailable" -- nothing failed.
function blankRow(dates: string[]): WeekPlanCell[] {
  return dates.map(() => BLANK);
}

function mealCells(row: PrintTemplateRow, { dates, sources }: RowContext): WeekPlanCell[] | null {
  if (!row.childId && !row.mealGroupId) {
    return blankRow(dates);
  }
  const entries = sources.meals.get(mealSourceKey(row));
  if (!entries) {
    return null;
  }

  return dates.map((date) => {
    const entry = entries.find((e) => e.date === date && e.slot === row.mealSlot);
    return entry ? text({ text: entry.mealName, icon: null, color: null }) : BLANK;
  });
}

function pickupCells(row: PrintTemplateRow, context: RowContext): WeekPlanCell[] | null {
  if (!row.childId) {
    return blankRow(context.dates);
  }
  const occurrences = context.sources.pickups.get(row.childId);
  if (!occurrences) {
    return null;
  }

  return context.dates.map((date) => {
    const dropOff = occurrences.find((o) => o.date === date && o.slot === DROP_OFF);
    const pickUp = occurrences.find((o) => o.date === date && o.slot === PICK_UP);

    return dropOff || pickUp
      ? {
          type: 'pickup',
          dropOff: pickupLabel(dropOff, context),
          pickUp: pickupLabel(pickUp, context),
        }
      : BLANK;
  });
}

// A missing slot stays blank so it can be filled in by hand.
function pickupLabel(
  occurrence: PickupOccurrence | undefined,
  context: RowContext,
): WeekPlanText | null {
  if (!occurrence) {
    return null;
  }

  const { names } = context.sources;
  const { labels } = context.options;

  const { assignee } = occurrence;

  switch (assignee.kind) {
    case PICKUP_GUARDIAN:
      return {
        text: names.get(assignee.guardianId) ?? '',
        icon: null,
        color: context.colors.get(assignee.guardianId) ?? null,
      };
    case PICKUP_SELF_ESCORT:
      return { text: labels.selfEscort, icon: null, color: null };
    case PICKUP_SIBLING:
      return { text: names.get(assignee.siblingChildId) ?? '', icon: null, color: null };
    case PICKUP_PLAYDATE:
      return { text: `${labels.playdate}: ${assignee.hostName}`, icon: null, color: null };
    case PICKUP_BABYSITTER:
      return {
        text: (assignee.name ?? '') || labels.babysitter,
        icon: null,
        color:
          context.babysitterColors.get(babysitterKey(assignee.guardianId, assignee.babysitterId)) ??
          null,
      };
  }
}

function workLocationCells(row: PrintTemplateRow, context: RowContext): WeekPlanCell[] | null {
  if (!row.guardianId) {
    return blankRow(context.dates);
  }
  const days = context.sources.workDays.get(row.guardianId);
  if (!days) {
    return null;
  }

  return context.dates.map((date) => {
    const day = days.find((d) => d.date === date);
    const location = day ? workDayLocation(day) : null;

    if (row.workLocationId) {
      return location?.id === row.workLocationId ? MARK : BLANK;
    }

    return location
      ? text({
          text: location.name,
          icon: location.icon,
          color: context.colors.get(row.guardianId ?? '') ?? null,
        })
      : BLANK;
  });
}

function calendarCells(row: PrintTemplateRow, context: RowContext): WeekPlanCell[] | null {
  const calendarIds = row.calendarIds ?? [];
  const loaded = calendarIds
    .map((id) => context.sources.occurrences.get(id))
    .filter((occurrences) => occurrences !== undefined && occurrences !== null);
  if (loaded.length !== calendarIds.length) {
    return null;
  }

  const filter = row.titleFilter?.toLowerCase() ?? null;
  const occurrences = dedupe(loaded.flat())
    .filter((o) => !row.assignedToId || o.assignedTo === row.assignedToId)
    .filter((o) => row.kind !== PRINT_ROW_KIND.taskChecklist || o.kind === TASK_KIND)
    .filter(
      (o) =>
        row.kind === PRINT_ROW_KIND.taskChecklist ||
        !filter ||
        o.title.toLowerCase().includes(filter),
    )
    .sort(byAllDayThenStart);

  const timeZone = context.options.timeZone;

  return context.dates.map((date) => {
    const onDay = occurrences.filter((o) => coveredDates(o, timeZone).includes(date));

    if (onDay.length === 0) {
      return BLANK;
    }

    if (row.kind === PRINT_ROW_KIND.calendarMarker) {
      return MARK;
    }

    const entries = groupRoutines(onDay, context.options.includeSubtasks);

    if (row.kind === PRINT_ROW_KIND.taskChecklist) {
      const items = entries.map(({ text, subtasks }): WeekPlanCheckItem => ({ text, subtasks }));
      return { type: 'checklist', ...limit(items, row.maxItems) };
    }

    const items = entries.map(({ first: o, text, subtasks }): WeekPlanItem => {
      return {
        time:
          row.showTime && !o.isAllDay && startsOn(o, date, timeZone)
            ? toTimeInTimeZone(new Date(o.sortAt), timeZone)
            : null,
        text,
        assignee:
          row.showAssignee && o.assignedTo
            ? (context.sources.names.get(o.assignedTo) ?? null)
            : null,
        subtasks,
      };
    });
    return { type: 'list', ...limit(items, row.maxItems) };
  });
}

interface CellEntry {
  // The occurrence the entry is timed and assigned by: a routine's earliest subtask.
  first: CalendarItemOccurrence;
  text: string;
  subtasks: string[];
}

// A routine scheduled from a task template prints once, under its parent title, at the position
// of its first subtask; its subtasks' titles follow it only when asked for. Grouping is per item,
// so two different tasks that happen to share a title stay two entries.
function groupRoutines(onDay: CalendarItemOccurrence[], includeSubtasks: boolean): CellEntry[] {
  const routines = new Map<string, CellEntry>();
  const entries: CellEntry[] = [];
  for (const o of onDay) {
    if (!o.routine) {
      entries.push({ first: o, text: o.title, subtasks: [] });
      continue;
    }
    let entry = routines.get(o.itemId);
    if (!entry) {
      entry = { first: o, text: o.routine.parentTitle, subtasks: [] };
      routines.set(o.itemId, entry);
      entries.push(entry);
    }
    if (includeSubtasks) {
      entry.subtasks.push(o.title);
    }
  }
  return entries;
}

function limit<T>(items: T[], maxItems: number | null): { items: T[]; overflow: number } {
  return maxItems && items.length > maxItems
    ? { items: items.slice(0, maxItems), overflow: items.length - maxItems }
    : { items, overflow: 0 };
}

// The same item can come back from two calendars named in one row.
function dedupe(occurrences: CalendarItemOccurrence[]): CalendarItemOccurrence[] {
  const seen = new Set<string>();
  return occurrences.filter((o) => {
    const key = `${o.itemId}|${o.routine?.subtaskId ?? ''}|${o.sortAt}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function startsOn(o: CalendarItemOccurrence, date: string, timeZone: string): boolean {
  return toIsoDateInTimeZone(new Date(o.sortAt), timeZone) === date;
}

// Every local date an occurrence touches: [startsAt, endsAt) for events (endsAt is exclusive, so
// an all-day event ending at midnight doesn't spill into the next day), the due date for tasks.
function coveredDates(o: CalendarItemOccurrence, timeZone: string): string[] {
  const first = toIsoDateInTimeZone(new Date(o.sortAt), timeZone);
  const { timing } = o;
  if (timing.kind !== 0 || new Date(timing.endsAt) <= new Date(timing.startsAt)) {
    return [first];
  }

  const last = toIsoDateInTimeZone(new Date(new Date(timing.endsAt).getTime() - 1), timeZone);
  const dates = [first];
  for (let date = first; date < last;) {
    date = addDaysIso(date, 1);
    dates.push(date);
  }
  return dates;
}

function byAllDayThenStart(a: CalendarItemOccurrence, b: CalendarItemOccurrence): number {
  if (a.isAllDay !== b.isAllDay) {
    return a.isAllDay ? -1 : 1;
  }
  return compareOccurrences(a, b);
}

function text(value: WeekPlanText): WeekPlanCell {
  return { type: 'text', value };
}
