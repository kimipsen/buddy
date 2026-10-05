import { CalendarItemOccurrence } from '../../../core/calendars.service';
import { MealPlanEntry } from '../../../core/mealplans.service';
import { PickupOccurrence } from '../../../core/pickups.service';
import { PaperSize, PrintRowKind } from '../../../core/print-templates.service';
import { WorkDay } from '../../../core/work-locations.service';

// What WeekPlanSheet renders: one fully resolved page. Built by assembleWeekPlan, which is the
// only place the printing rules live (see docs/frontend/analysis/week-plan-printing.md).

export interface WeekPlanDay {
  date: string;
  weekday: string;
  dayLabel: string;
}

export interface WeekPlanText {
  text: string;
  icon: string | null;
  color: string | null;
}

// subtasks: the titles of a template-scheduled routine's subtasks, printed under its parent
// title. Empty for every other item, and for routines when subtasks are left out.
export interface WeekPlanItem {
  time: string | null;
  text: string;
  assignee: string | null;
  subtasks: string[];
}

export interface WeekPlanCheckItem {
  text: string;
  subtasks: string[];
}

export type WeekPlanCell =
  | { type: 'blank' }
  | { type: 'mark' }
  | { type: 'text'; value: WeekPlanText }
  | { type: 'list'; items: WeekPlanItem[]; overflow: number }
  | { type: 'checklist'; items: WeekPlanCheckItem[]; overflow: number }
  | { type: 'pickup'; dropOff: WeekPlanText | null; pickUp: WeekPlanText | null };

export interface WeekPlanRow {
  kind: PrintRowKind;
  label: string;
  heightWeight: number;
  // A source this row depends on couldn't be read by the printing guardian (typically 404).
  unavailable: boolean;
  cells: WeekPlanCell[];
}

export interface WeekPlanModel {
  paperSize: PaperSize;
  weekLabel: string | null;
  days: WeekPlanDay[];
  rows: WeekPlanRow[];
}

// Everything the loader fetched, keyed so each source is fetched once however many rows use it.
// A null value means that source failed for the printing guardian.
export interface WeekPlanSources {
  meals: ReadonlyMap<string, MealPlanEntry[] | null>;
  pickups: ReadonlyMap<string, PickupOccurrence[] | null>;
  workDays: ReadonlyMap<string, WorkDay[] | null>;
  occurrences: ReadonlyMap<string, CalendarItemOccurrence[] | null>;
  // Given names of children and guardians, for pickup cells and assignees.
  names: ReadonlyMap<string, string>;
}

export function mealSourceKey(row: { childId: string | null; mealGroupId: string | null }): string {
  return row.mealGroupId ? `group:${row.mealGroupId}` : `child:${row.childId}`;
}

// Translated words the sheet prints, resolved by the caller in the printing guardian's language.
export interface WeekPlanLabels {
  week: string;
  selfEscort: string;
  playdate: string;
}

export interface WeekPlanOptions {
  start: string;
  locale: string;
  timeZone: string;
  labels: WeekPlanLabels;
  // Print a routine's subtasks under its parent title, or just the parent title.
  includeSubtasks: boolean;
}
