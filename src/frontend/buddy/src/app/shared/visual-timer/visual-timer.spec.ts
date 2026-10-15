import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { nestOccurrence } from '../../../testing/occurrence-fixture';
import { CalendarOccurrence } from '../../core/calendars.service';
import { UsersService } from '../../core/users.service';
import { TimerState } from '../../core/visual-timer';
import { VisualTimer } from './visual-timer';

const MINUTE = 60_000;
const NOW = Date.parse('2026-10-12T07:00:00Z');

function subtask(subtaskId: string): CalendarOccurrence {
  return nestOccurrence<CalendarOccurrence>({
    itemId: 'run-1',
    kind: 'Task',
    title: subtaskId,
    icon: '🧹',
    iconOverride: null,
    color: '#0f0',
    startsAt: '2026-10-12T07:05:00Z',
    endsAt: '2026-10-12T07:15:00Z',
    isAllDay: false,
    isCompleted: false,
    isSkipped: false,
    isRepeating: false,
    createdBy: 'g',
    lastModifiedBy: 'g',
    assignedTo: 'c',
    calendarId: 'cal',
    calendarName: 'Home',
    parentTitle: 'Morning',
    subtaskId,
  });
}

function state(overrides: Partial<TimerState> = {}): TimerState {
  return {
    current: subtask('dress'),
    next: null,
    remainingMs: 10 * MINUTE,
    budgetMs: 10 * MINUTE,
    runRemainingMs: 20 * MINUTE,
    runBudgetMs: 30 * MINUTE,
    deadlineMs: null,
    startsAtMs: Date.parse('2026-10-12T07:05:00Z'),
    phase: 'upcoming',
    childStarted: false,
    ...overrides,
  };
}

function running(remainingMs: number, overrides: Partial<TimerState> = {}): TimerState {
  return state({
    phase: 'running',
    remainingMs,
    deadlineMs: Date.now() + remainingMs,
    runRemainingMs: remainingMs + 10 * MINUTE,
    ...overrides,
  });
}

