import { describe, expect, it } from 'vitest';

import { nestOccurrence, FlatOccurrence } from '../../testing/occurrence-fixture';
import { CalendarOccurrence } from './calendars.service';
import { PickupOccurrence } from './pickups.service';
import {
  MyTransitionWarnings,
  computeTransitionWarnings,
  isInQuietHours,
  minutesUntil,
  remainingFraction,
} from './transition-warnings';

const ZONE = 'Europe/Copenhagen';
// 2026-10-13 is CEST (UTC+2): 16:00 local is 14:00Z.
const at = (time: string): number => Date.parse(`2026-10-13T${time}:00+02:00`);
const iso = (time: string): string => new Date(at(time)).toISOString();

const SETTINGS: MyTransitionWarnings = {
  enabled: true,
  leadMinutes: [15, 5, 1],
  kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
  quietHours: null,
  overrides: [],
};

const base = {
  kind: 'Event' as const,
  icon: '⚽',
  iconOverride: null,
  color: '#0f0',
  isAllDay: false,
  isCompleted: false,
  createdBy: 'g',
  lastModifiedBy: 'g',
  assignedTo: null,
  calendarId: 'cal-1',
  calendarName: 'Family',
};

function occurrence(
  fields: Partial<FlatOccurrence<CalendarOccurrence>> & { itemId: string; title: string },
): CalendarOccurrence {
  return nestOccurrence<CalendarOccurrence>({
    ...base,
    ...fields,
  } as FlatOccurrence<CalendarOccurrence>);
}

const football = occurrence({
  itemId: 'football',
  title: 'Football',
  startsAt: iso('16:30'),
  endsAt: iso('17:30'),
});

function pickup(time: string | null, slot: 'DropOff' | 'PickUp' = 'PickUp'): PickupOccurrence {
  return {
    date: '2026-10-13',
    slot,
    assignee: { kind: 0, guardianId: 'g' } as PickupOccurrence['assignee'],
    time,
    notes: '',
    assignedBy: 'g',
  };
}

function warnings(
  nowTime: string,
  input: {
    occurrences?: CalendarOccurrence[];
    pickups?: PickupOccurrence[];
    settings?: Partial<MyTransitionWarnings>;
  } = {},
) {
  return computeTransitionWarnings({
    occurrences: input.occurrences ?? [football],
    pickups: input.pickups ?? [],
    settings: { ...SETTINGS, ...input.settings },
    timeZone: ZONE,
    nowMs: at(nowTime),
  });
}

