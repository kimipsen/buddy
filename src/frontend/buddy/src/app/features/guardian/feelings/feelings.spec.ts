import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { FeelingCheckIn, FeelingsService } from '../../../core/feelings.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { MedicinesService } from '../../../core/medicines.service';
import { SleepDiaryService } from '../../../core/sleep-diary.service';
import { disableFeatures } from '../../../../testing/features-fixture';
import { GuardianFeelings } from './feelings';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const TODAY = '2026-10-13';

function child(id: string, givenName: string): ChildSummary {
  return { id, name: { givenName, familyName: 'Hansen' } } as ChildSummary;
}

function checkIn(overrides: Partial<FeelingCheckIn>): FeelingCheckIn {
  return {
    id: 'c1',
    zone: 'Green',
    tags: [],
    note: '',
    date: TODAY,
    time: '08:00',
    loggedBy: 'emil',
    isSelfReport: true,
    recordedAt: `${TODAY}T06:00:00Z`,
    ...overrides,
  };
}

describe('GuardianFeelings', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(children: ChildSummary[] = [child('emil', 'Emil')]) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(`${TODAY}T12:00:00`));

    const feelings = {
      listCheckIns: vi.fn(async () => ({
        from: '2026-08-19',
        to: TODAY,
        promptTimes: ['07:30'],
        displayMode: 'FacesAndWords' as const,
        checkIns: [
          checkIn({ id: 'old', date: '2026-09-01', zone: 'Red' }),
          checkIn({ id: 'today', zone: 'Yellow', tags: ['Worried'] }),
        ],
      })),
      listShareLinks: vi.fn(async () => []),
      removeCheckIn: vi.fn(async () => undefined),
    } satisfies Partial<FeelingsService>;
    const sleepDiary = {
      listEntries: vi.fn(async () => ({
        sleepHygieneNotes: '',
        entries: [{ date: '2026-10-12', totalSleepMinutes: 600 }],
      })),
    } as unknown as SleepDiaryService;
    const medicines = {
      listDoses: vi.fn(async () => [
        { date: TODAY, time: '07:45:00', status: 'Taken' },
        { date: TODAY, time: '19:00:00', status: 'Pending' },
      ]),
    } as unknown as MedicinesService;

    await TestBed.configureTestingModule({
      imports: [GuardianFeelings],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: { listMyChildren: vi.fn(async () => children) } },
        { provide: FeelingsService, useValue: feelings },
        { provide: SleepDiaryService, useValue: sleepDiary },
        { provide: MedicinesService, useValue: medicines },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianFeelings);
    await settle(fixture);
    return { fixture, feelings, sleepDiary, medicines, el: fixture.nativeElement as HTMLElement };
  }

  it('loads eight weeks of check-ins, and sleep and doses for the 14 days drawn', async () => {
    const { feelings, sleepDiary, medicines, el } = await setup();

    expect(feelings.listCheckIns).toHaveBeenCalledWith('emil', '2026-08-19', TODAY);
    expect(sleepDiary.listEntries).toHaveBeenCalledWith('emil', '2026-09-29', TODAY);
    expect(medicines.listDoses).toHaveBeenCalledWith('emil', '2026-09-30', TODAY);

    const todayRow = el.querySelector(`tr[data-date="${TODAY}"]`) as HTMLElement;
    expect(todayRow.textContent).toContain('10 h 0 min');
    expect(todayRow.textContent).toContain('Medicine taken at 07:45');
    expect(todayRow.textContent).not.toContain('19:00');
    expect(todayRow.textContent).toContain('Wound up (yellow): Worried');
    // The old check-in is in the 8-week summary only.
    expect(el.querySelectorAll('tr[data-date]')).toHaveLength(14);
    expect(el.textContent).not.toContain('Too much (red)');
  });

  it('shows today’s check-ins in the day detail and the settings with the child’s name', async () => {
    const { el } = await setup();

    expect(el.querySelector('[data-testid="feelings-day-detail"]')?.textContent).toContain(
      'Wound up',
    );
    expect(el.textContent).toContain('Hide the words on Emil’s buttons');
    expect(el.textContent).toContain('Share with a doctor');
  });

  it('switches to the 8-week summary', async () => {
    const { fixture, el } = await setup();

    (
      Array.from(el.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === '8 weeks',
      ) as HTMLButtonElement
    ).click();
    await settle(fixture);

    expect(el.querySelector('[data-testid="feelings-weeks"]')).toBeTruthy();
    expect(el.textContent).toContain('Too much (red): 1');
  });

  it('pages back 14 days and not past today', async () => {
    const { fixture, feelings, el } = await setup();
    const buttonText = (text: string) =>
      Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);

    expect(buttonText('Next 14 days')).toBeUndefined();
    buttonText('Previous 14 days')!.click();
    await settle(fixture);

    expect(feelings.listCheckIns).toHaveBeenLastCalledWith('emil', '2026-08-05', '2026-09-29');
    expect(buttonText('Next 14 days')).toBeTruthy();
  });

  it('leaves sleep and doses out when those features are off', async () => {
    disableFeatures('sleepDiary', 'medicines');
    const { sleepDiary, medicines, el } = await setup();

    expect(sleepDiary.listEntries).not.toHaveBeenCalled();
    expect(medicines.listDoses).not.toHaveBeenCalled();
    expect(el.textContent).not.toContain('Slept the night before');
    expect(el.textContent).not.toContain('medicine taken');
  });

  it('says to link a child first when there is none', async () => {
    const { feelings, el } = await setup([]);

    expect(feelings.listCheckIns).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Link a child from Settings');
  });

  it('drops a removed check-in from the page', async () => {
    const { fixture, el } = await setup();

    (
      Array.from(el.querySelectorAll('[data-testid="feelings-day-detail"] button')).find(
        (b) => b.textContent?.trim() === 'Remove',
      ) as HTMLButtonElement
    ).click();
    await settle(fixture);

    expect(el.querySelector(`tr[data-date="${TODAY}"]`)?.textContent).toContain('No check-ins');
  });
});