describe('VisualTimer', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  function setup(
    initial: TimerState,
    inputs: Record<string, unknown> = {},
  ): ComponentFixture<VisualTimer> {
    TestBed.configureTestingModule({
      imports: [VisualTimer],
      providers: [{ provide: UsersService, useValue: { timeZoneId: signal('UTC').asReadonly() } }],
    });
    const fixture = TestBed.createComponent(VisualTimer);
    fixture.componentRef.setInput('state', initial);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  function timer(fixture: ComponentFixture<VisualTimer>): HTMLElement {
    return (fixture.nativeElement as HTMLElement).querySelector('[role="timer"]')!;
  }

  function liveRegion(fixture: ComponentFixture<VisualTimer>): string {
    return (
      (fixture.nativeElement as HTMLElement).querySelector('[aria-live="polite"]')?.textContent ??
      ''
    ).trim();
  }

  it('labels the time left in whole minutes, rounded up, as the accessible name', () => {
    const fixture = setup(running(4 * MINUTE + 1_000));

    expect(timer(fixture).getAttribute('aria-label')).toBe('5 min left');
    expect(timer(fixture).textContent).toContain('5 min left');
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('svg')?.getAttribute('aria-hidden'),
    ).toBe('true');
  });

  it('says less than a minute rather than zero minutes', () => {
    const fixture = setup(running(20_000));

    expect(timer(fixture).getAttribute('aria-label')).toBe('Less than a minute left');
  });

  it("shows the calm time's up line and an empty disc", () => {
    const fixture = setup(state({ phase: 'timesUp', remainingMs: 0 }));

    expect(timer(fixture).getAttribute('aria-label')).toBe("Time's up. Finish when you're ready.");
    expect(timer(fixture).getAttribute('data-phase')).toBe('timesUp');
    expect((fixture.nativeElement as HTMLElement).querySelector('path')).toBeNull();
  });

  it('shows Paused, and when an upcoming step starts', () => {
    const paused = setup(state({ phase: 'paused', remainingMs: 3 * MINUTE }));
    expect(timer(paused).getAttribute('aria-label')).toBe('Paused');

    TestBed.resetTestingModule();
    const upcoming = setup(state());
    expect(timer(upcoming).getAttribute('aria-label')).toBe('Starts at 7:05 AM');
    // A full disc is a circle in the run's colour, not a wedge.
    expect(
      (upcoming.nativeElement as HTMLElement).querySelector('circle[fill="#34d399"]'),
    ).not.toBeNull();
  });

  it('draws a bar for the whole run, labelled with its title', () => {
    const fixture = setup(
      state({ phase: 'paused', remainingMs: 3 * MINUTE, runRemainingMs: 15 * MINUTE }),
      {
        variant: 'bar',
        title: 'Morning',
        color: '#123456',
      },
    );

    expect(timer(fixture).getAttribute('aria-label')).toBe('15 min left of Morning');
    const fill = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '[style*="background-color"]',
    )!;
    expect(fill.style.width).toBe('50%');
    // The bar speaks too: on the child home it is the one timer that stays mounted.
    expect((fixture.nativeElement as HTMLElement).querySelector('[aria-live]')).not.toBeNull();
  });

  it('has no live region when told not to announce', () => {
    const fixture = setup(running(3 * MINUTE), { announce: false });

    expect((fixture.nativeElement as HTMLElement).querySelector('[aria-live]')).toBeNull();
  });

  it('counts down once a second while running, and reads as time is up at the deadline', () => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    const fixture = setup(running(90_000));
    expect(timer(fixture).getAttribute('aria-label')).toBe('2 min left');

    vi.advanceTimersByTime(31_000);
    fixture.detectChanges();
    expect(timer(fixture).getAttribute('aria-label')).toBe('Less than a minute left');

    vi.advanceTimersByTime(60_000);
    fixture.detectChanges();
    expect(timer(fixture).getAttribute('aria-label')).toBe("Time's up. Finish when you're ready.");
  });

  it('does not tick while paused, and stops ticking when destroyed', () => {
    const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');
    const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');

    const ticks = () =>
      setIntervalSpy.mock.calls
        .map((call, index) => ({ delay: call[1], id: setIntervalSpy.mock.results[index]!.value }))
        .filter((call) => call.delay === 1_000);

    const paused = setup(state({ phase: 'paused', remainingMs: 3 * MINUTE }));
    expect(ticks()).toHaveLength(0);
    paused.destroy();

    TestBed.resetTestingModule();
    const fixture = setup(running(3 * MINUTE));
    expect(ticks()).toHaveLength(1);
    const id = ticks()[0]!.id;

    fixture.destroy();
    expect(clearIntervalSpy).toHaveBeenCalledWith(id);
  });

  it('moves the wedge smoothly, but in whole minutes under reduced motion', () => {
    // 4.5 of 10 minutes left: 45% smoothly, 5 of 10 minutes (half the disc) with reduced motion.
    const smooth = setup(running(4.5 * MINUTE));
    const smoothPath = (smooth.nativeElement as HTMLElement)
      .querySelector('path')!
      .getAttribute('d');
    expect(smoothPath).not.toContain('50.000 97.000');

    TestBed.resetTestingModule();
    vi.spyOn(window, 'matchMedia').mockImplementation(
      (query: string) =>
        ({ matches: query === '(prefers-reduced-motion: reduce)', media: query }) as MediaQueryList,
    );
    const stepped = setup(running(4.5 * MINUTE));
    expect(
      (stepped.nativeElement as HTMLElement).querySelector('path')!.getAttribute('d'),
    ).toContain('50.000 97.000');
  });

  it('announces changes of phase and step only, never on first render', () => {
    const fixture = setup(running(3 * MINUTE));
    expect(liveRegion(fixture)).toBe('');

    fixture.componentRef.setInput('state', running(2 * MINUTE));
    fixture.detectChanges();
    expect(liveRegion(fixture)).toBe('');

    fixture.componentRef.setInput('state', state({ phase: 'paused', remainingMs: 2 * MINUTE }));
    fixture.detectChanges();
    expect(liveRegion(fixture)).toBe('Timer paused.');

    fixture.componentRef.setInput('state', running(2 * MINUTE, { childStarted: true }));
    fixture.detectChanges();
    expect(liveRegion(fixture)).toBe('Timer started for dress.');

    fixture.componentRef.setInput('state', state({ phase: 'timesUp', remainingMs: 0 }));
    fixture.detectChanges();
    expect(liveRegion(fixture)).toBe("Time's up for dress.");

    fixture.componentRef.setInput('state', running(5 * MINUTE, { current: subtask('eat') }));
    fixture.detectChanges();
    expect(liveRegion(fixture)).toBe('Next step: eat.');
  });

  it('announces a child tapping Start while the schedule was already running', () => {
    const fixture = setup(running(3 * MINUTE));

    fixture.componentRef.setInput('state', running(5 * MINUTE, { childStarted: true }));
    fixture.detectChanges();

    expect(liveRegion(fixture)).toBe('Timer started for dress.');
  });
});
