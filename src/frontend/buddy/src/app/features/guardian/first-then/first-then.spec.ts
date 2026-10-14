import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import {
  FirstThenBoard,
  FirstThenService,
  GuardianFirstThen,
} from '../../../core/first-then.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { nestOccurrence } from '../../../../testing/occurrence-fixture';
import {
  GUARDIAN_FIRST_THEN_POLL_MS,
  GuardianFirstThen as GuardianFirstThenPage,
} from './first-then';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const TODAY = '2026-10-14';

function child(id: string, givenName: string): ChildSummary {
  return { id, name: { givenName, familyName: 'Hansen' }, timeZoneId: 'UTC' } as ChildSummary;
}

function board(overrides: Partial<FirstThenBoard> = {}): FirstThenBoard {
  return {
    revision: 3,
    state: 'Waiting',
    first: { title: 'Homework', icon: '📚' },
    then: { title: 'iPad', icon: '📱' },
    thenMinutes: 20,
    unlockedAt: null,
    lastModifiedAt: `${TODAY}T10:00:00Z`,
    lastModifiedByName: 'Sara',
    ...overrides,
  };
}

const none: FirstThenBoard = {
  ...board(),
  revision: 0,
  state: 'None',
  first: null,
  then: null,
  thenMinutes: null,
  lastModifiedByName: null,
};

function view(b: FirstThenBoard, overrides: Partial<GuardianFirstThen> = {}): GuardianFirstThen {
  return { board: b, recentFirsts: [], recentThens: [], ...overrides };
}

function task(title: string, assignedTo: string): CalendarOccurrence {
  return nestOccurrence<CalendarOccurrence>({
    itemId: title,
    kind: 'Task',
    title,
    icon: '✏️',
    iconOverride: null,
    color: '#112233',
    startsAt: null,
    endsAt: null,
    dueAt: `${TODAY}T16:00:00Z`,
    isAllDay: false,
    isCompleted: false,
    createdBy: 'guardian-1',
    lastModifiedBy: 'guardian-1',
    assignedTo,
    calendarId: 'cal-1',
    calendarName: 'Home',
    parentTitle: null,
    subtaskId: null,
  } as never);
}

interface Options {
  children?: ChildSummary[];
  views?: Record<string, GuardianFirstThen>;
  firstThen?: Partial<FirstThenService>;
  occurrences?: () => Promise<CalendarOccurrence[]>;
}

