import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
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
    kind: 0,
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

describe('GuardianSleepDiary', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(
    options: { children?: ChildSummary[]; sleepDiary?: Partial<SleepDiaryService> } = {},
  ) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 2, 15, 9, 0));

    const guardians: Partial<GuardiansService> = {
      listMyChildren: vi.fn(
        async () => options.children ?? [child('c1', 'Alex'), child('c2', 'Sam')],
      ),
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
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianSleepDiary);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, sleepDiary };
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
});
