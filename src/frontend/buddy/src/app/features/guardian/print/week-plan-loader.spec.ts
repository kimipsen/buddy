import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { CalendarsService } from '../../../core/calendars.service';
import { GuardiansService } from '../../../core/guardians.service';
import { MealplansService } from '../../../core/mealplans.service';
import { PickupsService } from '../../../core/pickups.service';
import {
  PRINT_ROW_KIND,
  PrintTemplate,
  PrintTemplateRow,
  emptyRow,
} from '../../../core/print-templates.service';
import { WorkLocationsService } from '../../../core/work-locations.service';
import { WeekPlanLoader } from './week-plan-loader';
import { disableFeatures } from '../../../../testing/features-fixture';

describe('WeekPlanLoader', () => {
  function row(
    kind: PrintTemplateRow['kind'],
    overrides: Partial<PrintTemplateRow>,
  ): PrintTemplateRow {
    return { ...emptyRow(kind, 'Row'), ...overrides };
  }

  function template(rows: PrintTemplateRow[]): PrintTemplate {
    return {
      id: 't',
      ownerUserId: 'me',
      ownerGroupId: null,
      name: 'Ugeplan',
      paperSize: 'A4',
      defaultStartWeekday: 'Monday',
      showWeekNumber: true,
      rows,
      guardianColors: [],
      babysitterColors: [],
    };
  }

  function setup() {
    const calendars = {
      listOccurrences: vi.fn(async (id: string) =>
        id === 'private' ? Promise.reject(new Error('404')) : [],
      ),
    };
    const mealplans = { listMealPlan: vi.fn(async () => []) };
    const pickups = { listSchedule: vi.fn(async () => []) };
    const workLocations = { listWorkDays: vi.fn(async () => []) };
    const guardians = {
      listMyChildren: vi.fn(async () => [
        { id: 'signe', name: { givenName: 'Signe', familyName: 'X' } },
      ]),
      listChildGuardians: vi.fn(async () => [
        { id: 'dad', name: { givenName: 'Far', familyName: 'X' } },
        { id: 'mum', name: { givenName: 'Mor', familyName: 'X' } },
      ]),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: CalendarsService, useValue: calendars },
        { provide: MealplansService, useValue: mealplans },
        { provide: PickupsService, useValue: pickups },
        { provide: WorkLocationsService, useValue: workLocations },
        { provide: GuardiansService, useValue: guardians },
      ],
    });

    return {
      loader: TestBed.inject(WeekPlanLoader),
      calendars,
      mealplans,
      pickups,
      workLocations,
      guardians,
    };
  }

  it('fetches each source once for the seven-day range, however many rows name it', async () => {
    const { loader, calendars, mealplans, pickups, workLocations } = setup();

    await loader.load(
      template([
        row(PRINT_ROW_KIND.calendarMarker, { calendarIds: ['family'] }),
        row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['family', 'school'] }),
        row(PRINT_ROW_KIND.taskChecklist, { calendarIds: ['school'] }),
        row(PRINT_ROW_KIND.pickup, { childId: 'signe' }),
        row(PRINT_ROW_KIND.pickup, { childId: 'signe' }),
        row(PRINT_ROW_KIND.workLocation, { guardianId: 'dad', workLocationId: 'stil' }),
        row(PRINT_ROW_KIND.workLocation, { guardianId: 'dad' }),
        row(PRINT_ROW_KIND.meal, { childId: 'signe', mealSlot: 'Dinner' }),
        row(PRINT_ROW_KIND.meal, { childId: 'signe', mealSlot: 'Lunch' }),
        row(PRINT_ROW_KIND.meal, { mealGroupId: 'fam', mealSlot: 'Dinner' }),
        row(PRINT_ROW_KIND.blank, {}),
      ]),
      '2026-10-04',
    );

    expect(calendars.listOccurrences.mock.calls).toEqual([
      ['family', '2026-10-04', '2026-10-10'],
      ['school', '2026-10-04', '2026-10-10'],
    ]);
    expect(pickups.listSchedule).toHaveBeenCalledTimes(1);
    expect(pickups.listSchedule).toHaveBeenCalledWith('signe', '2026-10-04', '2026-10-10');
    expect(workLocations.listWorkDays).toHaveBeenCalledTimes(1);
    expect(mealplans.listMealPlan.mock.calls).toEqual([
      [{ kind: 'family', childId: 'signe' }, '2026-10-04', '2026-10-10'],
      [expect.objectContaining({ kind: 'group', groupId: 'fam' }), '2026-10-04', '2026-10-10'],
    ]);
  });

  it('turns a failed source into null and keeps the others', async () => {
    const { loader } = setup();

    const sources = await loader.load(
      template([row(PRINT_ROW_KIND.calendarEvents, { calendarIds: ['private', 'family'] })]),
      '2026-10-04',
    );

    expect(sources.occurrences.get('private')).toBeNull();
    expect(sources.occurrences.get('family')).toEqual([]);
  });

  it('collects given names for children and their guardians', async () => {
    const { loader } = setup();

    const sources = await loader.load(template([]), '2026-10-04');

    expect([...sources.names]).toEqual([
      ['signe', 'Signe'],
      ['dad', 'Far'],
      ['mum', 'Mor'],
    ]);
  });

  it('still loads the sheet when names fail', async () => {
    const { loader, guardians } = setup();
    guardians.listMyChildren.mockRejectedValue(new Error('500'));

    const sources = await loader.load(template([]), '2026-10-04');

    expect(sources.names.size).toBe(0);
  });

  it("prints a disabled feature's rows from an empty source instead of fetching them", async () => {
    disableFeatures('mealplans', 'pickups', 'workLocations');
    const { loader, mealplans, pickups, workLocations } = setup();

    const sources = await loader.load(
      template([
        row(PRINT_ROW_KIND.meal, { childId: 'signe', mealSlot: 'Dinner' }),
        row(PRINT_ROW_KIND.pickup, { childId: 'signe' }),
        row(PRINT_ROW_KIND.workLocation, { guardianId: 'dad' }),
      ]),
      '2026-10-04',
    );

    expect(mealplans.listMealPlan).not.toHaveBeenCalled();
    expect(pickups.listSchedule).not.toHaveBeenCalled();
    expect(workLocations.listWorkDays).not.toHaveBeenCalled();
    // Empty, not null: the rows print blank rather than marked unavailable.
    expect([...sources.meals.values()]).toEqual([[]]);
    expect(sources.pickups.get('signe')).toEqual([]);
    expect(sources.workDays.get('dad')).toEqual([]);
  });
});
