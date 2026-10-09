import { CalendarSummary } from '../../../core/calendars.service';
import { FeatureName } from '../../../core/features.service';
import { MealSlot } from '../../../core/mealplans.service';
import { ChildSummary, GuardianSummary } from '../../../core/guardians.service';
import {
  PRINT_ROW_KIND,
  PrintRowKind,
  PrintTemplateRow,
  emptyRow,
} from '../../../core/print-templates.service';
import { WorkLocation } from '../../../core/work-locations.service';

const DINNER = 'Dinner' satisfies MealSlot;

// Backend limits (PrintTemplateRules).
export const MAX_LABEL_LENGTH = 40;
export const MAX_CALENDARS_PER_ROW = 10;

const CALENDAR_KINDS: readonly PrintRowKind[] = [
  PRINT_ROW_KIND.calendarMarker,
  PRINT_ROW_KIND.calendarEvents,
  PRINT_ROW_KIND.taskChecklist,
];

export function usesCalendars(kind: PrintRowKind): boolean {
  return CALENDAR_KINDS.includes(kind);
}

// The optional feature a row kind reads from (docs/backend/analysis/feature-flags.md). While it is
// off, the editor doesn't offer the kind and the sheet prints existing rows of it empty.
const ROW_KIND_FEATURES: Partial<Record<PrintRowKind, FeatureName>> = {
  [PRINT_ROW_KIND.meal]: 'mealplans',
  [PRINT_ROW_KIND.pickup]: 'pickups',
  [PRINT_ROW_KIND.workLocation]: 'workLocations',
};

export function rowKindFeature(kind: PrintRowKind): FeatureName | undefined {
  return ROW_KIND_FEATURES[kind];
}

// Mirrors the backend's per-kind field table: everything a kind doesn't use is cleared before
// saving, so the validator never sees stray values left over from switching a row's kind.
export function cleanRow(row: PrintTemplateRow): PrintTemplateRow {
  const clean = emptyRow(row.kind, row.label.trim());
  clean.heightWeight = row.heightWeight;

  switch (row.kind) {
    case PRINT_ROW_KIND.meal:
      return {
        ...clean,
        childId: row.mealGroupId ? null : row.childId,
        mealGroupId: row.mealGroupId,
        mealSlot: row.mealSlot,
      };
    case PRINT_ROW_KIND.pickup:
      return { ...clean, childId: row.childId };
    case PRINT_ROW_KIND.workLocation:
      return { ...clean, guardianId: row.guardianId, workLocationId: row.workLocationId };
    case PRINT_ROW_KIND.calendarMarker:
      return { ...clean, calendarIds: row.calendarIds, titleFilter: blankToNull(row.titleFilter) };
    case PRINT_ROW_KIND.calendarEvents:
      return {
        ...clean,
        calendarIds: row.calendarIds,
        assignedToId: row.assignedToId,
        titleFilter: blankToNull(row.titleFilter),
        maxItems: row.maxItems,
        showTime: row.showTime,
        showAssignee: row.showAssignee,
      };
    case PRINT_ROW_KIND.taskChecklist:
      return {
        ...clean,
        calendarIds: row.calendarIds,
        assignedToId: row.assignedToId,
        maxItems: row.maxItems,
      };
    default:
      return clean;
  }
}

// The i18n key of the first thing this row still needs before it can be saved, or null.
export function missingField(row: PrintTemplateRow): string | null {
  if (row.kind !== PRINT_ROW_KIND.blank && !row.label.trim()) {
    return 'print.editor.missing.label';
  }
  if (row.label.trim().length > MAX_LABEL_LENGTH) {
    return 'print.editor.missing.labelTooLong';
  }
  if ((row.calendarIds?.length ?? 0) > MAX_CALENDARS_PER_ROW) {
    return 'print.editor.missing.tooManyCalendars';
  }

  switch (row.kind) {
    case PRINT_ROW_KIND.meal:
      return !row.childId && !row.mealGroupId
        ? 'print.editor.missing.mealScope'
        : row.mealSlot === null
          ? 'print.editor.missing.mealSlot'
          : null;
    case PRINT_ROW_KIND.pickup:
      return row.childId ? null : 'print.editor.missing.child';
    case PRINT_ROW_KIND.workLocation:
      return row.guardianId ? null : 'print.editor.missing.guardian';
    default:
      return usesCalendars(row.kind) && !row.calendarIds?.length
        ? 'print.editor.missing.calendars'
        : null;
  }
}

function blankToNull(value: string | null): string | null {
  return value?.trim() ? value.trim() : null;
}

export interface ExampleInputs {
  children: ChildSummary[];
  guardians: GuardianSummary[];
  calendars: CalendarSummary[];
  workLocations: ReadonlyMap<string, WorkLocation[]>;
  labels: {
    dinner: string;
    pickup: (child: string) => string;
    atLocation: (guardian: string, location: string) => string;
    activities: (child: string) => string;
    chores: (child: string) => string;
    appointments: string;
    notes: string;
  };
}

// A starting point mirroring a typical family fridge sheet, built only from the guardian's own
// children, guardians and calendars. Nothing is saved until they press Save. The backend allows
// 12 rows, so the per-child rows stop short of that.
export function exampleRows(input: ExampleInputs): PrintTemplateRow[] {
  const { children, guardians, calendars, labels } = input;
  const calendarIds = calendars.slice(0, MAX_CALENDARS_PER_ROW).map((c) => c.id);
  const firstChild = children[0];
  const rows: PrintTemplateRow[] = [];

  if (firstChild) {
    rows.push({
      ...emptyRow(PRINT_ROW_KIND.meal, labels.dinner),
      childId: firstChild.id,
      mealSlot: DINNER,
    });
  }

  for (const child of children) {
    rows.push({
      ...emptyRow(PRINT_ROW_KIND.pickup, labels.pickup(child.name.givenName)),
      childId: child.id,
    });
  }

  for (const guardian of guardians) {
    for (const location of (input.workLocations.get(guardian.id) ?? []).filter(
      (l) => !l.isArchived,
    )) {
      rows.push({
        ...emptyRow(
          PRINT_ROW_KIND.workLocation,
          labels.atLocation(guardian.name.givenName, location.name),
        ),
        guardianId: guardian.id,
        workLocationId: location.id,
      });
    }
  }

  if (calendarIds.length > 0) {
    for (const child of children) {
      rows.push({
        ...emptyRow(PRINT_ROW_KIND.calendarEvents, labels.activities(child.name.givenName)),
        heightWeight: 2,
        calendarIds,
        assignedToId: child.id,
        showTime: true,
        maxItems: 3,
      });
      rows.push({
        ...emptyRow(PRINT_ROW_KIND.taskChecklist, labels.chores(child.name.givenName)),
        heightWeight: 2,
        calendarIds,
        assignedToId: child.id,
        maxItems: 4,
      });
    }
    rows.push({
      ...emptyRow(PRINT_ROW_KIND.calendarEvents, labels.appointments),
      heightWeight: 3,
      calendarIds,
      showTime: true,
    });
  }

  rows.push({ ...emptyRow(PRINT_ROW_KIND.blank, labels.notes), heightWeight: 1 });

  // A big family can overflow the backend's 12-row limit: drop the rows past it but keep the
  // trailing notes row. Labels built from long names are cut to the backend's label limit.
  const capped = rows.length > 12 ? [...rows.slice(0, 11), ...rows.slice(-1)] : rows;
  return capped.map((row) => ({ ...row, label: row.label.slice(0, MAX_LABEL_LENGTH) }));
}
