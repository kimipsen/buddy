import { describe, expect, it } from 'vitest';

import { CalendarItemOccurrence } from '../../../core/calendars.service';
import { MealPlanEntry } from '../../../core/mealplans.service';
import { PickupOccurrence } from '../../../core/pickups.service';
import {
  PRINT_ROW_KIND,
  PrintTemplate,
  PrintTemplateRow,
  emptyRow,
} from '../../../core/print-templates.service';
import { WorkDay, WorkLocation } from '../../../core/work-locations.service';
import { assembleWeekPlan } from './assemble-week-plan';
import { WeekPlanSources } from './week-plan-model';
import { FlatOccurrence, nestOccurrence } from '../../../../testing/occurrence-fixture';

const TZ = 'Europe/Copenhagen';
const OPTIONS = {
  start: '2026-09-28',
  locale: 'en',
  timeZone: TZ,
  labels: { week: 'Week', selfEscort: 'Alone', playdate: 'Playdate' },
  includeSubtasks: false,
};

function template(rows: PrintTemplateRow[], overrides: Partial<PrintTemplate> = {}): PrintTemplate {
  return {
    id: 't',
    ownerUserId: 'me',
    ownerGroupId: null,
    name: 'Ugeplan',
    paperSize: 0,
    defaultStartWeekday: 1,
    showWeekNumber: true,
    rows,
    guardianColors: [],
    ...overrides,
  };
}

function row(
  kind: PrintTemplateRow['kind'],
  overrides: Partial<PrintTemplateRow> = {},
): PrintTemplateRow {
  return { ...emptyRow(kind, 'Row'), ...overrides };
}

function sources(overrides: Partial<WeekPlanSources> = {}): WeekPlanSources {
  return {
    meals: new Map(),
    pickups: new Map(),
    workDays: new Map(),
    occurrences: new Map(),
    names: new Map([
      ['dad', 'Far'],
      ['mum', 'Mor'],
      ['signe', 'Signe'],
      ['viggo', 'Viggo'],
    ]),
    ...overrides,
  };
}

function occurrence(
  overrides: Partial<FlatOccurrence<CalendarItemOccurrence>>,
): CalendarItemOccurrence {
  return nestOccurrence<CalendarItemOccurrence>({
    itemId: 'i',
    kind: 0,
    title: 'Item',
    icon: '📅',
    iconOverride: null,
    color: '#000',
    startsAt: null,
    endsAt: null,
    dueAt: null,
    isAllDay: false,
    isCompleted: false,
    createdBy: 'dad',
    lastModifiedBy: 'dad',
    assignedTo: null,
    ...overrides,
  });
}

