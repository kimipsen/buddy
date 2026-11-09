import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  NightDutyService,
  NightPattern,
  NightSchedule,
  ReplaceNightPatternRequest,
} from '../../../../core/night-duty.service';
import { NightPeople } from '../night-people';
import { NightPatternEditor } from './night-pattern-editor';

describe('NightPatternEditor', () => {
  // Thursday 2026-11-12, in the week that starts Monday 2026-11-09.
  const today = '2026-11-12';

  const people: NightPeople = {
    guardians: [
      {
        id: 'mom',
        name: { givenName: 'Sara', familyName: 'Holm' },
        callingName: 'Mom',
        kind: 'Parent',
      },
      {
        id: 'dad',
        name: { givenName: 'Jonas', familyName: 'Holm' },
        callingName: '',
        kind: 'Parent',
      },
    ] as NightPeople['guardians'],
    babysitters: [{ guardianId: 'mom', id: 'maja', name: 'Maja', contactInfo: '' }],
  };

  function version(overrides: Partial<NightPattern> = {}): NightPattern {
    return {
      effectiveFrom: '2026-08-03',
      cycleWeeks: 1,
      anchorMonday: '2026-08-03',
      days: [],
      status: 'Current',
      ...overrides,
    };
  }

  function schedule(patterns: NightPattern[] = []): NightSchedule {
    return { childId: 'emil', today, patterns };
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 3; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function byLabel<T extends HTMLElement>(root: HTMLElement, label: string): T {
    const element = root.querySelector<T>(`[aria-label="${label}"]`);
    expect(element, label).toBeTruthy();
    return element!;
  }

  function choose(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function radio(root: HTMLElement, label: string): HTMLButtonElement {
    const button = Array.from(root.querySelectorAll<HTMLButtonElement>('[role="radio"]')).find(
      (b) => b.textContent?.trim() === label,
    );
    expect(button, label).toBeTruthy();
    return button!;
  }

  function saveButton(root: HTMLElement): HTMLButtonElement {
    return Array.from(root.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Save schedule',
    )!;
  }

  async function setup(
    initial: NightSchedule,
    replacePattern = vi.fn<
      (childId: string, request: ReplaceNightPatternRequest) => Promise<NightSchedule>
    >(async () => schedule()),
  ) {
    await TestBed.configureTestingModule({
      imports: [NightPatternEditor],
      providers: [{ provide: NightDutyService, useValue: { replacePattern } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(NightPatternEditor);
    fixture.componentRef.setInput('childId', 'emil');
    fixture.componentRef.setInput('schedule', initial);
    fixture.componentRef.setInput('people', people);
    const saved = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    await settle(fixture);

    return { fixture, replacePattern, saved, compiled: fixture.nativeElement as HTMLElement };
  }

  it('starts from the current version and lists every version', async () => {
    const { compiled } = await setup(
      schedule([
        version({
          days: [
            {
              week: 0,
              day: 'Friday',
              entry: { responsible: { kind: 0, guardianId: 'dad' }, note: 'Screen night' },
            },
          ],
        }),
      ]),
    );

    expect(byLabel<HTMLSelectElement>(compiled, 'Week A, Friday: who').value).toBe('g:dad');
    expect(byLabel<HTMLInputElement>(compiled, 'Week A, Friday: note').value).toBe('Screen night');
    expect(byLabel<HTMLSelectElement>(compiled, 'Week A, Monday: who').value).toBe('');
    expect(compiled.textContent).toContain('From Aug 3, 2026: 1 week (current)');
    expect(compiled.querySelector<HTMLInputElement>('#night-pattern-effective-from')?.value).toBe(
      today,
    );
  });

  it('says when there is no schedule yet', async () => {
    const { compiled } = await setup(schedule());

    expect(compiled.textContent).toContain('No fixed schedule yet.');
  });

  it('prefers the upcoming version and keeps its date', async () => {
    const { compiled } = await setup(
      schedule([
        version(),
        version({ effectiveFrom: '2026-11-16', anchorMonday: '2026-11-16', status: 'Upcoming' }),
      ]),
    );

    expect(compiled.querySelector<HTMLInputElement>('#night-pattern-effective-from')?.value).toBe(
      '2026-11-16',
    );
    expect(compiled.textContent).toContain('(upcoming)');
  });

  it('saves a two-week schedule with notes, a babysitter and a sleepover', async () => {
    const { fixture, compiled, replacePattern, saved } = await setup(schedule());

    radio(compiled, '2 weeks').click();
    await settle(fixture);
    radio(compiled, 'Week B').click();
    await settle(fixture);
    choose(byLabel(compiled, 'Week A, Monday: who'), 'g:mom');
    choose(byLabel(compiled, 'Week A, Tuesday: who'), 'b:mom:maja');
    type(byLabel(compiled, 'Week A, Friday: note'), ' Screen night 📱 ');
    choose(byLabel(compiled, 'Week B, Saturday: who'), 's');
    await settle(fixture);
    type(byLabel(compiled, 'Week B, Saturday: host'), 'Grandma Lis');
    await settle(fixture);
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).toHaveBeenCalledWith('emil', {
      effectiveFrom: today,
      cycleWeeks: 2,
      // This week (from 2026-11-09) is week B, so week A started the Monday before.
      anchorMonday: '2026-11-02',
      days: [
        {
          week: 0,
          day: 'Monday',
          entry: { responsible: { kind: 0, guardianId: 'mom' }, note: '' },
        },
        {
          week: 0,
          day: 'Tuesday',
          entry: { responsible: { kind: 1, guardianId: 'mom', babysitterId: 'maja' }, note: '' },
        },
        { week: 0, day: 'Friday', entry: { responsible: { kind: 3 }, note: 'Screen night 📱' } },
        {
          week: 1,
          day: 'Saturday',
          entry: {
            responsible: { kind: 2, hostName: 'Grandma Lis', location: '', contactInfo: '' },
            note: '',
          },
        },
      ],
    });
    expect(saved).toHaveBeenCalledOnce();
    expect(compiled.textContent).toContain('Schedule saved.');
  });

  it('keeps the stored anchor when it already makes this week the chosen week', async () => {
    const { fixture, compiled, replacePattern } = await setup(
      schedule([version({ cycleWeeks: 2, anchorMonday: '2026-08-03' })]),
    );

    // 2026-08-03 + 14 weeks = 2026-11-09, so this week is week A.
    expect(radio(compiled, 'Week A').getAttribute('aria-checked')).toBe('true');
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).toHaveBeenCalledWith(
      'emil',
      expect.objectContaining({ cycleWeeks: 2, anchorMonday: '2026-08-03' }),
    );
  });

  it('never applies a schedule from before today', async () => {
    const { fixture, compiled, replacePattern } = await setup(schedule());
    const input = compiled.querySelector<HTMLInputElement>('#night-pattern-effective-from')!;

    type(input, '2026-11-01');
    await settle(fixture);
    saveButton(compiled).click();
    await settle(fixture);

    expect(input.min).toBe(today);
    expect(replacePattern).toHaveBeenCalledWith(
      'emil',
      expect.objectContaining({ effectiveFrom: today }),
    );
  });

  it('applies from a later date the guardian picks and says what it replaces', async () => {
    const { fixture, compiled, replacePattern } = await setup(schedule());

    type(compiled.querySelector<HTMLInputElement>('#night-pattern-effective-from')!, '2026-11-30');
    await settle(fixture);

    expect(compiled.textContent).toContain('Saved schedules that start on or after Nov 30, 2026');
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).toHaveBeenCalledWith(
      'emil',
      expect.objectContaining({ effectiveFrom: '2026-11-30' }),
    );
  });

  it('keeps a sleepover’s stored location and contact when saving an unrelated change', async () => {
    const sleepover = {
      responsible: {
        kind: 2 as const,
        hostName: 'Grandma Lis',
        location: 'Skovvej 12',
        contactInfo: '20 12 34 56',
      },
      note: '',
    };
    const { fixture, compiled, replacePattern } = await setup(
      schedule([version({ days: [{ week: 0, day: 'Saturday', entry: sleepover }] })]),
    );

    type(byLabel(compiled, 'Week A, Monday: note'), 'Movie');
    await settle(fixture);
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern.mock.calls[0]![1].days).toContainEqual({
      week: 0,
      day: 'Saturday',
      entry: sleepover,
    });
  });

  it('refuses a sleepover without a host', async () => {
    const { fixture, compiled, replacePattern } = await setup(schedule());

    choose(byLabel(compiled, 'Week A, Monday: who'), 's');
    await settle(fixture);
    saveButton(compiled).click();
    await settle(fixture);

    expect(replacePattern).not.toHaveBeenCalled();
    expect(compiled.textContent).toContain('Every sleepover needs a host.');
  });

  it('shows the save error', async () => {
    const { fixture, compiled, saved } = await setup(
      schedule(),
      vi.fn(async () => {
        throw new Error('400');
      }),
    );

    saveButton(compiled).click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save the schedule.');
    expect(saved).not.toHaveBeenCalled();
  });
});
