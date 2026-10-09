import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BabysittersService } from '../../../../core/babysitters.service';
import { CalendarsService } from '../../../../core/calendars.service';
import { GuardiansService } from '../../../../core/guardians.service';
import { GroupsService } from '../../../../core/groups.service';
import {
  PRINT_ROW_KIND,
  PrintTemplate,
  PrintTemplatesService,
  emptyRow,
} from '../../../../core/print-templates.service';
import { CurrentUser, UsersService } from '../../../../core/users.service';
import { WorkLocationsService } from '../../../../core/work-locations.service';
import { WeekPlanLoader } from '../week-plan-loader';
import { PrintTemplateEditor } from './print-template-editor';
import { disableFeatures } from '../../../../../testing/features-fixture';

describe('PrintTemplateEditor', () => {
  const template: PrintTemplate = {
    id: 't-1',
    ownerUserId: 'me',
    ownerGroupId: null,
    name: 'Ugeplan',
    paperSize: 'A4',
    defaultStartWeekday: 'Monday',
    showWeekNumber: true,
    rows: [{ ...emptyRow(PRINT_ROW_KIND.pickup, 'Hente'), childId: 'signe' }],
    guardianColors: [],
    babysitterColors: [],
  };

  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function button(root: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text)!;
  }

  function choose(select: HTMLSelectElement, label: string): void {
    const option = Array.from(select.options).find((o) => o.textContent?.trim() === label);
    expect(option, `option "${label}"`).toBeTruthy();
    select.value = option!.value;
    select.dispatchEvent(new Event('change'));
  }

  beforeEach(() =>
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'Date'], shouldAdvanceTime: true }),
  );
  afterEach(() => vi.useRealTimers());

  async function setup(initial: PrintTemplate = template) {
    const templates = {
      get: vi.fn(async () => initial),
      rename: vi.fn(async (_id: string, name: string) => ({ ...initial, name })),
      updateLayout: vi.fn(async (_id: string, layout: object) => ({ ...initial, ...layout })),
      replaceRows: vi.fn(async (_id: string, rows: PrintTemplate['rows']) => ({
        ...initial,
        rows,
      })),
      replaceColors: vi.fn(
        async (_id: string, guardianColors: PrintTemplate['guardianColors']) => ({
          ...initial,
          guardianColors,
        }),
      ),
      replaceBabysitterColors: vi.fn(
        async (_id: string, babysitterColors: PrintTemplate['babysitterColors']) => ({
          ...initial,
          babysitterColors,
        }),
      ),
      delete: vi.fn(async () => undefined),
    };
    const loader = {
      load: vi.fn(async () => ({
        meals: new Map(),
        pickups: new Map(),
        workDays: new Map(),
        occurrences: new Map(),
        names: new Map(),
      })),
    };

    await TestBed.configureTestingModule({
      imports: [PrintTemplateEditor],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ templateId: 't-1' }) } },
        },
        { provide: PrintTemplatesService, useValue: templates },
        { provide: WeekPlanLoader, useValue: loader },
        {
          provide: GuardiansService,
          useValue: {
            listMyChildren: vi.fn(async () => [
              { id: 'signe', name: { givenName: 'Signe', familyName: 'X' } },
            ]),
            listChildGuardians: vi.fn(async () => [
              { id: 'me', name: { givenName: 'Ida', familyName: 'X' } },
              { id: 'mum', name: { givenName: 'Mor', familyName: 'X' } },
            ]),
          },
        },
        {
          provide: CalendarsService,
          useValue: {
            listMyCalendars: vi.fn(async () => [
              { id: 'school', name: 'Skole', icon: '🏫', role: 'Owner' },
              { id: 'family', name: 'Familie', icon: '📅', role: 'Owner' },
              { id: 'work', name: 'Arbejde', icon: '💼', role: 'Owner' },
            ]),
          },
        },
        { provide: GroupsService, useValue: { listMyGroups: vi.fn(async () => []) } },
        {
          provide: BabysittersService,
          useValue: {
            listMine: vi.fn(async () => [
              { id: 'b-mette', name: 'Mette', contactInfo: '', isArchived: false },
              { id: 'b-old', name: 'Gammel', contactInfo: '', isArchived: true },
            ]),
            // The child's list repeats the guardian's own babysitters.
            listForChild: vi.fn(async () => [
              { guardianId: 'mum', id: 'b-jonas', name: 'Jonas', contactInfo: '' },
              { guardianId: 'me', id: 'b-mette', name: 'Mette', contactInfo: '' },
            ]),
          },
        },
        {
          provide: WorkLocationsService,
          useValue: {
            getSchedule: vi.fn(async (id: string) => ({
              guardianId: id,
              locations:
                id === 'mum'
                  ? [
                      {
                        id: 'randers',
                        name: 'Randers',
                        icon: '🚆',
                        color: '#f43f5e',
                        isArchived: false,
                      },
                    ]
                  : [],
              pattern: { cycleWeeks: 1, anchorMonday: '2026-09-28', days: [] },
            })),
          },
        },
        {
          provide: UsersService,
          useValue: {
            ensureCurrentUser: vi.fn(
              async () =>
                ({ id: 'me', name: { givenName: 'Ida', familyName: 'X' } }) as CurrentUser,
            ),
            timeZoneId: () => 'Europe/Copenhagen',
          },
        },
      ],
    }).compileComponents();

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(PrintTemplateEditor);
    await settle(fixture);

    return { fixture, templates, loader, navigate, root: fixture.nativeElement as HTMLElement };
  }

  it('loads the template into the draft and renders a live preview', async () => {
    const { fixture, root, loader } = await setup();

    expect(root.querySelector<HTMLInputElement>('#template-name')!.value).toBe('Ugeplan');
    expect(root.querySelector('app-week-plan-sheet')).toBeTruthy();

    await vi.advanceTimersByTimeAsync(500);
    await settle(fixture);
    expect(loader.load).toHaveBeenCalled();
  });

  it('saves only what changed: a rename sends no rows, layout or colors', async () => {
    const { fixture, root, templates } = await setup();

    const name = root.querySelector<HTMLInputElement>('#template-name')!;
    name.value = 'Skoleuge';
    name.dispatchEvent(new Event('input'));
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.rename).toHaveBeenCalledWith('t-1', 'Skoleuge');
    expect(templates.updateLayout).not.toHaveBeenCalled();
    expect(templates.replaceRows).not.toHaveBeenCalled();
    expect(templates.replaceColors).not.toHaveBeenCalled();
    expect(templates.replaceBabysitterColors).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Template saved.');
  });

  it('hides "Template saved." again as soon as the draft changes', async () => {
    const { fixture, root } = await setup();
    const name = root.querySelector<HTMLInputElement>('#template-name')!;

    name.value = 'Skoleuge';
    name.dispatchEvent(new Event('input'));
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);
    expect(root.textContent).toContain('Template saved.');

    Array.from(root.querySelectorAll<HTMLButtonElement>('[role="radio"]'))
      .find((b) => b.textContent?.trim() === 'A3')!
      .click();
    await settle(fixture);
    expect(root.textContent).not.toContain('Template saved.');
  });

  it('collapses quick edits into one preview reload', async () => {
    const { fixture, root, loader } = await setup();
    await vi.advanceTimersByTimeAsync(500);
    await settle(fixture);
    const before = loader.load.mock.calls.length;

    choose(root.querySelector<HTMLSelectElement>('#new-row-kind')!, 'Blank');
    button(root, 'Add row').click();
    await settle(fixture);
    button(root, 'Add row').click();
    await settle(fixture);
    expect(loader.load.mock.calls.length).toBe(before);

    await vi.advanceTimersByTimeAsync(500);
    await settle(fixture);
    expect(loader.load.mock.calls.length).toBe(before + 1);
  });

  it('switching a row’s kind shows only that kind’s fields and saves no stray values', async () => {
    const { fixture, root, templates } = await setup();
    const rowKey = root
      .querySelector<HTMLSelectElement>('select[id^="row-kind-"]')!
      .id.replace('row-kind-', '');

    expect(root.querySelector(`#row-child-${rowKey}`)).toBeTruthy();
    choose(root.querySelector<HTMLSelectElement>(`#row-kind-${rowKey}`)!, 'Work location');
    await settle(fixture);
    expect(root.querySelector(`#row-child-${rowKey}`)).toBeNull();

    // Save stays disabled until the work-location row has a guardian.
    expect(button(root, 'Save').disabled).toBe(true);
    expect(root.textContent).toContain('Choose a guardian.');

    choose(root.querySelector<HTMLSelectElement>(`#row-guardian-${rowKey}`)!, 'Mor X');
    await settle(fixture);
    choose(root.querySelector<HTMLSelectElement>(`#row-location-${rowKey}`)!, '🚆 Randers');
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.replaceRows).toHaveBeenCalledWith('t-1', [
      {
        ...emptyRow(PRINT_ROW_KIND.workLocation, 'Hente'),
        guardianId: 'mum',
        workLocationId: 'randers',
      },
    ]);
  });

  it('adds, reorders and removes rows', async () => {
    const { fixture, root, templates } = await setup();

    choose(root.querySelector<HTMLSelectElement>('#new-row-kind')!, 'Blank');
    button(root, 'Add row').click();
    await settle(fixture);
    root.querySelectorAll<HTMLButtonElement>('button[aria-label="Move row up"]')[1].click();
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.replaceRows).toHaveBeenLastCalledWith('t-1', [
      emptyRow(PRINT_ROW_KIND.blank, ''),
      { ...emptyRow(PRINT_ROW_KIND.pickup, 'Hente'), childId: 'signe' },
    ]);

    Array.from(root.querySelectorAll('button'))
      .filter((b) => b.textContent?.trim() === 'Remove')[0]
      .click();
    await settle(fixture);
    expect(root.querySelectorAll('select[id^="row-kind-"]')).toHaveLength(1);
  });

  it('changes the layout and a guardian color', async () => {
    const { fixture, root, templates } = await setup();

    Array.from(root.querySelectorAll<HTMLButtonElement>('[role="radio"]'))
      .find((b) => b.textContent?.trim() === 'A3')!
      .click();
    choose(root.querySelector<HTMLSelectElement>('#template-weekday')!, 'Sunday');
    root
      .querySelector<HTMLButtonElement>('button[role="switch"][aria-label="Show week number"]')!
      .click();
    root
      .querySelector<HTMLElement>('app-color-swatch-picker')!
      .querySelector<HTMLButtonElement>('[role="radio"]')!
      .click();
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.updateLayout).toHaveBeenCalledWith('t-1', {
      paperSize: 'A3',
      defaultStartWeekday: 'Sunday',
      showWeekNumber: false,
    });
    expect(templates.replaceColors).toHaveBeenCalledWith('t-1', [
      { guardianId: 'me', color: '#f43f5e' },
    ]);
  });

  it('lists each active babysitter once and saves a babysitter color by its guardian', async () => {
    const { fixture, root, templates } = await setup();

    expect(root.textContent).toContain('Babysitter colors');
    const pickers = root.querySelectorAll<HTMLElement>(
      'app-color-swatch-picker [role="radiogroup"]',
    );
    const labels = Array.from(pickers).map((p) => p.getAttribute('aria-label'));
    expect(labels.filter((l) => l === 'Mette' || l === 'Jonas' || l === 'Gammel')).toEqual([
      'Jonas',
      'Mette',
    ]);

    root
      .querySelector<HTMLElement>('[role="radiogroup"][aria-label="Jonas"]')!
      .querySelector<HTMLButtonElement>('[role="radio"]')!
      .click();
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.replaceColors).not.toHaveBeenCalled();
    expect(templates.replaceBabysitterColors).toHaveBeenCalledWith('t-1', [
      { guardianId: 'mum', babysitterId: 'b-jonas', color: '#f43f5e' },
    ]);
  });

  it('clears a babysitter color', async () => {
    const { fixture, root, templates } = await setup({
      ...template,
      babysitterColors: [{ guardianId: 'me', babysitterId: 'b-mette', color: '#a855f7' }],
    });

    button(root, 'No color').click();
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.replaceBabysitterColors).toHaveBeenCalledWith('t-1', []);
  });

  it('lists the guardian themself first among work-location guardians', async () => {
    const { root } = await setup({
      ...template,
      rows: [{ ...emptyRow(PRINT_ROW_KIND.workLocation, 'Mig'), guardianId: 'me' }],
    });
    const guardianSelect = root.querySelector<HTMLSelectElement>('select[id^="row-guardian-"]')!;
    expect(Array.from(guardianSelect.options).map((o) => o.textContent?.trim())).toEqual([
      'Choose…',
      'Ida X',
      'Mor X',
    ]);
    expect(root.textContent).not.toContain('Refers to something you can’t see.');
  });

  it('fills the draft from the example without saving', async () => {
    const { fixture, root, templates } = await setup();

    button(root, 'Start from example').click();
    await settle(fixture);

    const labels = Array.from(
      root.querySelectorAll<HTMLInputElement>('input[id^="row-label-"]'),
    ).map((i) => i.value);
    expect(labels).toContain('Dinner');
    expect(labels).toContain('Mor at Randers');
    expect(templates.replaceRows).not.toHaveBeenCalled();
  });

  it('flags a row that refers to something the guardian can’t see', async () => {
    const { root } = await setup({
      ...template,
      rows: [
        { ...emptyRow(PRINT_ROW_KIND.calendarEvents, 'Privat'), calendarIds: ['someone-elses'] },
      ],
    });

    expect(root.textContent).toContain('Refers to something you can’t see.');
  });

  it('lets a calendar the guardian can’t see be unticked without losing the row', async () => {
    const { fixture, root, templates } = await setup({
      ...template,
      rows: [
        {
          ...emptyRow(PRINT_ROW_KIND.calendarEvents, 'Aftaler'),
          calendarIds: ['family', 'someone-elses'],
        },
      ],
    });

    root
      .querySelector<HTMLButtonElement>(
        'button[role="switch"][aria-label="A calendar you can’t see"]',
      )!
      .click();
    await settle(fixture);
    expect(root.textContent).not.toContain('Refers to something you can’t see.');
    button(root, 'Save').click();
    await settle(fixture);

    expect(templates.replaceRows).toHaveBeenCalledWith('t-1', [
      expect.objectContaining({ label: 'Aftaler', calendarIds: ['family'] }),
    ]);
  });

  // CalendarsService.listMyCalendars sorts by name; the editor keeps that order.
  it('lists the calendars to load from in the order the service returns them', async () => {
    const { root } = await setup({
      ...template,
      rows: [emptyRow(PRINT_ROW_KIND.calendarEvents, 'Aftaler')],
    });

    const calendarNames = new Set(['Skole', 'Familie', 'Arbejde']);
    const labels = Array.from(root.querySelectorAll('button[role="switch"]'))
      .map((b) => b.getAttribute('aria-label'))
      .filter((label) => calendarNames.has(label ?? ''));
    expect(labels).toEqual(['Skole', 'Familie', 'Arbejde']);
  });

  it('flags a work-location row whose location was archived', async () => {
    const { root } = await setup({
      ...template,
      rows: [
        {
          ...emptyRow(PRINT_ROW_KIND.workLocation, 'Mor'),
          guardianId: 'mum',
          workLocationId: 'gone',
        },
      ],
    });

    expect(root.textContent).toContain('Refers to something you can’t see.');
  });

  it('deletes after confirmation and returns to printing', async () => {
    const { fixture, root, templates, navigate } = await setup();

    button(root, 'Delete template').click();
    await settle(fixture);
    expect(templates.delete).not.toHaveBeenCalled();
    button(root, 'Delete').click();
    await settle(fixture);

    expect(templates.delete).toHaveBeenCalledWith('t-1');
    expect(navigate).toHaveBeenCalledWith(['/guardian/print']);
  });

  it('shows the save error and keeps the draft', async () => {
    const { fixture, root, templates } = await setup();
    templates.rename.mockRejectedValue(new Error('400'));

    const name = root.querySelector<HTMLInputElement>('#template-name')!;
    name.value = 'Skoleuge';
    name.dispatchEvent(new Event('input'));
    await settle(fixture);
    button(root, 'Save').click();
    await settle(fixture);

    expect(root.textContent).toContain('Unable to save the template.');
    expect(root.querySelector<HTMLInputElement>('#template-name')!.value).toBe('Skoleuge');
  });

  it('offers no rows from features that are turned off, and does not look their data up', async () => {
    disableFeatures('mealplans', 'pickups', 'workLocations', 'babysitters');
    const { fixture, root } = await setup();

    const kinds = Array.from(root.querySelectorAll<HTMLOptionElement>('#new-row-kind option')).map(
      (option) => option.textContent?.trim(),
    );
    expect(kinds).toHaveLength(4);

    button(root, 'Start from example').click();
    await settle(fixture);
    const labels = Array.from(
      root.querySelectorAll<HTMLInputElement>('input[id^="row-label-"]'),
    ).map((i) => i.value);
    expect(labels).not.toContain('Dinner');
    expect(labels).not.toContain('Mor at Randers');

    const babysitters = TestBed.inject(BabysittersService);
    expect(babysitters.listMine).not.toHaveBeenCalled();
    expect(babysitters.listForChild).not.toHaveBeenCalled();
    expect(TestBed.inject(WorkLocationsService).getSchedule).not.toHaveBeenCalled();
  });

  it('keeps the kind of an existing row from a turned-off feature in its own select', async () => {
    disableFeatures('pickups');
    const { root } = await setup();

    const rowKind = root.querySelector<HTMLSelectElement>('select[id^="row-kind-"]')!;
    expect(rowKind.selectedOptions[0]?.textContent?.trim()).toBe('Drop-off / pick-up');

    const newRowKinds = Array.from(
      root.querySelectorAll<HTMLOptionElement>('#new-row-kind option'),
    ).map((option) => option.textContent?.trim());
    expect(newRowKinds).not.toContain('Drop-off / pick-up');
  });
});
