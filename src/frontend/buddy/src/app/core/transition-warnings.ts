import type { Schemas } from './api/schemas';
import { CalendarOccurrence } from './calendars.service';
import { instantInTimeZone, minutesOfDayInTimeZone, toIsoDateInTimeZone } from './date-utils';
import { PickupOccurrence, PickupSlot } from './pickups.service';
import { groupTaskRuns, isTaskRun } from './task-run';

// The heads-up a child sees before a transition (docs/backend/analysis/transition-warnings.md,
// Question 5). Pure: no Angular, no clock -- every case is a plain test with a fixed `nowMs`.

export type WarningKind = Schemas['WarningKind'];
export type QuietHours = Schemas['QuietHours'];
export type MyTransitionWarnings = Schemas['MyTransitionWarningsResponse'];

export interface TransitionWarning {
  /** `${source}:${id}:${date}:${leadMinutes}`: one stage of one item, what "Got it" dismisses. */
  key: string;
  kind: WarningKind;
  /** The item's title; empty for a pickup, whose label the banner translates from `slot`. */
  title: string;
  icon: string;
  slot: PickupSlot | null;
  /** What the countdown counts down to. */
  startsAtMs: number;
  /** The stage this is (15, 5, 1 ...). */
  leadMinutes: number;
  /** The item's largest lead time: where the draining bar starts. */
  firstLeadMinutes: number;
  /** The item's smallest lead time, styled more urgently. */
  isFinalStage: boolean;
}

interface Candidate {
  source: 'item' | 'pickup';
  id: string;
  kind: WarningKind;
  title: string;
  icon: string;
  slot: PickupSlot | null;
  startsAtMs: number;
  // Undefined when nothing overrides this item.
  override: readonly number[] | undefined;
}

const PICKUP_ICONS: Record<PickupSlot, string> = { DropOff: '🎒', PickUp: '👋' };

function startOf(occurrence: CalendarOccurrence): number {
  const { timing } = occurrence;
  return Date.parse(timing.kind === 0 ? timing.startsAt : timing.dueAt);
}

// Events, plain tasks, and one candidate per routine or packing list (counting down to its first
// subtask: the transition is into the routine, and every packing-list item is due at its "leave
// by" time). All-day items and anything already done never warn.
function occurrenceCandidates(
  occurrences: readonly CalendarOccurrence[],
  overrides: ReadonlyMap<string, readonly number[]>,
): Candidate[] {
  const candidates: Candidate[] = [];

  for (const entry of groupTaskRuns([...occurrences])) {
    if (isTaskRun(entry)) {
      if (entry.subtasks.some((subtask) => subtask.isAllDay)) {
        continue;
      }
      if (entry.subtasks.every((subtask) => subtask.isCompleted)) {
        continue;
      }
      candidates.push({
        source: 'item',
        id: entry.itemId,
        kind: 'Task',
        title: entry.parentTitle,
        icon: entry.icon,
        slot: null,
        startsAtMs: Math.min(...entry.subtasks.map(startOf)),
        override: overrides.get(entry.itemId),
      });
      continue;
    }

    if (entry.isAllDay || (entry.kind === 'Task' && entry.isCompleted)) {
      continue;
    }
    candidates.push({
      source: 'item',
      id: entry.itemId,
      kind: entry.kind === 'Event' ? 'Event' : 'Task',
      title: entry.title,
      icon: entry.icon,
      slot: null,
      startsAtMs: startOf(entry),
      override: overrides.get(entry.itemId),
    });
  }

  return candidates;
}

// Timed pickups only: the time is a wall clock in the child's own zone. No per-pickup overrides.
function pickupCandidates(pickups: readonly PickupOccurrence[], timeZone: string): Candidate[] {
  return pickups
    .filter((pickup) => pickup.time !== null)
    .map((pickup) => ({
      source: 'pickup' as const,
      id: pickup.slot,
      kind: pickup.slot,
      title: '',
      icon: PICKUP_ICONS[pickup.slot],
      slot: pickup.slot,
      startsAtMs: instantInTimeZone(pickup.date, pickup.time ?? '', timeZone),
      override: undefined,
    }));
}

function minutesOf(time: string): number {
  const [hours = 0, minutes = 0] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

// From > To wraps midnight (21:00-07:00). From is inside, To is not.
export function isInQuietHours(
  quietHours: QuietHours | null,
  nowMs: number,
  timeZone: string,
): boolean {
  if (quietHours === null) {
    return false;
  }

  const now = minutesOfDayInTimeZone(new Date(nowMs), timeZone);
  const from = minutesOf(quietHours.from);
  const to = minutesOf(quietHours.to);
  return from < to ? now >= from && now < to : now >= from || now < to;
}

export function computeTransitionWarnings(input: {
  occurrences: readonly CalendarOccurrence[];
  pickups: readonly PickupOccurrence[];
  settings: MyTransitionWarnings;
  timeZone: string;
  nowMs: number;
}): TransitionWarning[] {
  const { settings, timeZone, nowMs } = input;

  // Off means nothing warns, overrides included: they are kept, not applied.
  if (!settings.enabled || isInQuietHours(settings.quietHours, nowMs, timeZone)) {
    return [];
  }

  const overrides = new Map(settings.overrides.map((o) => [o.itemId, o.leadMinutes]));
  const candidates = [
    ...occurrenceCandidates(input.occurrences, overrides),
    ...pickupCandidates(input.pickups, timeZone),
  ];
  const warnings: TransitionWarning[] = [];

  for (const candidate of candidates) {
    if (candidate.startsAtMs <= nowMs) {
      continue;
    }

    // An item's own override wins over the defaults, whatever its kind; an empty one is "never".
    const leads =
      candidate.override ?? (settings.kinds.includes(candidate.kind) ? settings.leadMinutes : []);
    // The stage is the smallest lead time already reached.
    const reached = leads.filter((lead) => candidate.startsAtMs - lead * 60_000 <= nowMs);
    if (reached.length === 0) {
      continue;
    }

    const leadMinutes = Math.min(...reached);
    const date = toIsoDateInTimeZone(new Date(candidate.startsAtMs), timeZone);
    warnings.push({
      key: `${candidate.source}:${candidate.id}:${date}:${leadMinutes}`,
      kind: candidate.kind,
      title: candidate.title,
      icon: candidate.icon,
      slot: candidate.slot,
      startsAtMs: candidate.startsAtMs,
      leadMinutes,
      firstLeadMinutes: Math.max(...leads),
      isFinalStage: leadMinutes === Math.min(...leads),
    });
  }

  return warnings.sort((a, b) => a.startsAtMs - b.startsAtMs || a.title.localeCompare(b.title));
}

/** Whole minutes left until the warning's start, rounded up (4:01 reads as 5). */
export function minutesUntil(warning: TransitionWarning, nowMs: number): number {
  return Math.max(1, Math.ceil((warning.startsAtMs - nowMs) / 60_000));
}

/** How much of the bar is left: 1 at the first lead time, 0 at the start. */
export function remainingFraction(warning: TransitionWarning, nowMs: number): number {
  const total = warning.firstLeadMinutes * 60_000;
  return Math.min(1, Math.max(0, (warning.startsAtMs - nowMs) / total));
}
