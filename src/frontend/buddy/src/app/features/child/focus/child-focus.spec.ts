import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { nestOccurrence } from '../../../../testing/occurrence-fixture';
import {
  CalendarOccurrence,
  CalendarsService,
  TaskCompletion,
} from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { UsersService } from '../../../core/users.service';
import { ChildFocus } from './child-focus';

describe('ChildFocus', () => {
  const today = todayIsoDate();
  const NOW = Date.parse(`${today}T12:00:00Z`);

  function subtask(
    subtaskId: string,
    title: string,
    start: string,
    end: string,
    overrides: { itemId?: string; isCompleted?: boolean } = {},
  ): CalendarOccurrence {
    return nestOccurrence<CalendarOccurrence>({
      itemId: overrides.itemId ?? 'run-1',
      kind: 'Task',
      title,
      icon: '👕',
      iconOverride: null,
      color: '#22c55e',
      startsAt: `${today}T${start}:00Z`,
      endsAt: `${today}T${end}:00Z`,
      isAllDay: false,
      isCompleted: overrides.isCompleted ?? false,
      isSkipped: false,
      isRepeating: false,
      createdBy: 'g',
      lastModifiedBy: 'g',
      assignedTo: 'child-1',
      calendarId: 'cal-1',
      calendarName: 'Home',
      parentTitle: 'Morning routine',
      parentIcon: '🌅',
      subtaskId,
    });
  }

  function morning(): CalendarOccurrence[] {
    return [
      subtask('sub-1', 'Get dressed', '11:58', '12:03'),
      subtask('sub-2', 'Eat breakfast', '12:03', '12:13'),
      subtask('other', 'Not this run', '11:00', '13:00', { itemId: 'run-2' }),
    ];
  }

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  async function setup(calendars: Partial<CalendarsService>, itemId = 'run-1') {
    const calendarsStub: Partial<CalendarsService> = {
      setTaskCompletion: vi.fn(
        async () => ({ itemId, occurrenceDate: today, isCompleted: true }) as TaskCompletion,
      ),
      ...calendars,
    };

    await TestBed.configureTestingModule({
      imports: [ChildFocus],
      providers: [
        provideRouter([]),
        { provide: CalendarsService, useValue: calendarsStub },
        {
          provide: UsersService,
          useValue: {
            timeZoneId: signal('UTC').asReadonly(),
            profileTimeZoneId: signal<string | null>(null).asReadonly(),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ itemId }) } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildFocus);
    return { fixture, calendars: calendarsStub };
  }

  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  // For tests that also fake the timeouts and intervals, so they can step to an exact instant.
  function fakeAllTimers(at: number) {
    vi.useFakeTimers({
      toFake: ['Date', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'],
    });
    vi.setSystemTime(at);
  }

  async function settleFaked(fixture: { detectChanges: () => void }) {
    for (let round = 0; round < 3; round++) {
      fixture.detectChanges();
      await vi.advanceTimersByTimeAsync(0);
    }
    fixture.detectChanges();
  }

  function timerLabel(compiled: HTMLElement): string | null | undefined {
    return compiled.querySelector('[role="timer"]')?.getAttribute('aria-label');
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((element) =>
      element.textContent?.includes(text),
    );
  }

  function text(compiled: HTMLElement): string {
    return (compiled.textContent ?? '').replace(/\s+/g, ' ');
  }

  it('shows the current step with one big disc, the next step and the controls', async () => {
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => morning()) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('One step at a time');
    expect(compiled.querySelector('h2')?.textContent).toContain('Get dressed');
    expect(text(compiled)).toContain('Morning routine');
    expect(text(compiled)).toContain('Next: Eat breakfast');
    expect(text(compiled)).not.toContain('Not this run');
    const timers = compiled.querySelectorAll('[role="timer"]');
    expect(timers).toHaveLength(1);
    expect(timers[0]!.getAttribute('aria-label')).toBe('3 min left');
    expect(button(compiled, "I'm done")).toBeTruthy();
    expect(button(compiled, 'Start')).toBeTruthy();
    expect(compiled.querySelector('a[href="/child"]')?.textContent).toContain('Back to today');
  });

  it('takes the steps in time order, whatever order they arrive in', async () => {
    const { fixture } = await setup({
      listTodayOccurrences: vi.fn(async () => [...morning()].reverse()),
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Get dressed');
    expect(text(compiled)).toContain('Next: Eat breakfast');
  });

  it('leaves out an occurrence of the item that is not a routine step', async () => {
    const plain = nestOccurrence<CalendarOccurrence>({
      ...subtask('plain', 'Plain task', '11:00', '11:30'),
      timing: { kind: 1, dueAt: `${today}T11:00:00Z` },
      routine: null,
    });
    const { fixture } = await setup({
      listTodayOccurrences: vi.fn(async () => [plain, ...morning()]),
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Get dressed');
    expect(text(compiled)).not.toContain('Plain task');
  });

  it('moves on from the start time to the countdown once the step starts', async () => {
    fakeAllTimers(NOW);
    const { fixture } = await setup({
      listTodayOccurrences: vi.fn(async () => [subtask('sub-1', 'Get dressed', '12:01', '12:06')]),
    });
    await settleFaked(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(timerLabel(compiled)).toContain('Starts at');

    await vi.advanceTimersByTimeAsync(60_000);
    await settleFaked(fixture);
    expect(timerLabel(compiled)).toBe('5 min left');
  });

  it('stops the minute refresh on leaving', async () => {
    fakeAllTimers(NOW);
    const setInterval = vi.spyOn(globalThis, 'setInterval');
    const clearInterval = vi.spyOn(globalThis, 'clearInterval');
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => morning()) });
    await settleFaked(fixture);
    const minute = setInterval.mock.calls.findIndex(([, ms]) => ms === 60_000);
    const refreshId = setInterval.mock.results[minute]!.value as unknown;

    fixture.destroy();

    expect(clearInterval).toHaveBeenCalledWith(refreshId);
  });

  it("drops Pause the moment a child-started step's time runs out", async () => {
    fakeAllTimers(NOW);
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => morning()) });
    await settleFaked(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    // Off the minute, so the deadline doesn't coincide with the minute refresh.
    await vi.advanceTimersByTimeAsync(30_000);
    button(compiled, 'Start')!.click();
    await settleFaked(fixture);

    await vi.advanceTimersByTimeAsync(5 * 60_000 - 1);
    fixture.detectChanges();
    expect(button(compiled, 'Pause')).toBeTruthy();

    await vi.advanceTimersByTimeAsync(1);
    fixture.detectChanges();
    expect(button(compiled, 'Pause')).toBeUndefined();
    expect(button(compiled, 'Stop timer')).toBeTruthy();
  });

  it('reads the time afresh after a step is marked done', async () => {
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => morning()) });
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    // Done at 12:05, two minutes into the next step's 12:03-12:13 window.
    vi.setSystemTime(NOW + 5 * 60_000);

    button(compiled, "I'm done")!.click();
    await settle(fixture);

    expect(compiled.querySelector('h2')?.textContent).toContain('Eat breakfast');
    expect(timerLabel(compiled)).toBe('8 min left');
  });

  it('marks the current step done by its subtask id and moves on, chaining a child-started timer', async () => {
    const { fixture, calendars } = await setup({
      listTodayOccurrences: vi.fn(async () => morning()),
    });
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    button(compiled, 'Start')!.click();
    await settle(fixture);

    button(compiled, "I'm done")!.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith(
      'cal-1',
      'run-1',
      today,
      true,
      'sub-1',
    );
    expect(compiled.querySelector('h2')?.textContent).toContain('Eat breakfast');
    expect(compiled.querySelector('[role="timer"]')?.getAttribute('aria-label')).toBe(
      '10 min left',
    );
    expect(text(compiled)).not.toContain('Next:');
  });

  it('pauses, resumes and stops a child-started timer', async () => {
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => morning()) });
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;

    button(compiled, 'Start')!.click();
    await settle(fixture);
    button(compiled, 'Pause')!.click();
    await settle(fixture);
    expect(compiled.querySelector('[role="timer"]')?.getAttribute('aria-label')).toBe('Paused');

    button(compiled, 'Keep going')!.click();
    await settle(fixture);
    expect(button(compiled, 'Pause')).toBeTruthy();

    button(compiled, 'Stop timer')!.click();
    await settle(fixture);
    expect(button(compiled, 'Start')).toBeTruthy();
    expect(localStorage.getItem('buddy_child_timer')).toBeNull();
  });

  it('keeps a timer started on the child home', async () => {
    localStorage.setItem(
      'buddy_child_timer',
      JSON.stringify({
        itemId: 'run-1',
        date: today,
        anchor: { subtaskKey: `run-1:sub-1:${today}`, startedAtMs: NOW - 60_000, pausedAtMs: NOW },
      }),
    );
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => morning()) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[role="timer"]')?.getAttribute('aria-label')).toBe('Paused');
    expect(button(compiled, 'Keep going')).toBeTruthy();
  });

  it('shows the All done card once every step is ticked', async () => {
    const done = morning().map((occurrence) => ({ ...occurrence, isCompleted: true }));
    const { fixture } = await setup({ listTodayOccurrences: vi.fn(async () => done) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(text(compiled)).toContain('All done! Great job.');
    expect(compiled.querySelector('[role="timer"]')).toBeNull();
  });

  it("shows the All done card, not an error, for a run that isn't today's", async () => {
    const { fixture } = await setup(
      { listTodayOccurrences: vi.fn(async () => morning()) },
      'deleted-run',
    );
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(text(compiled)).toContain('All done! Great job.');
    expect(text(compiled)).not.toContain('Something went wrong');
  });

  it('shows the load error when today cannot be loaded', async () => {
    const { fixture } = await setup({
      listTodayOccurrences: vi.fn(async () => Promise.reject(new Error('offline'))),
    });
    await settle(fixture);

    expect(text(fixture.nativeElement as HTMLElement)).toContain('Something went wrong');
  });

  it('shows the error when marking a step done fails, and keeps the step', async () => {
    const { fixture } = await setup({
      listTodayOccurrences: vi.fn(async () => morning()),
      setTaskCompletion: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;

    button(compiled, "I'm done")!.click();
    await settle(fixture);

    expect(text(compiled)).toContain('Something went wrong');
    expect(compiled.querySelector('h2')?.textContent).toContain('Get dressed');
  });
});
