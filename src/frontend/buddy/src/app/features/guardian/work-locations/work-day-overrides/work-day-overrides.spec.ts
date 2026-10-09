import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  WorkDay,
  WorkLocation,
  WorkLocationSchedule,
  WorkLocationsService,
} from '../../../../core/work-locations.service';
import { WorkDayOverrides } from './work-day-overrides';

describe('WorkDayOverrides', () => {
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

  const schedule: WorkLocationSchedule = {
    guardianId: 'me',
    locations: [stil, randers],
    pattern: { cycleWeeks: 1, anchorMonday: '2026-09-28', days: [] },
  };

  function days(from: string, overrides: Record<number, Partial<WorkDay>> = {}): WorkDay[] {
    const start = new Date(`${from}T12:00:00`);
    return Array.from({ length: 28 }, (_, i) => {
      const d = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i);
      const date = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
      return { date, status: { kind: 0 }, ...overrides[i] } as WorkDay;
    });
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 4; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function picker(root: HTMLElement, label: string): HTMLSelectElement {
    const select = root.querySelector<HTMLSelectElement>(`select[aria-label="${label}"]`);
    expect(select, `picker "${label}"`).toBeTruthy();
    return select!;
  }

  function choose(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function button(root: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text)!;
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 1, 12));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(stub: Partial<WorkLocationsService> = {}) {
    const service: Partial<WorkLocationsService> = {
      listWorkDays: vi.fn(async (_id: string, from: string) =>
        days(from, {
          1: { status: { kind: 2, location: stil, source: 'Pattern' } },
          2: { status: { kind: 1 } },
          3: { status: { kind: 2, location: randers, source: 'Override' } },
        }),
      ),
      setOverrides: vi.fn(async () => []),
      clearOverrides: vi.fn(async () => undefined),
      ...stub,
    };

    await TestBed.configureTestingModule({
      imports: [WorkDayOverrides],
      providers: [{ provide: WorkLocationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(WorkDayOverrides);
    fixture.componentRef.setInput('schedule', schedule);
    await settle(fixture);

    return { fixture, service, compiled: fixture.nativeElement as HTMLElement };
  }

  it('loads four weeks from this week’s Monday', async () => {
    const { service } = await setup();

    expect(service.listWorkDays).toHaveBeenCalledWith('me', '2026-09-28', '2026-10-25');
  });

  it('shows where each day resolves and whether it is an exception', async () => {
    const { compiled } = await setup();

    expect(picker(compiled, 'Location on Mon, Sep 28').value).toBe('pattern');
    expect(picker(compiled, 'Location on Tue, Sep 29').value).toBe('pattern');
    expect(picker(compiled, 'Location on Wed, Sep 30').value).toBe('off');
    // An archived location stays selectable on a day that still uses it.
    expect(picker(compiled, 'Location on Thu, Oct 1').value).toBe('randers');
    expect(compiled.textContent).toContain('From pattern');
    expect(compiled.textContent).toContain('Exception');
  });

  it('sets a single-day exception and reloads', async () => {
    const { fixture, compiled, service } = await setup();

    choose(picker(compiled, 'Location on Mon, Sep 28'), 'stil');
    await settle(fixture);

    expect(service.setOverrides).toHaveBeenCalledWith('2026-09-28', '2026-09-28', 'stil');
    expect(service.listWorkDays).toHaveBeenCalledTimes(2);
  });

  it('marks a day off with a null location', async () => {
    const { fixture, compiled, service } = await setup();

    choose(picker(compiled, 'Location on Mon, Sep 28'), 'off');
    await settle(fixture);

    expect(service.setOverrides).toHaveBeenCalledWith('2026-09-28', '2026-09-28', null);
  });

  it('clears an exception when the day goes back to the pattern', async () => {
    const { fixture, compiled, service } = await setup();

    choose(picker(compiled, 'Location on Wed, Sep 30'), 'pattern');
    await settle(fixture);

    expect(service.clearOverrides).toHaveBeenCalledWith('2026-09-30', '2026-09-30');
  });

  it('applies a range as off by default', async () => {
    const { fixture, compiled, service } = await setup();

    button(compiled, 'Apply').click();
    await settle(fixture);

    expect(service.setOverrides).toHaveBeenCalledWith('2026-10-01', '2026-10-01', null);
  });

  it('pages four weeks later', async () => {
    const { fixture, compiled, service } = await setup();

    button(compiled, 'Later').click();
    await settle(fixture);

    expect(service.listWorkDays).toHaveBeenLastCalledWith('me', '2026-10-26', '2026-11-22');
  });

  it('ignores a slower, older load that resolves after a newer one', async () => {
    const pending: ((days: WorkDay[]) => void)[] = [];
    const { fixture, compiled, service } = await setup();
    service.listWorkDays = vi.fn(() => new Promise<WorkDay[]>((resolve) => pending.push(resolve)));

    button(compiled, 'Later').click();
    await settle(fixture);
    button(compiled, 'Later').click();
    await settle(fixture);
    expect(pending).toHaveLength(2);

    // Newest (+8 weeks) first, then the stale +4 weeks response.
    pending[1](days('2026-11-23'));
    await settle(fixture);
    pending[0](days('2026-10-26'));
    await settle(fixture);

    expect(compiled.querySelector('select[aria-label="Location on Mon, Nov 23"]')).toBeTruthy();
    expect(compiled.querySelector('select[aria-label="Location on Mon, Oct 26"]')).toBeNull();
  });

  it('keeps the loaded weeks on screen while the next weeks load', async () => {
    let resolve!: (days: WorkDay[]) => void;
    const { fixture, compiled, service } = await setup();
    service.listWorkDays = vi.fn(() => new Promise<WorkDay[]>((r) => (resolve = r)));

    button(compiled, 'Later').click();
    await settle(fixture);

    expect(compiled.querySelector('select[aria-label="Location on Mon, Sep 28"]')).toBeTruthy();

    resolve(days('2026-10-26'));
    await settle(fixture);

    expect(compiled.querySelector('select[aria-label="Location on Mon, Oct 26"]')).toBeTruthy();
    expect(compiled.querySelector('select[aria-label="Location on Mon, Sep 28"]')).toBeNull();
  });

  it('keeps Apply disabled for a range that ends before it starts', async () => {
    const { fixture, compiled } = await setup();
    const [from] = Array.from(compiled.querySelectorAll<HTMLElement>('app-date-select'));
    const input = from.querySelector<HTMLInputElement>('input')!;

    input.value = '2026-10-05';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(button(compiled, 'Apply').disabled).toBe(true);
  });

  it('shows the save error', async () => {
    const { fixture, compiled } = await setup({
      setOverrides: vi.fn(async () => {
        throw new Error('400');
      }),
    });

    choose(picker(compiled, 'Location on Mon, Sep 28'), 'off');
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save. A range can be at most a month.');
  });

  it('shows the load error', async () => {
    const { compiled } = await setup({
      listWorkDays: vi.fn(async () => {
        throw new Error('500');
      }),
    });

    expect(compiled.textContent).toContain('Unable to load your days.');
  });
});