describe('assembleWeekPlan', () => {
  describe('columns and week number', () => {
    it.each([
      ['2026-09-28', 'Monday', 'Week 40'],
      ['2026-09-29', 'Tuesday', 'Week 40–41'],
      ['2026-10-04', 'Sunday', 'Week 40–41'],
      ['2026-12-28', 'Monday', 'Week 53'],
      ['2027-01-03', 'Sunday', 'Week 53–1'],
    ])('starting %s makes %s column one (%s)', (start, weekday, weekLabel) => {
      const model = assembleWeekPlan(template([]), sources(), { ...OPTIONS, start });

      expect(model.days).toHaveLength(7);
      expect(model.days[0].date).toBe(start);
      expect(model.days[0].weekday).toBe(weekday);
      expect(model.weekLabel).toBe(weekLabel);
    });

    it('omits the week number when the template hides it', () => {
      expect(
        assembleWeekPlan(template([], { showWeekNumber: false }), sources(), OPTIONS).weekLabel,
      ).toBeNull();
    });

    it('carries the paper size and keeps the row order and labels as typed', () => {
      const model = assembleWeekPlan(
        template(
          [
            row(PRINT_ROW_KIND.blank, { label: 'Signes aktiviteter', heightWeight: 3 }),
            row(PRINT_ROW_KIND.blank),
          ],
          {
            paperSize: 1,
          },
        ),
        sources(),
        OPTIONS,
      );

      expect(model.paperSize).toBe(1);
      expect(model.rows.map((r) => r.label)).toEqual(['Signes aktiviteter', 'Row']);
      expect(model.rows[0].heightWeight).toBe(3);
      expect(model.rows[0].cells).toEqual(Array(7).fill({ type: 'blank' }));
    });
  });

  describe('meal rows', () => {
    const entry = (date: string, slot: MealPlanEntry['slot'], mealName: string) =>
      ({ date, slot, mealName }) as MealPlanEntry;

    it('prints the meal for the row’s slot on its day, from the family or group scope', () => {
      const meals = new Map([
        [
          'child:signe',
          [
            entry('2026-09-28', 2, 'Rejer'),
            entry('2026-09-28', 1, 'Rugbrød'),
            entry('2026-09-30', 2, 'Pasta'),
          ],
        ],
        ['group:fam', [entry('2026-09-29', 2, 'Butterchicken')]],
      ]);

      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.meal, { childId: 'signe', mealSlot: 2 }),
          row(PRINT_ROW_KIND.meal, { mealGroupId: 'fam', mealSlot: 2 }),
        ]),
        sources({ meals }),
        OPTIONS,
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'text',
        value: { text: 'Rejer', icon: null, color: null },
      });
      expect(model.rows[0].cells[1]).toEqual({ type: 'blank' });
      expect(model.rows[0].cells[2]).toMatchObject({ value: { text: 'Pasta' } });
      expect(model.rows[1].cells[1]).toMatchObject({ value: { text: 'Butterchicken' } });
    });
  });

  describe('pickup rows', () => {
    const pickup = (overrides: Partial<PickupOccurrence>) =>
      ({
        assignee: { kind: 0, guardianId: 'guardian-1' },
        date: '2026-09-28',
        slot: 0,
        ...overrides,
      }) as PickupOccurrence;

    it('splits drop-off above and pick-up below, coloring guardians and labelling the other kinds', () => {
      const pickups = new Map([
        [
          'signe',
          [
            pickup({ assignee: { kind: 0, guardianId: 'dad' }, slot: 0 }),
            pickup({ assignee: { kind: 0, guardianId: 'mum' }, slot: 1 }),
            pickup({ assignee: { kind: 1 }, date: '2026-09-29', slot: 0 }),
            pickup({ assignee: { kind: 2, siblingChildId: 'viggo' }, date: '2026-09-29', slot: 1 }),
            pickup({
              assignee: { kind: 3, hostName: 'Emma', location: '', contactInfo: '' },
              date: '2026-09-30',
              slot: 1,
            }),
          ],
        ],
      ]);

      const model = assembleWeekPlan(
        template([row(PRINT_ROW_KIND.pickup, { childId: 'signe' })], {
          guardianColors: [{ guardianId: 'dad', color: '#2563eb' }],
        }),
        sources({ pickups }),
        OPTIONS,
      );
      const [monday, tuesday, wednesday, thursday] = model.rows[0].cells;

      expect(monday).toEqual({
        type: 'pickup',
        dropOff: { text: 'Far', icon: null, color: '#2563eb' },
        pickUp: { text: 'Mor', icon: null, color: null },
      });
      expect(tuesday).toMatchObject({ dropOff: { text: 'Alone' }, pickUp: { text: 'Viggo' } });
      // Only the pick-up is planned: the drop-off half stays blank for handwriting.
      expect(wednesday).toMatchObject({ dropOff: null, pickUp: { text: 'Playdate: Emma' } });
      expect(thursday).toEqual({ type: 'blank' });
    });
  });

  describe('work location rows', () => {
    const stil: WorkLocation = {
      id: 'stil',
      name: 'Stil',
      icon: '🏢',
      color: '#0ea5e9',
      isArchived: false,
    };
    const randers: WorkLocation = {
      id: 'randers',
      name: 'Randers',
      icon: '🚆',
      color: '#f43f5e',
      isArchived: true,
    };
    const days: WorkDay[] = [
      { date: '2026-09-28', status: { kind: 2, location: stil, source: 0 } },
      { date: '2026-09-29', status: { kind: 2, location: randers, source: 1 } },
      { date: '2026-09-30', status: { kind: 1 } },
    ];

    it('marks only the days at the chosen location, archived locations included', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.workLocation, { guardianId: 'dad', workLocationId: 'stil' }),
          row(PRINT_ROW_KIND.workLocation, { guardianId: 'dad', workLocationId: 'randers' }),
        ]),
        sources({ workDays: new Map([['dad', days]]) }),
        OPTIONS,
      );

      expect(model.rows[0].cells.slice(0, 3)).toEqual([
        { type: 'mark' },
        { type: 'blank' },
        { type: 'blank' },
      ]);
      expect(model.rows[1].cells[1]).toEqual({ type: 'mark' });
    });

    it('without a location, prints each day’s location in the guardian’s color and leaves days off blank', () => {
      const model = assembleWeekPlan(
        template([row(PRINT_ROW_KIND.workLocation, { guardianId: 'dad' })], {
          guardianColors: [{ guardianId: 'dad', color: '#2563eb' }],
        }),
        sources({ workDays: new Map([['dad', days]]) }),
        OPTIONS,
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'text',
        value: { text: 'Stil', icon: '🏢', color: '#2563eb' },
      });
      expect(model.rows[0].cells[2]).toEqual({ type: 'blank' });
    });
  });

  describe('calendar rows', () => {
    const occurrences = new Map([
      [
        'family',
        [
          occurrence({
            itemId: 'kor',
            title: 'Kor',
            startsAt: '2026-09-29T14:00:00Z',
            endsAt: '2026-09-29T15:00:00Z',
            assignedTo: 'signe',
          }),
          occurrence({
            itemId: 'dans',
            title: 'Dans',
            startsAt: '2026-09-30T15:35:00Z',
            endsAt: '2026-09-30T16:35:00Z',
            assignedTo: 'signe',
          }),
          occurrence({
            itemId: 'trip',
            title: 'Lejrtur',
            startsAt: '2026-09-29T22:00:00Z',
            endsAt: '2026-10-01T22:00:00Z',
            isAllDay: true,
          }),
          occurrence({
            itemId: 'trash',
            title: 'Skraldespand ud',
            startsAt: '2026-10-01T05:00:00Z',
            endsAt: '2026-10-01T06:00:00Z',
          }),
          occurrence({
            itemId: 'affald',
            kind: 1,
            title: 'Affald + pant',
            dueAt: '2026-09-28T16:00:00Z',
            assignedTo: 'viggo',
          }),
          occurrence({
            itemId: 'lektier',
            kind: 1,
            title: 'Lektier',
            dueAt: '2026-10-01T16:00:00Z',
            assignedTo: 'viggo',
          }),
          occurrence({
            itemId: 'routine',
            kind: 1,
            title: 'Brush',
            parentTitle: 'Morgenrutine',
            subtaskId: 's1',
            startsAt: '2026-09-28T05:00:00Z',
            assignedTo: 'viggo',
          }),
          occurrence({
            itemId: 'routine',
            kind: 1,
            title: 'Dress',
            parentTitle: 'Morgenrutine',
            subtaskId: 's2',
            startsAt: '2026-09-28T05:10:00Z',
            assignedTo: 'viggo',
          }),
        ],
      ],
      ['private', null],
    ]);

    it('marks days with an occurrence matching the title filter, case-insensitively', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.calendarMarker, { calendarIds: ['family'], titleFilter: 'SKRALD' }),
        ]),
        sources({ occurrences }),
        OPTIONS,
      );

      expect(model.rows[0].cells.map((c) => c.type)).toEqual([
        'blank',
        'blank',
        'blank',
        'mark',
        'blank',
        'blank',
        'blank',
      ]);
    });

    it('lists an assignee’s events with local start times, all-day items first, spanning their days', () => {
      const model = assembleWeekPlan(
        template([row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['family'], showTime: true })]),
        sources({ occurrences }),
        OPTIONS,
      );
      const [, tuesday, wednesday, thursday, friday] = model.rows[0].cells;

      expect(tuesday).toEqual({
        type: 'list',
        items: [{ time: '16:00', text: 'Kor', assignee: null, subtasks: [] }],
        overflow: 0,
      });
      // The all-day trip covers Wed and Thu (end is exclusive) and sorts before timed items.
      expect(wednesday).toMatchObject({
        items: [
          { text: 'Lejrtur', time: null },
          { text: 'Dans', time: '17:35' },
        ],
      });
      // Events rows list every occurrence, tasks included (Viggo's homework is due at 18:00).
      expect(thursday).toMatchObject({
        items: [
          { text: 'Lejrtur' },
          { text: 'Skraldespand ud', time: '07:00' },
          { text: 'Lektier', time: '18:00' },
        ],
      });
      expect(friday).toEqual({ type: 'blank' });
    });

    it('filters by assignee and shows the assignee’s name when asked', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.calendarEvents, {
            calendarIds: ['family'],
            assignedToId: 'signe',
            showAssignee: true,
          }),
        ]),
        sources({ occurrences }),
        OPTIONS,
      );

      expect(model.rows[0].cells[1]).toEqual({
        type: 'list',
        items: [{ time: null, text: 'Kor', assignee: 'Signe', subtasks: [] }],
        overflow: 0,
      });
      expect(model.rows[0].cells[2]).toMatchObject({ items: [{ text: 'Dans' }] });
    });

    it('caps a cell at maxItems and reports the rest as overflow', () => {
      const model = assembleWeekPlan(
        template([row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['family'], maxItems: 1 })]),
        sources({ occurrences }),
        OPTIONS,
      );

      expect(model.rows[0].cells[2]).toMatchObject({ items: [{ text: 'Lejrtur' }], overflow: 1 });
    });

    it('checklists keep only tasks, grouping a routine’s subtasks under its parent title', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.taskChecklist, { calendarIds: ['family'], assignedToId: 'viggo' }),
        ]),
        sources({ occurrences }),
        OPTIONS,
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'checklist',
        items: [
          { text: 'Morgenrutine', subtasks: [] },
          { text: 'Affald + pant', subtasks: [] },
        ],
        overflow: 0,
      });
      expect(model.rows[0].cells[3]).toEqual({
        type: 'checklist',
        items: [{ text: 'Lektier', subtasks: [] }],
        overflow: 0,
      });
      expect(model.rows[0].cells[1]).toEqual({ type: 'blank' });
    });

    it('lists a routine’s subtasks under its parent title in checklists when asked', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.taskChecklist, { calendarIds: ['family'], assignedToId: 'viggo' }),
        ]),
        sources({ occurrences }),
        { ...OPTIONS, includeSubtasks: true },
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'checklist',
        items: [
          { text: 'Morgenrutine', subtasks: ['Brush', 'Dress'] },
          { text: 'Affald + pant', subtasks: [] },
        ],
        overflow: 0,
      });
    });

    it('prints a routine once in events rows, timed by its first subtask', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.calendarEvents, {
            calendarIds: ['family'],
            assignedToId: 'viggo',
            showTime: true,
          }),
        ]),
        sources({ occurrences }),
        OPTIONS,
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'list',
        items: [
          { time: '07:00', text: 'Morgenrutine', assignee: null, subtasks: [] },
          { time: '18:00', text: 'Affald + pant', assignee: null, subtasks: [] },
        ],
        overflow: 0,
      });
    });

    it('lists a routine’s subtasks under its parent title in events rows when asked', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['family'], assignedToId: 'viggo' }),
        ]),
        sources({ occurrences }),
        { ...OPTIONS, includeSubtasks: true },
      );

      expect(model.rows[0].cells[0]).toMatchObject({
        items: [
          { text: 'Morgenrutine', subtasks: ['Brush', 'Dress'] },
          { text: 'Affald + pant', subtasks: [] },
        ],
      });
    });

    it('counts a routine with its subtasks as one item against maxItems', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.taskChecklist, {
            calendarIds: ['family'],
            assignedToId: 'viggo',
            maxItems: 1,
          }),
        ]),
        sources({ occurrences }),
        { ...OPTIONS, includeSubtasks: true },
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'checklist',
        items: [{ text: 'Morgenrutine', subtasks: ['Brush', 'Dress'] }],
        overflow: 1,
      });
    });

    it('keeps two different tasks that share a title as two tick boxes', () => {
      const twins = new Map([
        [
          'family',
          [
            occurrence({ itemId: 'a', kind: 1, title: 'Støvsug', dueAt: '2026-09-28T08:00:00Z' }),
            occurrence({ itemId: 'b', kind: 1, title: 'Støvsug', dueAt: '2026-09-28T15:00:00Z' }),
          ],
        ],
      ]);

      const model = assembleWeekPlan(
        template([row(PRINT_ROW_KIND.taskChecklist, { calendarIds: ['family'] })]),
        sources({ occurrences: twins }),
        OPTIONS,
      );

      expect(model.rows[0].cells[0]).toEqual({
        type: 'checklist',
        items: [
          { text: 'Støvsug', subtasks: [] },
          { text: 'Støvsug', subtasks: [] },
        ],
        overflow: 0,
      });
    });

    it('counts an item once when two of the row’s calendars return it', () => {
      const model = assembleWeekPlan(
        template([row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['family', 'copy'] })]),
        sources({ occurrences: new Map([...occurrences, ['copy', occurrences.get('family')!]]) }),
        OPTIONS,
      );

      expect(model.rows[0].cells[1]).toMatchObject({ items: [{ text: 'Kor' }] });
    });
  });

  describe('rows still being built', () => {
    it('prints rows without a source yet as blank, not as unavailable', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.pickup),
          row(PRINT_ROW_KIND.workLocation),
          row(PRINT_ROW_KIND.meal, { mealSlot: 2 }),
        ]),
        sources(),
        OPTIONS,
      );

      expect(model.rows.map((r) => r.unavailable)).toEqual([false, false, false]);
      expect(model.rows[0].cells).toEqual(Array(7).fill({ type: 'blank' }));
    });
  });

  describe('unavailable sources', () => {
    it('marks only the rows whose source failed, leaving the rest of the sheet intact', () => {
      const model = assembleWeekPlan(
        template([
          row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['private'] }),
          row(PRINT_ROW_KIND.pickup, { childId: 'gone' }),
          row(PRINT_ROW_KIND.workLocation, { guardianId: 'stranger' }),
          row(PRINT_ROW_KIND.meal, { childId: 'gone', mealSlot: 2 }),
          row(PRINT_ROW_KIND.blank),
        ]),
        sources({ occurrences: new Map([['private', null]]), pickups: new Map([['gone', null]]) }),
        OPTIONS,
      );

      expect(model.rows.map((r) => r.unavailable)).toEqual([true, true, true, true, false]);
      expect(model.rows[0].cells).toEqual(Array(7).fill({ type: 'blank' }));
    });
  });
});
