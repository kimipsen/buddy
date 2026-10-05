import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { SleepDiaryService, SleepEntry } from '../../../../core/sleep-diary.service';
import { SleepEntryForm } from './sleep-entry-form';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function entry(overrides: Partial<SleepEntry> = {}): SleepEntry {
  return {
    date: '2026-03-02',
    isWeekend: false,
    loggedBy: 'guardian-1',
    routineStartTime: '19:00',
    ritualStartTime: '19:30',
    ritualEndTime: '20:10',
    bedTime: '20:15',
    fellAsleepTime: '20:45',
    nightWakeUps: [{ startTime: '03:30', durationMinutes: 30 }],
    morningWakeTime: '06:30',
    isTired: true,
    naps: [],
    totalSleepMinutes: 555,
    remarks: 'Restless',
    ...overrides,
  };
}

describe('SleepEntryForm', () => {
  async function setup(
    options: { entry?: SleepEntry | null; service?: Partial<SleepDiaryService> } = {},
  ) {
    const service: Partial<SleepDiaryService> = {
      logEntry: vi.fn(async (_childId: string, date: string) => entry({ date })),
      clearEntry: vi.fn(async () => undefined),
      ...options.service,
    };

    await TestBed.configureTestingModule({
      imports: [SleepEntryForm],
      providers: [{ provide: SleepDiaryService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(SleepEntryForm);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('date', '2026-03-02');
    fixture.componentRef.setInput('entry', options.entry ?? null);

    const saved = vi.fn();
    const cleared = vi.fn();
    const dateChange = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    fixture.componentInstance.cleared.subscribe(cleared);
    fixture.componentInstance.dateChange.subscribe(dateChange);

    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;

    return { fixture, compiled, service, saved, cleared, dateChange };
  }

  function byLabel(compiled: HTMLElement, label: string): HTMLInputElement {
    const input = compiled.querySelector<HTMLInputElement>(`[aria-label="${label}"]`);
    if (!input) {
      throw new Error(`No input labelled ${label}`);
    }
    return input;
  }

  function type(input: HTMLInputElement | HTMLTextAreaElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  it('lays a blank night out in the order the night happens, without a clear button', async () => {
    const { compiled } = await setup();

    const labels = Array.from(compiled.querySelectorAll('p.font-medium')).map((p) =>
      p.textContent?.trim(),
    );
    expect(labels).toEqual([
      'Getting ready for bed',
      'Bedtime ritual',
      'Lies down to sleep',
      'Falls asleep',
      'Night wake-ups',
      'Daytime naps',
      'Wakes up in the morning',
      'Total time slept',
    ]);
    expect(button(compiled, 'Clear this night')).toBeUndefined();
    expect(compiled.querySelector('button[role="switch"]')?.getAttribute('aria-checked')).toBe(
      'false',
    );
  });

  it('suggests a total from the times and saves it with the whole night', async () => {
    const { fixture, compiled, service, saved } = await setup();

    type(byLabel(compiled, 'Lies down to sleep'), '20:15');
    type(byLabel(compiled, 'Falls asleep'), '20:45');
    type(byLabel(compiled, 'Wakes up in the morning'), '06:30');
    button(compiled, '+ Add wake-up')!.click();
    await settle(fixture);
    type(byLabel(compiled, 'Woke up at'), '03:30');
    type(byLabel(compiled, 'Minutes'), '30');
    (compiled.querySelector('button[role="switch"]') as HTMLButtonElement).click();
    type(compiled.querySelector('textarea')!, ' Restless ');
    await settle(fixture);

    expect(byLabel(compiled, 'Hours slept').value).toBe('9');
    expect(byLabel(compiled, 'Minutes slept').value).toBe('15');
    expect(compiled.textContent).toContain('Suggested from the times above.');

    button(compiled, 'Save night')!.click();
    await settle(fixture);

    expect(service.logEntry).toHaveBeenCalledWith('child-1', '2026-03-02', {
      routineStartTime: null,
      ritualStartTime: null,
      ritualEndTime: null,
      bedTime: '20:15',
      fellAsleepTime: '20:45',
      nightWakeUps: [{ startTime: '03:30', durationMinutes: 30 }],
      morningWakeTime: '06:30',
      isTired: true,
      naps: [],
      totalSleepMinutes: 555,
      remarks: 'Restless',
    });
    expect(saved).toHaveBeenCalledWith(expect.objectContaining({ date: '2026-03-02' }));
    expect(compiled.querySelector('[role="status"]')?.textContent?.trim()).toBe('Saved.');
  });

  it('keeps a typed total over the suggestion and can switch back to it', async () => {
    const { fixture, compiled, service } = await setup();

    type(byLabel(compiled, 'Falls asleep'), '21:00');
    type(byLabel(compiled, 'Wakes up in the morning'), '07:00');
    await settle(fixture);
    type(byLabel(compiled, 'Hours slept'), '8');
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Suggested from the times above.');
    const useSuggestion = button(compiled, 'Use suggestion (10 h 0 min)');
    expect(useSuggestion).toBeDefined();

    button(compiled, 'Save night')!.click();
    await settle(fixture);
    expect(vi.mocked(service.logEntry!).mock.calls[0][2].totalSleepMinutes).toBe(480);

    useSuggestion!.click();
    await settle(fixture);
    button(compiled, 'Save night')!.click();
    await settle(fixture);
    expect(vi.mocked(service.logEntry!).mock.calls[1][2].totalSleepMinutes).toBe(600);
  });

  it('fills the form from a logged entry and keeps its saved total', async () => {
    const { compiled } = await setup({ entry: entry() });

    expect(byLabel(compiled, 'Ritual starts').value).toBe('19:30');
    expect(byLabel(compiled, 'Ritual ends').value).toBe('20:10');
    expect(byLabel(compiled, 'Woke up at').value).toBe('03:30');
    expect(byLabel(compiled, 'Hours slept').value).toBe('9');
    expect(compiled.querySelector('textarea')!.value).toBe('Restless');
    expect(compiled.querySelector('button[role="switch"]')?.getAttribute('aria-checked')).toBe(
      'true',
    );
    expect(compiled.textContent).not.toContain('Suggested from the times above.');
  });

  it('keeps typed numbers inside what the API accepts', async () => {
    const { fixture, compiled, service } = await setup();

    button(compiled, '+ Add wake-up')!.click();
    await settle(fixture);
    type(byLabel(compiled, 'Woke up at'), '03:30');
    type(byLabel(compiled, 'Minutes'), '0');
    type(byLabel(compiled, 'Hours slept'), '30');
    await settle(fixture);
    button(compiled, 'Save night')!.click();
    await settle(fixture);

    const request = vi.mocked(service.logEntry!).mock.calls[0][2];
    expect(request.nightWakeUps).toEqual([{ startTime: '03:30', durationMinutes: 1 }]);
    expect(request.totalSleepMinutes).toBe(24 * 60);
  });

  it('removes a wake-up row', async () => {
    const { fixture, compiled } = await setup({ entry: entry() });

    button(compiled, 'Remove')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[aria-label="Woke up at"]')).toBeNull();
  });

  it('clears a logged night', async () => {
    const { fixture, compiled, service, cleared } = await setup({ entry: entry() });

    button(compiled, 'Clear this night')!.click();
    await settle(fixture);

    expect(service.clearEntry).toHaveBeenCalledWith('child-1', '2026-03-02');
    expect(cleared).toHaveBeenCalledWith('2026-03-02');
  });

  it('shows an error when saving fails', async () => {
    const { fixture, compiled, saved } = await setup({
      service: { logEntry: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    button(compiled, 'Save night')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save this night.');
    expect(saved).not.toHaveBeenCalled();
  });

  it('emits a picked date', async () => {
    const { compiled, dateChange } = await setup();

    type(byLabel(compiled, 'Night of'), '2026-02-20');

    expect(dateChange).toHaveBeenCalledWith('2026-02-20');
  });
});
