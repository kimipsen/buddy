import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { FeaturesService } from '../../../core/features.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { Night, NightDutyService } from '../../../core/night-duty.service';
import { SleepDiaryService, SleepEntry } from '../../../core/sleep-diary.service';
import { GuardianSleepDiary } from './sleep-diary';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return {
    id,
    name: { givenName, familyName: 'Anderson' },
    guardianLinkId: `link-${id}`,
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'Europe/Copenhagen',
  } as ChildSummary;
}

function entry(date: string, bedTime: string): SleepEntry {
  return {
    date,
    isWeekend: false,
    loggedBy: 'g',
    routineStartTime: null,
    ritualStartTime: null,
    ritualEndTime: null,
    bedTime,
    fellAsleepTime: null,
    nightWakeUps: [],
    morningWakeTime: null,
    isTired: false,
    naps: [],
    totalSleepMinutes: null,
    remarks: '',
  };
}

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

describe('GuardianSleepDiary', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(
    options: {
      children?: ChildSummary[];
      sleepDiary?: Partial<SleepDiaryService>;
      nightDuty?: Partial<NightDutyService>;
      nightDutyEnabled?: boolean;
      listChildGuardians?: GuardiansService['listChildGuardians'];
    } = {},
  ) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 2, 15, 9, 0));

    const guardians: Partial<GuardiansService> = {
      listMyChildren: vi.fn(
        async () => options.children ?? [child('c1', 'Alex'), child('c2', 'Sam')],
      ),
      listChildGuardians: vi.fn(
        options.listChildGuardians ??
          (async () =>
            [{ id: 'g-mom', name: { givenName: 'Mona', familyName: 'Anderson' } }] as Awaited<
              ReturnType<GuardiansService['listChildGuardians']>
            >),
      ),
    };
    const nightDuty: Partial<NightDutyService> = {
      listNights: vi.fn(async () => []),
      ...options.nightDuty,
    };
    const features: Partial<FeaturesService> = {
      enabled: vi.fn(() => options.nightDutyEnabled ?? true),
    };
    const sleepDiary: Partial<SleepDiaryService> = {
      listEntries: vi.fn(async () => ({
        sleepHygieneNotes: 'Curtains',
        entries: [entry('2026-03-14', '20:30'), entry('2026-03-15', '21:00')],
      })),
      listShareLinks: vi.fn(async () => []),
      logEntry: vi.fn(async (_c: string, date: string) => entry(date, '19:45')),
      ...options.sleepDiary,
    };

    await TestBed.configureTestingModule({
      imports: [GuardianSleepDiary],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardians },
        { provide: SleepDiaryService, useValue: sleepDiary },
        { provide: NightDutyService, useValue: nightDuty },
        { provide: FeaturesService, useValue: features },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianSleepDiary);
    await settle(fixture);

    return {
      fixture,
      compiled: fixture.nativeElement as HTMLElement,
      sleepDiary,
      nightDuty,
      guardians,
    };
  }

  function bedTimeInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('app-sleep-entry-form [aria-label="Lies down to sleep"]')!;
  }

  it('loads the first child’s last 14 nights and opens tonight in the form', async () => {
    const { compiled, sleepDiary } = await setup();

    expect(sleepDiary.listEntries).toHaveBeenCalledWith('c1', '2026-03-02', '2026-03-15');
    expect(compiled.querySelectorAll('app-sleep-history tbody tr')).toHaveLength(14);
    expect(bedTimeInput(compiled).value).toBe('21:00');
    expect(compiled.querySelector<HTMLTextAreaElement>('#sleepHygieneNotes')!.value).toBe(
      'Curtains',
    );
  });

  it('opens a night picked in the history', async () => {
    const { fixture, compiled } = await setup();

    // Newest first: the second row is 14 March.
    const rows = compiled.querySelectorAll('app-sleep-history tbody tr');
    (rows[1].querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(bedTimeInput(compiled).value).toBe('20:30');
  });

  it('opens a night picked in the phone night list', async () => {
    const { fixture, compiled } = await setup();

    const nights = compiled.querySelectorAll('app-sleep-history ul > li');
    expect(nights).toHaveLength(14);
    (nights[1].querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(bedTimeInput(compiled).value).toBe('20:30');
    expect(nights[1].getAttribute('aria-current')).toBe('date');
  });

  it('pages back 14 nights, and only forward up to today', async () => {
    const { fixture, compiled, sleepDiary } = await setup();
    const button = (text: string) =>
      Array.from(compiled.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);

    expect(button('Next 14 nights')).toBeUndefined();
    button('Previous 14 nights')!.click();
    await settle(fixture);

    expect(sleepDiary.listEntries).toHaveBeenLastCalledWith('c1', '2026-02-16', '2026-03-01');
    expect(button('Next 14 nights')).toBeDefined();
  });

  it('switches child', async () => {
    const { fixture, compiled, sleepDiary } = await setup();

    const select = compiled.querySelector<HTMLSelectElement>('#sleepDiaryChildId')!;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(sleepDiary.listEntries).toHaveBeenLastCalledWith('c2', '2026-03-02', '2026-03-15');
    expect(sleepDiary.listShareLinks).toHaveBeenLastCalledWith('c2');
  });

  it('shows a saved night in the history without reloading', async () => {
    const { fixture, compiled, sleepDiary } = await setup();

    const rows = compiled.querySelectorAll('app-sleep-history tbody tr');
    (rows[2].querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Save night')!
      .click();
    await settle(fixture);

    expect(sleepDiary.logEntry).toHaveBeenCalledWith('c1', '2026-03-13', expect.anything());
    expect(sleepDiary.listEntries).toHaveBeenCalledTimes(1);
    expect(compiled.querySelectorAll('app-sleep-history tbody tr')[2].textContent).toContain(
      '7:45',
    );
  });

  it('keeps the form mounted but read-only while another window loads', async () => {
    let resolveSecond: (value: { sleepHygieneNotes: string; entries: SleepEntry[] }) => void = () =>
      undefined;
    const listEntries = vi
      .fn()
      .mockResolvedValueOnce({ sleepHygieneNotes: '', entries: [] })
      .mockImplementationOnce(() => new Promise((resolve) => (resolveSecond = resolve)));
    const { fixture, compiled } = await setup({ sleepDiary: { listEntries } });
    const save = () =>
      Array.from(compiled.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Save night',
      ) as HTMLButtonElement;

    expect(save().disabled).toBe(false);
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Previous 14 nights')!
      .click();
    await settle(fixture);

    expect(save().disabled).toBe(true);

    resolveSecond({ sleepHygieneNotes: '', entries: [] });
    await settle(fixture);
    expect(save().disabled).toBe(false);
  });

  it('asks the guardian to link a child first', async () => {
    const { compiled } = await setup({ children: [] });

    expect(compiled.textContent).toContain(
      'Link a child from Settings before keeping a sleep diary.',
    );
  });

  it('shows an error when the diary fails to load', async () => {
    const { compiled } = await setup({
      sleepDiary: { listEntries: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    expect(compiled.textContent).toContain('Unable to load the sleep diary.');
    // A blank form here would let one Save overwrite a night the guardian can't see.
    expect(compiled.querySelector('app-sleep-entry-form')).toBeNull();
    expect(compiled.querySelector('app-hygiene-notes')).toBeNull();
  });

  describe('night duty', () => {
    function headers(compiled: HTMLElement): string[] {
      return Array.from(compiled.querySelectorAll('app-sleep-history thead th')).map(
        (th) => th.textContent?.trim() ?? '',
      );
    }

    it('shows who was on duty and the note next to each logged night', async () => {
      const { compiled, nightDuty, guardians } = await setup({
        nightDuty: {
          listNights: vi.fn(async () => [
            plannedNight('2026-03-15', 'g-mom', 'Screen night'),
            plannedNight('2026-03-14', 'g-gone'),
            // Not logged in the diary: nothing extra on that row.
            plannedNight('2026-03-13', 'g-mom', 'Unlogged note'),
          ]),
        },
      });

      expect(nightDuty.listNights).toHaveBeenCalledWith('c1', '2026-03-02', '2026-03-15');
      expect(guardians.listChildGuardians).toHaveBeenCalledWith('c1');
      expect(headers(compiled)).toContain('On duty');
      const rows = compiled.querySelectorAll('app-sleep-history tbody tr');
      const cell = (row: number) =>
        rows[row]
          .querySelector('[data-testid="night-duty-cell"]')
          ?.textContent?.replace(/\s+/g, ' ')
          .trim();
      expect(cell(0)).toBe('👤 Mona Screen night');
      // A guardian whose name can't be resolved any more.
      expect(cell(1)).toBe('👤 A guardian');
      expect(rows[2].textContent).not.toContain('Unlogged note');
      expect(rows[2].querySelector('td')?.getAttribute('colspan')).toBe('8');
    });

    it('names an unnamed babysitter and a sleepover host, and a night without a note', async () => {
      const night = (date: string, responsible: object): Night =>
        ({
          date,
          status: { kind: 2, entry: { responsible }, source: 'Override', scheduled: null },
        }) as Night;
      const { compiled } = await setup({
        nightDuty: {
          listNights: vi.fn(async () => [
            night('2026-03-15', { kind: 1, guardianId: 'g-mom', babysitterId: 'b1' }),
            night('2026-03-14', { kind: 2, hostName: 'Grandma', location: '', contactInfo: '' }),
          ]),
        },
      });

      const cells = compiled.querySelectorAll('[data-testid="night-duty-cell"]');
      expect(cells[0].textContent?.replace(/\s+/g, ' ').trim()).toBe('🧑‍🍼 Babysitter');
      expect(cells[1].textContent?.replace(/\s+/g, ' ').trim()).toBe('🛏️ Sleepover: Grandma');
    });

    it('leaves a logged night without a night entry blank', async () => {
      const { compiled } = await setup({
        nightDuty: { listNights: vi.fn(async () => [plannedNight('2026-03-15', 'g-mom')]) },
      });

      const rows = compiled.querySelectorAll('app-sleep-history tbody tr');
      expect(rows[1].querySelector('[data-testid="night-duty-cell"]')?.textContent?.trim()).toBe(
        '',
      );
    });

    it('still names nobody when the guardian names fail to load', async () => {
      const { compiled } = await setup({
        listChildGuardians: async () => {
          throw new Error('boom');
        },
        nightDuty: { listNights: vi.fn(async () => [plannedNight('2026-03-15', 'g-mom')]) },
      });

      expect(
        compiled.querySelector('[data-testid="night-duty-cell"]')?.textContent?.trim(),
      ).toContain('A guardian');
    });

    it('hides the column, and keeps the diary, when night duty can’t be read', async () => {
      const { compiled } = await setup({
        nightDuty: {
          listNights: vi.fn(async () => {
            throw new Error('404');
          }),
        },
      });

      expect(headers(compiled)).not.toContain('On duty');
      expect(compiled.querySelector('[data-testid="night-duty-cell"]')).toBeNull();
      expect(compiled.textContent).not.toContain('Unable to load the sleep diary.');
      expect(compiled.querySelectorAll('app-sleep-history tbody tr')).toHaveLength(14);
    });

    it('doesn’t ask for night duty when the feature is off', async () => {
      const { compiled, nightDuty } = await setup({ nightDutyEnabled: false });

      expect(nightDuty.listNights).not.toHaveBeenCalled();
      expect(headers(compiled)).not.toContain('On duty');
    });
  });
});