describe('computeTransitionWarnings', () => {
  it('warns nothing before the first lead time', () => {
    expect(warnings('16:14')).toEqual([]);
  });

  it('shows the stage reached: 15 at 16:15, 5 at 16:25, 1 at 16:29', () => {
    expect(warnings('16:15').map((w) => w.leadMinutes)).toEqual([15]);
    expect(warnings('16:21').map((w) => w.leadMinutes)).toEqual([15]);
    expect(warnings('16:25').map((w) => w.leadMinutes)).toEqual([5]);
    expect(warnings('16:29').map((w) => w.leadMinutes)).toEqual([1]);
  });

  it('describes the warning: key, title, icon, start and the final stage', () => {
    const [early] = warnings('16:20');
    expect(early).toEqual({
      key: 'item:football:2026-10-13:15',
      kind: 'Event',
      title: 'Football',
      icon: '⚽',
      slot: null,
      startsAtMs: at('16:30'),
      leadMinutes: 15,
      firstLeadMinutes: 15,
      isFinalStage: false,
    });
    expect(warnings('16:29')[0]?.isFinalStage).toBe(true);
  });

  it('stops once the item has started', () => {
    expect(warnings('16:30')).toEqual([]);
    expect(warnings('16:45')).toEqual([]);
  });

  it('warns nothing while switched off, overrides included', () => {
    expect(
      warnings('16:25', {
        settings: { enabled: false, overrides: [{ itemId: 'football', leadMinutes: [30] }] },
      }),
    ).toEqual([]);
  });

  it('skips a kind the guardian left out', () => {
    expect(warnings('16:25', { settings: { kinds: ['Task'] } })).toEqual([]);
  });

  it('uses an item override’s own lead times, whatever the kinds', () => {
    const result = warnings('16:05', {
      settings: { kinds: ['Task'], overrides: [{ itemId: 'football', leadMinutes: [30, 10] }] },
    });
    expect(result.map((w) => [w.leadMinutes, w.firstLeadMinutes])).toEqual([[30, 30]]);
    expect(
      warnings('16:22', {
        settings: { overrides: [{ itemId: 'football', leadMinutes: [30, 10] }] },
      })[0]?.isFinalStage,
    ).toBe(true);
  });

  it('never warns for an item whose override is empty', () => {
    expect(
      warnings('16:29', { settings: { overrides: [{ itemId: 'football', leadMinutes: [] }] } }),
    ).toEqual([]);
  });

  it('never warns for an all-day event or a completed task', () => {
    const allDay = occurrence({
      itemId: 'trip',
      title: 'Trip',
      isAllDay: true,
      startsAt: iso('16:30'),
    });
    const done = occurrence({
      itemId: 'bag',
      title: 'Pack your bag',
      kind: 'Task',
      isCompleted: true,
      dueAt: iso('16:30'),
    });
    expect(warnings('16:25', { occurrences: [allDay, done] })).toEqual([]);
  });

  it('warns for an event whatever its completed flag, which only tasks use', () => {
    const event = occurrence({
      itemId: 'football',
      title: 'Football',
      isCompleted: true,
      startsAt: iso('16:30'),
    });
    expect(warnings('16:25', { occurrences: [event] })).toMatchObject([
      { kind: 'Event', title: 'Football' },
    ]);
  });

  it('warns before a plain task’s due time', () => {
    const task = occurrence({
      itemId: 'bag',
      title: 'Pack your bag',
      kind: 'Task',
      dueAt: iso('07:30'),
    });
    expect(warnings('07:26', { occurrences: [task] })).toMatchObject([
      { kind: 'Task', title: 'Pack your bag', leadMinutes: 5 },
    ]);
  });

  it('warns once for a routine, before its first subtask, under the routine’s title', () => {
    const subtasks = [
      ['teeth', '20:00', '20:05'],
      ['pyjamas', '20:05', '20:10'],
    ].map(([subtaskId, from, to]) =>
      occurrence({
        itemId: 'bed',
        title: subtaskId ?? '',
        kind: 'Task',
        startsAt: iso(from ?? ''),
        endsAt: iso(to ?? ''),
        subtaskId,
        parentTitle: 'Go to bed',
        parentIcon: '🛏️',
      }),
    );

    expect(warnings('19:56', { occurrences: subtasks })).toMatchObject([
      { title: 'Go to bed', icon: '🛏️', kind: 'Task', startsAtMs: at('20:00'), leadMinutes: 5 },
    ]);
    // Once the routine is under way, moving between its steps isn't a transition.
    expect(warnings('20:04', { occurrences: subtasks })).toEqual([]);
  });

  it('never warns for a routine with an all-day step', () => {
    const subtasks = [
      ['trip', '20:30', true],
      ['teeth', '20:00', false],
    ].map(([subtaskId, from, isAllDay]) =>
      occurrence({
        itemId: 'day',
        title: String(subtaskId),
        kind: 'Task',
        isAllDay: Boolean(isAllDay),
        startsAt: iso(String(from)),
        subtaskId: String(subtaskId),
        parentTitle: 'Holiday',
        parentIcon: '🏖️',
      }),
    );

    expect(warnings('19:56', { occurrences: subtasks })).toEqual([]);
  });

  it('warns before a packing list’s "leave by" time, until every item is packed', () => {
    const items = ['Boots', 'Water bottle'].map((title, index) =>
      occurrence({
        itemId: 'football-bag',
        title,
        kind: 'Task',
        dueAt: iso('07:45'),
        isCompleted: index === 0,
        routine: {
          subtaskId: `s${index}`,
          parentTitle: 'Football bag',
          parentIcon: '🎒',
          checklist: { eveningBeforeFrom: '18:00:00' },
        },
      }),
    );

    expect(warnings('07:40', { occurrences: items })).toMatchObject([
      { title: 'Football bag', kind: 'Task', startsAtMs: at('07:45'), leadMinutes: 5 },
    ]);
    const packed = items.map((item) => ({ ...item, isCompleted: true }));
    expect(warnings('07:40', { occurrences: packed })).toEqual([]);
  });

  it('warns before a timed pickup in the child’s zone, by its slot', () => {
    const result = warnings('14:57', { occurrences: [], pickups: [pickup('15:00:00')] });
    expect(result).toEqual([
      expect.objectContaining({
        key: 'pickup:PickUp:2026-10-13:5',
        kind: 'PickUp',
        slot: 'PickUp',
        title: '',
        icon: '👋',
        startsAtMs: at('15:00'),
      }),
    ]);
    expect(
      warnings('07:57', { occurrences: [], pickups: [pickup('08:00:00', 'DropOff')] }),
    ).toEqual([expect.objectContaining({ kind: 'DropOff', slot: 'DropOff', icon: '🎒' })]);
  });

  it('skips a pickup with no time, and a slot whose kind is left out', () => {
    expect(warnings('14:57', { occurrences: [], pickups: [pickup(null)] })).toEqual([]);
    expect(
      warnings('07:55', {
        occurrences: [],
        pickups: [pickup('08:00', 'DropOff')],
        settings: { kinds: ['Event', 'Task', 'PickUp'] },
      }),
    ).toEqual([]);
  });

  it('lists overlapping warnings soonest first', () => {
    const swim = occurrence({ itemId: 'swim', title: 'Swimming', startsAt: iso('16:20') });
    expect(warnings('16:16', { occurrences: [football, swim] }).map((w) => w.title)).toEqual([
      'Swimming',
      'Football',
    ]);
  });

  it('drops warnings during quiet hours rather than showing them late', () => {
    const quiet = { quietHours: { from: '16:00:00', to: '16:26:00' } };
    expect(warnings('16:25', { settings: quiet })).toEqual([]);
    expect(warnings('16:26', { settings: quiet }).map((w) => w.leadMinutes)).toEqual([5]);
  });
});