describe('GuardianFirstThen', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  async function setup(options: Options = {}) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(`${TODAY}T12:00:00`));

    const views = options.views ?? { emil: view(board()) };
    const firstThen = {
      getBoard: vi.fn(async (childId: string) => views[childId] ?? view(none)),
      setBoard: vi.fn(async () => board()),
      act: vi.fn(async () => board()),
      ...options.firstThen,
    };
    const guardians: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => options.children ?? [child('emil', 'Emil')]),
    };
    const calendars: Partial<CalendarsService> = {
      listTodayOccurrences: vi.fn(options.occurrences ?? (async () => [])),
    };

    await TestBed.configureTestingModule({
      imports: [GuardianFirstThenPage],
      providers: [
        provideRouter([]),
        { provide: FirstThenService, useValue: firstThen },
        { provide: GuardiansService, useValue: guardians },
        { provide: CalendarsService, useValue: calendars },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianFirstThenPage);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, firstThen };
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return [...compiled.querySelectorAll('button')].find((b) => b.textContent?.trim() === text);
  }

  function chip(compiled: HTMLElement, title: string): HTMLButtonElement | undefined {
    return [...compiled.querySelectorAll<HTMLButtonElement>('button[aria-pressed]')].find((b) =>
      b.textContent?.includes(title),
    );
  }

  function state(compiled: HTMLElement): string {
    return (
      compiled.querySelector('[data-testid="guardian-first-then-state"]')?.textContent?.trim() ?? ''
    );
  }

  it('shows the current waiting board with who changed it, Mark done and End', async () => {
    const { compiled, firstThen } = await setup();

    expect(firstThen.getBoard).toHaveBeenCalledWith('emil');
    const shown = compiled.querySelector('[data-testid="guardian-first-then-board"]')!;
    expect(shown.textContent).toContain('Homework');
    expect(shown.textContent).toContain('iPad');
    expect(shown.textContent).toContain('20 min');
    expect(state(compiled)).toContain('Homework');
    expect(state(compiled)).not.toContain('From yesterday');
    expect(compiled.textContent).toContain('Sara');
    expect(button(compiled, 'Mark done')).toBeDefined();
    expect(button(compiled, 'Undo')).toBeUndefined();
    expect(button(compiled, 'End board')).toBeDefined();
  });

  it('marks the First done with the revision shown, then reloads the board', async () => {
    const { fixture, compiled, firstThen } = await setup();

    button(compiled, 'Mark done')!.click();
    await settle(fixture);

    expect(firstThen.act).toHaveBeenCalledWith('emil', 'first/complete', 3);
    expect(firstThen.getBoard).toHaveBeenCalledTimes(2);
  });

  it('offers Undo on an unlocked board and ends it', async () => {
    const { fixture, compiled, firstThen } = await setup({
      views: { emil: view(board({ state: 'Unlocked', unlockedAt: `${TODAY}T10:00:00Z` })) },
    });

    expect(state(compiled)).toContain('iPad');
    expect(button(compiled, 'Mark done')).toBeUndefined();

    button(compiled, 'Undo')!.click();
    await settle(fixture);
    expect(firstThen.act).toHaveBeenCalledWith('emil', 'first/reopen', 3);

    button(compiled, 'End board')!.click();
    await settle(fixture);
    expect(firstThen.act).toHaveBeenCalledWith('emil', 'end', 3);
  });

  it('labels a board left over from yesterday', async () => {
    const { compiled } = await setup({
      views: { emil: view(board({ lastModifiedAt: '2026-10-13T10:00:00Z' })) },
    });

    expect(state(compiled)).toContain('From yesterday');
  });

  it('keeps the board and its revision on screen when a poll fails', async () => {
    let fail = false;
    const { fixture, compiled, firstThen } = await setup({
      firstThen: {
        getBoard: vi.fn(async () => {
          if (fail) {
            throw new HttpErrorResponse({ status: 0 });
          }
          return view(board());
        }),
      },
    });

    fail = true;
    document.dispatchEvent(new Event('visibilitychange'));
    await settle(fixture);

    expect(firstThen.getBoard).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('[data-testid="guardian-first-then-board"]')).not.toBeNull();
    button(compiled, 'Mark done')!.click();
    await settle(fixture);
    expect(firstThen.act).toHaveBeenCalledWith('emil', 'first/complete', 3);
  });

  it('says there is no board when none is current', async () => {
    const { compiled } = await setup({ views: { emil: view(none) } });

    expect(compiled.querySelector('[data-testid="guardian-first-then-none"]')).not.toBeNull();
    expect(button(compiled, 'End board')).toBeUndefined();
  });

  it('shows a note instead of an error when the board changed meanwhile', async () => {
    const { fixture, compiled } = await setup({
      firstThen: {
        act: vi.fn(async () => {
          throw new HttpErrorResponse({
            status: 409,
            error: { code: 'first_then_board_changed' },
          });
        }),
      },
    });

    button(compiled, 'Mark done')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="status"]')).not.toBeNull();
    expect(compiled.querySelector('[role="alert"]')).toBeNull();
  });

  it('treats the central concurrency conflict as a changed board too', async () => {
    const { fixture, compiled } = await setup({
      firstThen: {
        act: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 409, error: { code: 'concurrency_conflict' } });
        }),
      },
    });

    button(compiled, 'End board')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="status"]')).not.toBeNull();
    expect(compiled.querySelector('[role="alert"]')).toBeNull();
  });

  it('keeps the draft when saving fails', async () => {
    const { fixture, compiled } = await setup({
      views: { emil: view(none) },
      firstThen: {
        setBoard: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 500 });
        }),
      },
    });

    chip(compiled, 'Homework')!.click();
    chip(compiled, 'iPad')!.click();
    await settle(fixture);
    button(compiled, 'Show to Emil')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="alert"]')).not.toBeNull();
    expect(compiled.querySelector<HTMLInputElement>('#firstThenFirstTitle')!.value).toBe(
      'Homework',
    );
  });

  it('shows a translated error when an action fails', async () => {
    const { fixture, compiled } = await setup({
      firstThen: {
        act: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 500 });
        }),
      },
    });

    button(compiled, 'End board')!.click();
    await settle(fixture);

    const alert = compiled.querySelector('[role="alert"]');
    expect(alert).not.toBeNull();
    expect(alert!.textContent).not.toContain('firstThen.');
  });

  it('sets a new board from the form with the current revision', async () => {
    const { fixture, compiled, firstThen } = await setup({ views: { emil: view(none) } });

    const set = (id: string, value: string) => {
      const input = compiled.querySelector<HTMLInputElement>(`#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    set('firstThenFirstTitle', 'Homework');
    set('firstThenFirstIcon', '📚');
    set('firstThenThenTitle', 'iPad');
    set('firstThenThenIcon', '📱');
    await settle(fixture);

    button(compiled, 'Show to Emil')!.click();
    await settle(fixture);

    expect(firstThen.setBoard).toHaveBeenCalledWith('emil', {
      revision: 0,
      first: { title: 'Homework', icon: '📚' },
      then: { title: 'iPad', icon: '📱' },
      thenMinutes: null,
    });
    expect(compiled.querySelector<HTMLInputElement>('#firstThenFirstTitle')!.value).toBe('');
  });

  it("offers the selected child's open tasks as First suggestions", async () => {
    const { compiled } = await setup({
      occurrences: async () => [task('Maths p. 12', 'emil'), task('Piano', 'ida')],
    });

    expect(chip(compiled, 'Maths p. 12')).toBeDefined();
    expect(compiled.textContent).not.toContain('Piano');
  });

  it('keeps the page working when today’s tasks fail to load', async () => {
    const { compiled } = await setup({
      occurrences: async () => {
        throw new Error('offline');
      },
    });

    expect(button(compiled, 'Show to Emil')).toBeDefined();
  });

  it('switches child with the selector', async () => {
    const { fixture, compiled, firstThen } = await setup({
      children: [child('emil', 'Emil'), child('ida', 'Ida')],
      views: { emil: view(board()), ida: view(none) },
    });

    const select = compiled.querySelector<HTMLSelectElement>('#firstThenChildId')!;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(firstThen.getBoard).toHaveBeenLastCalledWith('ida');
    expect(compiled.querySelector('[data-testid="guardian-first-then-none"]')).not.toBeNull();
    expect(button(compiled, 'Show to Ida')).toBeDefined();
  });

  it('says so when the guardian has no children', async () => {
    const { compiled, firstThen } = await setup({ children: [] });

    expect(firstThen.getBoard).not.toHaveBeenCalled();
    expect(compiled.querySelector('#firstThenChildId')).toBeNull();
  });

  it('polls the board while open and stops when destroyed', async () => {
    const intervals = vi.spyOn(globalThis, 'setInterval');
    const cleared = vi.spyOn(globalThis, 'clearInterval');
    const { fixture } = await setup();

    expect(intervals).toHaveBeenCalledWith(expect.any(Function), GUARDIAN_FIRST_THEN_POLL_MS);

    fixture.destroy();
    expect(cleared).toHaveBeenCalled();
  });
});
