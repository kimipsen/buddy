import {
  addDaysIso,
  buildDateRangeIso,
  isoWeekNumber,
  parseIsoDate,
  toIsoDateInTimeZone,
  toTimeInTimeZone,
} from '../../../core/date-utils';
import { CalendarItemOccurrence } from '../../../core/calendars.service';
import { PickupOccurrence } from '../../../core/pickups.service';
import {
  PRINT_ROW_KIND,
  PrintTemplate,
  PrintTemplateRow,
} from '../../../core/print-templates.service';
import {
  WeekPlanCell,
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

const TASK_KIND = 1;
const DROP_OFF = 0;
const PICK_UP = 1;
const PICKUP_GUARDIAN = 0;
const PICKUP_SELF_ESCORT = 1;
const PICKUP_SIBLING = 2;

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
  const context: RowContext = { dates, sources, options, colors };

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
}

// "Uge 40" for a Monday start; any other start spans two ISO weeks: "Uge 40–41".
function weekLabel(dates: string[], word: string): string {
  const first = isoWeekNumber(dates[0]);
  const last = isoWeekNumber(dates[dates.length - 1]);
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
  const occurrences = context.sources.pickups.get(row.childId ?? '');
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

  switch (occurrence.kind) {
    case PICKUP_GUARDIAN:
      return {
        text: names.get(occurrence.guardianId ?? '') ?? '',
        icon: null,
        color: context.colors.get(occurrence.guardianId ?? '') ?? null,
      };
    case PICKUP_SELF_ESCORT:
      return { text: labels.selfEscort, icon: null, color: null };
    case PICKUP_SIBLING:
      return { text: names.get(occurrence.siblingChildId ?? '') ?? '', icon: null, color: null };
    default:
      return {
        text: `${labels.playdate}: ${occurrence.playdateHostName ?? ''}`,
        icon: null,
        color: null,
      };
  }
}

function workLocationCells(row: PrintTemplateRow, context: RowContext): WeekPlanCell[] | null {
  if (!row.guardianId) {
    return blankRow(context.dates);
  }
  const days = context.sources.workDays.get(row.guardianId ?? '');
  if (!days) {
    return null;
  }

  return context.dates.map((date) => {
    const location = days.find((d) => d.date === date)?.location;

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
  const loaded = (row.calendarIds ?? []).map((id) => context.sources.occurrences.get(id));
  if (loaded.some((occurrences) => !occurrences)) {
    return null;
  }

  const filter = row.titleFilter?.toLowerCase() ?? null;
  const occurrences = dedupe(loaded.flatMap((o) => o!))
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

    if (row.kind === PRINT_ROW_KIND.taskChecklist) {
      // A routine scheduled from a task template prints once, under its parent title. Grouping is
      // per item, so two different tasks that happen to share a title stay two tick boxes.
      const groups = new Map<string, string>();
      for (const o of onDay) {
        const key = o.parentTitle
          ? `${o.itemId}|routine`
          : `${o.itemId}|${o.subtaskId ?? ''}|${start(o)}`;
        if (!groups.has(key)) {
          groups.set(key, o.parentTitle ?? o.title);
        }
      }
      return { type: 'checklist', ...limit([...groups.values()], row.maxItems) };
    }

    const items = onDay.map((o): WeekPlanItem => ({
      time:
        row.showTime && !o.isAllDay && startsOn(o, date, timeZone)
          ? toTimeInTimeZone(new Date(start(o)!), timeZone)
          : null,
      text: o.title,
      assignee:
        row.showAssignee && o.assignedTo ? (context.sources.names.get(o.assignedTo) ?? null) : null,
    }));
    return { type: 'list', ...limit(items, row.maxItems) };
  });
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
    const key = `${o.itemId}|${o.subtaskId ?? ''}|${start(o)}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function start(o: CalendarItemOccurrence): string | null {
  return o.startsAt ?? o.dueAt;
}

function startsOn(o: CalendarItemOccurrence, date: string, timeZone: string): boolean {
  const value = start(o);
  return !!value && toIsoDateInTimeZone(new Date(value), timeZone) === date;
}

// Every local date an occurrence touches: [startsAt, endsAt) for events (endsAt is exclusive, so
// an all-day event ending at midnight doesn't spill into the next day), the due date for tasks.
function coveredDates(o: CalendarItemOccurrence, timeZone: string): string[] {
  const value = start(o);
  if (!value) {
    return [];
  }

  const first = toIsoDateInTimeZone(new Date(value), timeZone);
  if (!o.startsAt || !o.endsAt || new Date(o.endsAt) <= new Date(o.startsAt)) {
    return [first];
  }

  const last = toIsoDateInTimeZone(new Date(new Date(o.endsAt).getTime() - 1), timeZone);
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
  return new Date(start(a) ?? 0).getTime() - new Date(start(b) ?? 0).getTime();
}

function text(value: WeekPlanText): WeekPlanCell {
  return { type: 'text', value };
}