describe('isInQuietHours', () => {
  it('is false with no quiet hours', () => {
    expect(isInQuietHours(null, at('12:00'), ZONE)).toBe(false);
  });

  it('includes From and excludes To', () => {
    const school = { from: '08:00:00', to: '14:00:00' };
    expect(isInQuietHours(school, at('07:59'), ZONE)).toBe(false);
    expect(isInQuietHours(school, at('08:00'), ZONE)).toBe(true);
    expect(isInQuietHours(school, at('13:59'), ZONE)).toBe(true);
    expect(isInQuietHours(school, at('14:00'), ZONE)).toBe(false);
  });

  it('wraps midnight when From is after To', () => {
    const night = { from: '21:00', to: '07:00' };
    expect(isInQuietHours(night, at('20:59'), ZONE)).toBe(false);
    expect(isInQuietHours(night, at('21:00'), ZONE)).toBe(true);
    expect(isInQuietHours(night, at('23:30'), ZONE)).toBe(true);
    expect(isInQuietHours(night, at('06:59'), ZONE)).toBe(true);
    expect(isInQuietHours(night, at('07:00'), ZONE)).toBe(false);
  });

  it('reads the clock in the child’s zone, not UTC', () => {
    // 21:30 in Copenhagen is 19:30Z.
    expect(isInQuietHours({ from: '21:00', to: '07:00' }, at('21:30'), ZONE)).toBe(true);
    expect(isInQuietHours({ from: '21:00', to: '07:00' }, at('21:30'), 'UTC')).toBe(false);
  });
});

describe('minutesUntil and remainingFraction', () => {
  const [warning] = computeTransitionWarnings({
    occurrences: [football],
    pickups: [],
    settings: SETTINGS,
    timeZone: ZONE,
    nowMs: at('16:16'),
  });

  it('rounds the minutes left up, never below 1', () => {
    expect(warning).toBeDefined();
    if (!warning) return;
    expect(minutesUntil(warning, at('16:16'))).toBe(14);
    expect(minutesUntil(warning, at('16:16') + 30_000)).toBe(14);
    expect(minutesUntil(warning, at('16:30') - 1_000)).toBe(1);
    expect(minutesUntil(warning, at('16:31'))).toBe(1);
  });

  it('drains from the first lead time to the start', () => {
    if (!warning) return;
    expect(remainingFraction(warning, at('16:15'))).toBe(1);
    expect(remainingFraction(warning, at('16:00'))).toBe(1);
    expect(remainingFraction(warning, at('16:27'))).toBeCloseTo(0.2);
    expect(remainingFraction(warning, at('16:40'))).toBe(0);
  });
});
