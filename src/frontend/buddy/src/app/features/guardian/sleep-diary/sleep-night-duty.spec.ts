import { TestBed } from '@angular/core/testing';

import { FeaturesService } from '../../../core/features.service';
import { GuardiansService } from '../../../core/guardians.service';
import { Night, NightDutyService } from '../../../core/night-duty.service';
import { NIGHTS_PER_READ, nightDutyWindows, SleepNightDuty } from './sleep-night-duty';

function plannedNight(date: string, guardianId: string, note = ''): Night {
  return {
    date,
    status: {
      kind: 2,
      entry: { responsible: { kind: 0, guardianId }, note },
      source: 'Pattern',
      scheduled: null,
    },
  } as Night;
}

function span(from: string, to: string): number {
  return Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000) + 1;
}

describe('nightDutyWindows', () => {
  it('uses one window for a period of up to 31 nights', () => {
    expect(nightDutyWindows('2026-03-01', '2026-03-31')).toEqual([
      { from: '2026-03-01', to: '2026-03-31' },
    ]);
    expect(nightDutyWindows('2026-03-15', '2026-03-15')).toEqual([
      { from: '2026-03-15', to: '2026-03-15' },
    ]);
  });

  it('splits 62 nights into two full windows', () => {
    expect(nightDutyWindows('2026-01-01', '2026-03-03')).toEqual([
      { from: '2026-01-01', to: '2026-01-31' },
      { from: '2026-02-01', to: '2026-03-03' },
    ]);
  });

  it('splits 92 nights into three windows with no gap or overlap', () => {
    const windows = nightDutyWindows('2026-01-01', '2026-04-02');

    expect(windows).toHaveLength(3);
    expect(windows[0].from).toBe('2026-01-01');
    expect(windows[2].to).toBe('2026-04-02');
    expect(windows.map((w) => span(w.from, w.to))).toEqual([31, 31, 30]);
    expect(windows.every((w) => span(w.from, w.to) <= NIGHTS_PER_READ)).toBe(true);
  });

  it('steps by calendar days across the spring and autumn DST changes', () => {
    // 2026-03-29 and 2026-10-25 are the European DST changes.
    const spring = nightDutyWindows('2026-03-20', '2026-05-20');
    expect(spring).toEqual([
      { from: '2026-03-20', to: '2026-04-19' },
      { from: '2026-04-20', to: '2026-05-20' },
    ]);
    const autumn = nightDutyWindows('2026-10-10', '2026-12-10');
    expect(autumn).toEqual([
      { from: '2026-10-10', to: '2026-11-09' },
      { from: '2026-11-10', to: '2026-12-10' },
    ]);
  });
});

describe('SleepNightDuty', () => {
  function setup(
    options: {
      enabled?: boolean;
      listNights?: NightDutyService['listNights'];
      listChildGuardians?: GuardiansService['listChildGuardians'];
    } = {},
  ) {
    const nightDuty: Partial<NightDutyService> = {
      listNights: vi.fn(options.listNights ?? (async () => [])),
    };
    const guardians: Partial<GuardiansService> = {
      listChildGuardians: vi.fn(
        options.listChildGuardians ??
          (async () =>
            [{ id: 'g-mom', name: { givenName: 'Mona', familyName: 'Anderson' } }] as Awaited<
              ReturnType<GuardiansService['listChildGuardians']>
            >),
      ),
    };
    const features: Partial<FeaturesService> = { enabled: vi.fn(() => options.enabled ?? true) };

    TestBed.configureTestingModule({
      providers: [
        { provide: NightDutyService, useValue: nightDuty },
        { provide: GuardiansService, useValue: guardians },
        { provide: FeaturesService, useValue: features },
      ],
    });

    return { service: TestBed.inject(SleepNightDuty), nightDuty, guardians };
  }

  it('returns null without any request when the feature is off', async () => {
    const { service, nightDuty, guardians } = setup({ enabled: false });

    expect(await service.load('c1', '2026-03-01', '2026-03-14')).toBeNull();
    expect(nightDuty.listNights).not.toHaveBeenCalled();
    expect(guardians.listChildGuardians).not.toHaveBeenCalled();
  });

  it('reads every window and merges the nights in order', async () => {
    const { service, nightDuty } = setup({
      listNights: async (_c, from) => [plannedNight(from, 'g-mom')],
    });

    const data = await service.load('c1', '2026-01-01', '2026-04-02');

    expect(nightDuty.listNights).toHaveBeenCalledTimes(3);
    expect(nightDuty.listNights).toHaveBeenCalledWith('c1', '2026-01-01', '2026-01-31');
    expect(nightDuty.listNights).toHaveBeenCalledWith('c1', '2026-02-01', '2026-03-03');
    expect(nightDuty.listNights).toHaveBeenCalledWith('c1', '2026-03-04', '2026-04-02');
    expect(data?.nights.map((n) => n.date)).toEqual(['2026-01-01', '2026-02-01', '2026-03-04']);
    expect(data?.guardians).toHaveLength(1);
  });

  it('returns null when any window fails', async () => {
    const { service } = setup({
      listNights: async (_c, from) => {
        if (from === '2026-02-01') {
          throw new Error('boom');
        }

        return [];
      },
    });

    expect(await service.load('c1', '2026-01-01', '2026-03-03')).toBeNull();
  });

  it('falls back to no guardians when the guardian read fails', async () => {
    const { service } = setup({
      listNights: async () => [plannedNight('2026-03-14', 'g-mom')],
      listChildGuardians: async () => {
        throw new Error('nope');
      },
    });

    const data = await service.load('c1', '2026-03-01', '2026-03-14');

    expect(data).toEqual({ nights: [plannedNight('2026-03-14', 'g-mom')], guardians: [] });
    expect(service.cells(data!)['2026-03-14']?.who?.text).toBeTruthy();
  });

  it('maps planned nights to cells and skips the others', async () => {
    const { service } = setup();
    const cells = service.cells({
      guardians: [{ id: 'g-mom', name: { givenName: 'Mona', familyName: 'Anderson' } }] as Awaited<
        ReturnType<GuardiansService['listChildGuardians']>
      >,
      nights: [
        plannedNight('2026-03-15', 'g-mom', 'Screen night'),
        { date: '2026-03-14', status: { kind: 0 } } as unknown as Night,
      ],
    });

    expect(Object.keys(cells)).toEqual(['2026-03-15']);
    expect(cells['2026-03-15']?.note).toBe('Screen night');
    expect(cells['2026-03-15']?.who?.text).toContain('Mona');
  });
});
