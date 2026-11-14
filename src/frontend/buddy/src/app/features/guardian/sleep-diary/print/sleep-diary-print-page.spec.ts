import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { FeaturesService } from '../../../../core/features.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { Night, NightDutyService } from '../../../../core/night-duty.service';
import { SleepDiaryService, SleepEntry } from '../../../../core/sleep-diary.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { SleepDiaryPrintPage } from './sleep-diary-print-page';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
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

function plannedNight(date: string, guardianId: string): Night {
  return {
    date,
    status: {
      kind: 2,
      entry: { responsible: { kind: 0, guardianId }, note: '' },
      source: 'Pattern',
      scheduled: null,
    },
  } as Night;
}

describe('SleepDiaryPrintPage', () => {
  afterEach(() => {
    vi.useRealTimers();
    document.head.querySelectorAll('style[data-sleep-diary-print]').forEach((s) => s.remove());
  });

  async function setup(
    options: {
      query?: Record<string, string>;
      childId?: string;
      children?: ChildSummary[];
      listEntries?: SleepDiaryService['listEntries'];
      listNights?: NightDutyService['listNights'];
      nightDutyEnabled?: boolean;
    } = {},
  ) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 2, 15, 9, 0));

    const listEntries = vi.fn(
      options.listEntries ??
        (async () => ({
          sleepHygieneNotes: 'Curtains',
          entries: [entry('2026-03-14', '20:30')],
        })),
    );
    const listNights = vi.fn(options.listNights ?? (async () => []));

    await TestBed.configureTestingModule({
      imports: [SleepDiaryPrintPage],
      providers: [
        provideRouter([]),
        {
          provide: GuardiansService,
          useValue: {
            listMyChildren: vi.fn(async () => options.children ?? [child('c1', 'Alex')]),
            listChildGuardians: vi.fn(async () => [
              { id: 'g-mom', name: { givenName: 'Mona', familyName: 'Anderson' } },
            ]),
          },
        },
        { provide: SleepDiaryService, useValue: { listEntries } },
        { provide: NightDutyService, useValue: { listNights } },
        {
          provide: FeaturesService,
          useValue: { enabled: vi.fn(() => options.nightDutyEnabled ?? true) },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ childId: options.childId ?? 'c1' }),
              queryParamMap: convertToParamMap(options.query ?? {}),
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(SleepDiaryPrintPage);
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    return { fixture, el, listEntries, listNights };
  }

  const printButton = (el: HTMLElement) =>
    [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Print');
  const rows = (el: HTMLElement) => el.querySelectorAll('tbody tr');
  const alert = (el: HTMLElement) => el.querySelector('[role="alert"]')?.textContent?.trim();

  it('prints the period from the URL: one read, one row per night, the child and the period', async () => {
    const { el, listEntries } = await setup({ query: { from: '2026-03-10', to: '2026-03-15' } });

    expect(listEntries).toHaveBeenCalledExactlyOnceWith('c1', '2026-03-10', '2026-03-15');
    expect(rows(el)).toHaveLength(6);
    expect(el.querySelector('h1')?.textContent).toContain('Alex Anderson');
    expect(el.textContent).toContain('March 10, 2026 – March 15, 2026');
    expect(el.textContent).toContain('Curtains');
    expect(printButton(el)).toBeTruthy();
  });

  it('defaults to the last 14 nights ending today', async () => {
    const { el, listEntries } = await setup();

    expect(listEntries).toHaveBeenCalledExactlyOnceWith('c1', '2026-03-02', '2026-03-15');
    expect(rows(el)).toHaveLength(14);
  });

  it('falls back to the default window for a query parameter that is not a real date', async () => {
    const { listEntries } = await setup({ query: { from: '2026-02-30', to: 'soon' } });

    expect(listEntries).toHaveBeenCalledExactlyOnceWith('c1', '2026-03-02', '2026-03-15');
  });

  it('reads night duty in three windows for 92 nights and fills the column on logged nights', async () => {
    const listNights = vi.fn<NightDutyService['listNights']>(async (_c, from) =>
      from === '2026-01-01' ? [plannedNight('2026-01-02', 'g-mom')] : [],
    );
    const { el } = await setup({
      query: { from: '2026-01-01', to: '2026-04-02' },
      listEntries: async () => ({
        sleepHygieneNotes: '',
        entries: [entry('2026-01-02', '20:00'), entry('2026-01-03', '20:00')],
      }),
      listNights,
    });

    expect(listNights.mock.calls.map(([, from, to]) => [from, to])).toEqual([
      ['2026-01-01', '2026-01-31'],
      ['2026-02-01', '2026-03-03'],
      ['2026-03-04', '2026-04-02'],
    ]);
    expect(rows(el)).toHaveLength(92);
    expect(el.querySelectorAll('thead th')).toHaveLength(13);
    const cells = [...el.querySelectorAll('[data-testid="night-duty-cell"]')];
    expect(cells).toHaveLength(92);
    expect(cells.filter((c) => c.textContent?.includes('Mona'))).toHaveLength(1);
  });

  it('leaves the night duty column out when the feature is off', async () => {
    const { el, listNights } = await setup({ nightDutyEnabled: false });

    expect(listNights).not.toHaveBeenCalled();
    expect(el.querySelectorAll('thead th')).toHaveLength(12);
  });

  it('leaves the column out, and still prints, when night duty fails to load', async () => {
    const { el } = await setup({
      listNights: async () => {
        throw new Error('boom');
      },
    });

    expect(el.querySelectorAll('thead th')).toHaveLength(12);
    expect(printButton(el)).toBeTruthy();
  });

  it('formats the period in the guardian’s language', async () => {
    const { fixture, el } = await setup({ query: { from: '2026-03-10', to: '2026-03-15' } });
    const translation = TestBed.inject(TranslationService);

    await translation.setLanguage('da');
    await settle(fixture);

    expect(el.textContent).toContain('10. marts 2026 – 15. marts 2026');
    await translation.setLanguage('en');
  });

  it.each([
    [
      'reversed',
      { from: '2026-03-16', to: '2026-03-15' },
      'The to date can’t be before the from date.',
    ],
    [
      'too long',
      { from: '2026-01-01', to: '2026-04-03' },
      'A printout can cover at most 92 nights. Choose a shorter period.',
    ],
  ])('shows one message and no sheet or Print button for a %s period', async (_k, query, text) => {
    const { el, listEntries, listNights } = await setup({ query });

    expect(alert(el)).toBe(text);
    expect(el.querySelector('table')).toBeNull();
    expect(printButton(el)).toBeUndefined();
    expect(listEntries).not.toHaveBeenCalled();
    expect(listNights).not.toHaveBeenCalled();
  });

  it('shows the missing-date message, no sheet and no Print button when a date is cleared', async () => {
    const { fixture, el } = await setup({ query: { from: '2026-03-10', to: '2026-03-15' } });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const [fromInput] = [...el.querySelectorAll<HTMLInputElement>('input[type="date"]')];

    fromInput.value = '';
    fromInput.dispatchEvent(new Event('input'));
    fromInput.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(alert(el)).toBe('Choose a from date and a to date.');
    expect(el.querySelector('table')).toBeNull();
    expect(printButton(el)).toBeUndefined();
  });

  it('keeps the period in the URL and reads again when a date changes', async () => {
    const { fixture, el, listEntries } = await setup({
      query: { from: '2026-03-10', to: '2026-03-15' },
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const [fromInput] = [...el.querySelectorAll<HTMLInputElement>('input[type="date"]')];

    fromInput.value = '2026-03-12';
    fromInput.dispatchEvent(new Event('input'));
    fromInput.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith([], {
      queryParams: { from: '2026-03-12' },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    expect(listEntries).toHaveBeenLastCalledWith('c1', '2026-03-12', '2026-03-15');
    expect(rows(el)).toHaveLength(4);
  });

  it('shows the load error, with no sheet, when the diary cannot be read', async () => {
    const { el } = await setup({
      listEntries: async () => {
        throw new Error('boom');
      },
    });

    expect(el.textContent).toContain('Unable to load the sleep diary.');
    expect(el.querySelector('table')).toBeNull();
    expect(printButton(el)).toBeUndefined();
  });

  it('shows the load error when the child is not one of the guardian’s', async () => {
    const { el } = await setup({ childId: 'someone-else' });

    expect(el.textContent).toContain('Unable to load the sleep diary.');
    expect(el.querySelector('table')).toBeNull();
  });

  it('shows a spinner and no sheet while loading', async () => {
    const { el } = await setup({ listEntries: () => new Promise(() => undefined) });

    expect(el.querySelector('app-loading-spinner')).not.toBeNull();
    expect(el.querySelector('table')).toBeNull();
    expect(printButton(el)).toBeUndefined();
  });

  it('adds the A4 landscape page style once the sheet is ready and removes it with the page', async () => {
    const { fixture } = await setup();

    const style = document.head.querySelector('style[data-sleep-diary-print]');
    expect(style?.textContent).toContain('size: A4 landscape');
    expect(style?.textContent).toContain('margin: 10mm');

    fixture.destroy();
    expect(document.head.querySelector('style[data-sleep-diary-print]')).toBeNull();
  });

  it('prints the window when Print is pressed', async () => {
    const { el } = await setup();
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    printButton(el)?.click();

    expect(print).toHaveBeenCalledOnce();
  });
});
