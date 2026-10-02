import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  WorkLocation,
  WorkLocationSchedule,
  WorkLocationsService,
  WorkPattern,
} from '../../../../core/work-locations.service';
import { WorkPatternEditor } from './work-pattern-editor';

describe('WorkPatternEditor', () => {
  const stil: WorkLocation = {
    id: 'stil',
    name: 'Stil',
    icon: '🏢',
    color: '#0ea5e9',
    isArchived: false,
  };

  function schedule(pattern: Partial<WorkPattern> = {}, locations = [stil]): WorkLocationSchedule {
    return {
      guardianId: 'me',
      locations,
      pattern: { cycleWeeks: 1, anchorMonday: '2026-09-28', days: [], ...pattern },
    };
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 3; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function radio(root: HTMLElement, label: string): HTMLButtonElement {
    const button = Array.from(root.querySelectorAll<HTMLButtonElement>('[role="radio"]')).find(
      (b) => b.textContent?.trim() === label,
    );
    expect(button, `radio "${label}"`).toBeTruthy();
    return button!;
  }

  function cell(root: HTMLElement, label: string): HTMLSelectElement {
    const select = root.querySelector<HTMLSelectElement>(`select[aria-label="${label}"]`);
    expect(select, `cell "${label}"`).toBeTruthy();
    return select!;
  }

  function choose(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function saveButton(root: HTMLElement): HTMLButtonElement {
    return Array.from(root.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Save pattern',
    )!;
  }

  beforeEach(() => {
    // Thursday of the week starting Monday 2026-09-28.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 1, 12));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(
    initial: WorkLocationSchedule,
    replacePattern = vi.fn(async (p: WorkPattern) => p),
  ) {
    await TestBed.configureTestingModule({
      imports: [WorkPatternEditor],
      providers: [{ provide: WorkLocationsService, useValue: { replacePattern } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(WorkPatternEditor);
    fixture.componentRef.setInput('schedule', initial);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return { fixture, replacePattern, changed, compiled: fixture.nativeElement as HTMLElement };
  }

  it('asks for a location first when there are none', async () => {
    const { compiled } = await setup(schedule({}, []));

    expect(compiled.textContent).toContain('Add a location first to build a pattern.');
    expect(compiled.querySelector('select')).toBeNull();
  });

  it('shows the stored pattern in the grid', async () => {
    const { compiled } = await setup(schedule({ days: [{ week: 0, day: 2, locationId: 'stil' }] }));

    expect(cell(compiled, 'Week A, Tue').value).toBe('stil');
    expect(cell(compiled, 'Week A, Mon').value).toBe('');
  });

  it('saves an alternating pattern anchored so this week is the chosen week', async () => {
    const { fixture, compiled, replacePattern, changed } = await setup(schedule());

    radio(compiled, '2 weeks').click();
    await settle(fixture);
    radio(compiled, 'Week B').click();
    await settle(fixture);
    choose(cell(compiled, 'Week A, Tue'), 'stil');
    choose(cell(compiled, 'Week B, Thu'), 'stil');
    await settle(fixture);
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).toHaveBeenCalledWith({
      cycleWeeks: 2,
      anchorMonday: '2026-09-21',
      days: [
        { week: 0, day: 2, locationId: 'stil' },
        { week: 1, day: 4, locationId: 'stil' },
      ],
    });
    expect(changed).toHaveBeenCalledOnce();
    expect(compiled.textContent).toContain('Pattern saved.');
  });

  it('keeps the stored anchor when it already matches', async () => {
    const { fixture, compiled, replacePattern } = await setup(
      schedule({ cycleWeeks: 2, anchorMonday: '2026-08-31' }),
    );

    // 2026-08-31 + 4 weeks = 2026-09-28, so this week is already week A.
    expect(radio(compiled, 'Week A').getAttribute('aria-checked')).toBe('true');
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).toHaveBeenCalledWith(
      expect.objectContaining({ cycleWeeks: 2, anchorMonday: '2026-08-31' }),
    );
  });

  it('drops weeks beyond a shortened cycle', async () => {
    const { fixture, compiled, replacePattern } = await setup(
      schedule({
        cycleWeeks: 2,
        days: [
          { week: 0, day: 1, locationId: 'stil' },
          { week: 1, day: 1, locationId: 'stil' },
        ],
      }),
    );

    radio(compiled, '1 week').click();
    await settle(fixture);
    expect(compiled.querySelector('select[aria-label="Week B, Mon"]')).toBeNull();
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).toHaveBeenCalledWith({
      cycleWeeks: 1,
      anchorMonday: '2026-09-28',
      days: [{ week: 0, day: 1, locationId: 'stil' }],
    });
  });

  it('shows the save error', async () => {
    const { fixture, compiled, changed } = await setup(
      schedule(),
      vi.fn(async () => {
        throw new Error('400');
      }),
    );

    saveButton(compiled).click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save the pattern.');
    expect(changed).not.toHaveBeenCalled();
  });
});
