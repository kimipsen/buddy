import { describe, expect, it } from 'vitest';

import { PRINT_ROW_KIND, PrintTemplateRow, emptyRow } from '../../../core/print-templates.service';
import { cleanRow, exampleRows, missingField } from './template-rows';

function row(
  kind: PrintTemplateRow['kind'],
  overrides: Partial<PrintTemplateRow> = {},
): PrintTemplateRow {
  return { ...emptyRow(kind, 'Row'), ...overrides };
}

describe('cleanRow', () => {
  it('drops fields left over from a previous kind and trims text', () => {
    const switched = row(PRINT_ROW_KIND.pickup, {
      label: '  Hente  ',
      childId: 'signe',
      calendarIds: ['family'],
      showTime: true,
      guardianId: 'dad',
      heightWeight: 2,
    });

    expect(cleanRow(switched)).toEqual({
      ...emptyRow(PRINT_ROW_KIND.pickup, 'Hente'),
      childId: 'signe',
      heightWeight: 2,
    });
  });

  it('keeps exactly one meal scope, preferring the group when both are set', () => {
    expect(
      cleanRow(
        row(PRINT_ROW_KIND.meal, { childId: 'signe', mealGroupId: 'fam', mealSlot: 'Dinner' }),
      ),
    ).toMatchObject({
      childId: null,
      mealGroupId: 'fam',
      mealSlot: 'Dinner',
    });
  });

  it('turns a blank title filter into null and keeps the event options', () => {
    const cleaned = cleanRow(
      row(PRINT_ROW_KIND.calendarEvents, {
        calendarIds: ['a'],
        titleFilter: '  ',
        maxItems: 3,
        showAssignee: true,
        childId: 'x',
      }),
    );

    expect(cleaned).toMatchObject({
      titleFilter: null,
      maxItems: 3,
      showAssignee: true,
      childId: null,
      calendarIds: ['a'],
    });
  });

  it('clears event-only options on a checklist and everything on a blank row', () => {
    expect(
      cleanRow(
        row(PRINT_ROW_KIND.taskChecklist, { calendarIds: ['a'], showTime: true, titleFilter: 'x' }),
      ),
    ).toMatchObject({
      showTime: false,
      titleFilter: null,
    });
    expect(cleanRow(row(PRINT_ROW_KIND.blank, { label: '', calendarIds: ['a'] }))).toEqual(
      emptyRow(PRINT_ROW_KIND.blank, ''),
    );
  });
});

describe('missingField', () => {
  it.each([
    [row(PRINT_ROW_KIND.pickup, { label: ' ' }), 'print.editor.missing.label'],
    [row(PRINT_ROW_KIND.meal, { mealSlot: 'Dinner' }), 'print.editor.missing.mealScope'],
    [row(PRINT_ROW_KIND.meal, { childId: 'signe' }), 'print.editor.missing.mealSlot'],
    [row(PRINT_ROW_KIND.pickup), 'print.editor.missing.child'],
    [row(PRINT_ROW_KIND.workLocation), 'print.editor.missing.guardian'],
    [row(PRINT_ROW_KIND.calendarMarker, { calendarIds: [] }), 'print.editor.missing.calendars'],
    [row(PRINT_ROW_KIND.taskChecklist), 'print.editor.missing.calendars'],
  ])('reports what a row still needs (%#)', (incomplete, key) => {
    expect(missingField(incomplete)).toBe(key);
  });

  it('enforces the backend label and calendar limits', () => {
    expect(missingField(row(PRINT_ROW_KIND.blank, { label: 'x'.repeat(41) }))).toBe(
      'print.editor.missing.labelTooLong',
    );
    expect(missingField(row(PRINT_ROW_KIND.blank, { label: 'x'.repeat(40) }))).toBeNull();
    const eleven = Array.from({ length: 11 }, (_, i) => `c${i}`);
    expect(missingField(row(PRINT_ROW_KIND.calendarMarker, { calendarIds: eleven }))).toBe(
      'print.editor.missing.tooManyCalendars',
    );
  });

  it('accepts complete rows, and a blank row without a label', () => {
    expect(missingField(row(PRINT_ROW_KIND.blank, { label: '' }))).toBeNull();
    expect(
      missingField(row(PRINT_ROW_KIND.meal, { mealGroupId: 'fam', mealSlot: 'Breakfast' })),
    ).toBeNull();
    expect(missingField(row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['a'] }))).toBeNull();
  });
});

describe('exampleRows', () => {
  const labels = {
    dinner: 'Dinner',
    pickup: (c: string) => `${c}: drop-off / pick-up`,
    atLocation: (g: string, l: string) => `${g} at ${l}`,
    activities: (c: string) => `${c}'s activities`,
    chores: (c: string) => `${c}'s chores`,
    appointments: 'Appointments',
    notes: 'Notes',
  };
  const child = (id: string, givenName: string) =>
    ({ id, name: { givenName, familyName: 'X' } }) as never;
  const guardian = (id: string, givenName: string) =>
    ({ id, name: { givenName, familyName: 'X' } }) as never;

  it('mirrors the fridge sheet from the family’s own children, guardians and calendars', () => {
    const rows = exampleRows({
      children: [child('signe', 'Signe')],
      guardians: [guardian('dad', 'Far')],
      calendars: [{ id: 'family', name: 'Familie', icon: '📅', role: 'Owner' } as never],
      workLocations: new Map([
        ['dad', [{ id: 'stil', name: 'Stil', icon: '🏢', color: '#000', isArchived: false }]],
      ]),
      labels,
    });

    expect(rows.map((r) => [r.kind, r.label])).toEqual([
      [PRINT_ROW_KIND.meal, 'Dinner'],
      [PRINT_ROW_KIND.pickup, 'Signe: drop-off / pick-up'],
      [PRINT_ROW_KIND.workLocation, 'Far at Stil'],
      [PRINT_ROW_KIND.calendarEvents, "Signe's activities"],
      [PRINT_ROW_KIND.taskChecklist, "Signe's chores"],
      [PRINT_ROW_KIND.calendarEvents, 'Appointments'],
      [PRINT_ROW_KIND.blank, 'Notes'],
    ]);
    expect(rows.every((r) => missingField(r) === null)).toBe(true);
    expect(rows[3]).toMatchObject({ calendarIds: ['family'], assignedToId: 'signe' });
  });

  it('cuts labels built from long names to the backend limit', () => {
    const rows = exampleRows({
      children: [child('c', 'Maximiliana-Kristiansen-Bøgelund-Hansen')],
      guardians: [],
      calendars: [],
      workLocations: new Map(),
      labels,
    });

    expect(rows.every((r) => r.label.length <= 40)).toBe(true);
    expect(rows.every((r) => missingField(r) === null)).toBe(true);
  });

  it('skips calendar rows without calendars and never exceeds 12 rows', () => {
    const many = Array.from({ length: 20 }, (_, i) => child(`c${i}`, `Kid ${i}`));
    const rows = exampleRows({
      children: many,
      guardians: [],
      calendars: [],
      workLocations: new Map(),
      labels,
    });

    expect(rows).toHaveLength(12);
    expect(rows.some((r) => r.kind === PRINT_ROW_KIND.calendarEvents)).toBe(false);
    expect(rows[11].label).toBe('Notes');
  });
});
