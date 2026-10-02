import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import {
  CalendarOccurrence,
  CalendarSummary,
  CalendarsService,
} from '../../../core/calendars.service';
import { toIsoDate, todayIsoDate } from '../../../core/date-utils';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildCalendar } from './child-calendar';

describe('ChildCalendar', () => {
  const today = todayIsoDate();

  const currentUser: CurrentUser = {
    id: 'child-1',
    email: { value: 'kid@buddy.test', isVerified: true },
    userName: 'kid',
    name: { givenName: 'Kim', familyName: 'Kid' },
    timeZoneId: 'UTC',
    language: 'en',
  };

  // Mirrors the component's own local-date arithmetic (parseIsoDate + toIsoDate) so expectations
  // don't depend on the host machine's time zone.
  function addDays(isoDate: string, days: number): string {
    const [year, month, day] = isoDate.split('-').map(Number);
    return toIsoDate(new Date(year, month - 1, day + days));
  }

  function calendarSummary(overrides: Partial<CalendarSummary> = {}): CalendarSummary {
    return { id: 'cal-1', name: 'Home', icon: '🏠', role: 2, ...overrides };
  }

  function occurrence(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'item-1',
      kind: 0,
      title: 'Dentist',
      icon: '🦷',
      iconOverride: null,
      color: '#112233',
      startsAt: `${today}T09:00:00Z`,
      endsAt: `${today}T10:00:00Z`,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      calendarId: 'cal-1',
      calendarName: 'Home',
      ...overrides,
    };
  }

  function mealEntry(overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date: today,
      slot: 0,
      mealId: 'meal-1',
      mealName: 'Pancakes',
      icon: '🥞',
      color: '#ffaa00',
      rating: null,
      notes: '',
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides,
    };
  }

  interface Stubs {
    users?: Partial<UsersService>;
    calendars?: Partial<CalendarsService>;
    mealplans?: Partial<MealplansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const usersStub: Partial<UsersService> = {
      timeZoneId: signal('UTC').asReadonly(),
      ensureCurrentUser: vi.fn(async () => currentUser),
      ...stubs.users,
    };
    const calendarsStub: Partial<CalendarsService> = {
      listMyCalendars: vi.fn(async () => [calendarSummary()]),
      listOccurrencesInRange: vi.fn(async () => []),
      setTaskCompletion: vi.fn(async () => ({
        itemId: 'item-1',
        occurrenceDate: today,
        isCompleted: true,
      })),
      ...stubs.calendars,
    };
    const mealplansStub: Partial<MealplansService> = {
      listMealPlan: vi.fn(async () => []),
      ...stubs.mealplans,
    };

    await TestBed.configureTestingModule({
      imports: [ChildCalendar],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: usersStub },
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: MealplansService, useValue: mealplansStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildCalendar);

    return { fixture, calendars: calendarsStub, mealplans: mealplansStub };
  }

  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function findButtonByAriaLabel(
    compiled: HTMLElement,
    label: string,
  ): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.getAttribute('aria-label') === label,
    );
  }

  it('shows the loading message before the week resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading your calendar…');
  });

  it('shows the empty state once loading finishes with no occurrences', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Nothing planned this week');
  });

  it('shows the translated error message when listOccurrencesInRange rejects', async () => {
    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Something went wrong loading your calendar');
  });

  it('requests the current week (today through six days ahead) on first load', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenCalledWith(today, addDays(today, 6));
  });

  it('groups occurrences under the correct day and orders same-day items by time', async () => {
    const tomorrow = addDays(today, 1);
    const early = occurrence({
      itemId: 'early',
      title: 'Early meeting',
      startsAt: `${today}T08:00:00Z`,
      endsAt: `${today}T08:30:00Z`,
    });
    const late = occurrence({
      itemId: 'late',
      title: 'Late meeting',
      startsAt: `${today}T18:00:00Z`,
      endsAt: `${today}T18:30:00Z`,
    });
    const nextDay = occurrence({
      itemId: 'tomorrow',
      title: 'Tomorrow item',
      startsAt: `${tomorrow}T09:00:00Z`,
      endsAt: `${tomorrow}T09:30:00Z`,
    });

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [late, nextDay, early]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const text = compiled.textContent ?? '';
    expect(text.indexOf('Early meeting')).toBeLessThan(text.indexOf('Late meeting'));
    expect(text.indexOf('Late meeting')).toBeLessThan(text.indexOf('Tomorrow item'));
  });

  it('groups a task by its due date and shows a completion toggle', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`,
      assignedTo: 'child-1',
    });

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [task]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Feed the cat');
    expect(findButtonByAriaLabel(compiled, 'Mark done')).toBeTruthy();
  });

  it("toggles a task's completion", async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`,
    });
    const { fixture, calendars } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [task]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'Mark done')?.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true, null);
    expect(findButtonByAriaLabel(compiled, 'Mark not done')).toBeTruthy();
  });

  it('disables the completion toggle for a task due on a future day', async () => {
    const tomorrow = addDays(today, 1);
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${tomorrow}T17:00:00Z`,
    });

    const { fixture, calendars } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [task]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const button = findButtonByAriaLabel(compiled, 'Mark done');
    expect(button?.disabled).toBe(true);

    button?.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).not.toHaveBeenCalled();
  });

  it('refuses to complete a future task even if its toggle is clicked while enabled', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${addDays(today, 1)}T17:00:00Z`,
    });
    const { fixture, calendars } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [task]) },
    });
    await settle(fixture);

    const button = findButtonByAriaLabel(fixture.nativeElement as HTMLElement, 'Mark done')!;
    button.disabled = false;
    button.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).not.toHaveBeenCalled();
  });

  it("disables a task's toggle while its completion is saving, and re-enables it afterwards", async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`,
    });
    let resolveSave!: () => void;
    const setTaskCompletion = vi.fn(
      () =>
        new Promise<{ itemId: string; occurrenceDate: string; isCompleted: boolean }>((resolve) => {
          resolveSave = () =>
            resolve({ itemId: 'task-1', occurrenceDate: today, isCompleted: true });
        }),
    );
    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [task]), setTaskCompletion },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'Mark done')!.click();
    fixture.detectChanges();

    expect(findButtonByAriaLabel(compiled, 'Mark done')?.disabled).toBe(true);

    resolveSave();
    await settle(fixture);

    expect(findButtonByAriaLabel(compiled, 'Mark not done')?.disabled).toBe(false);
  });

  it('shows an error and leaves the task unchanged when saving its completion fails', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`,
    });
    const { fixture } = await setup({
      calendars: {
        listOccurrencesInRange: vi.fn(async () => [task]),
        setTaskCompletion: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'Mark done')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(
      'Something went wrong updating that task. Try again in a bit.',
    );
    expect(findButtonByAriaLabel(compiled, 'Mark done')?.disabled).toBe(false);
    expect(findButtonByAriaLabel(compiled, 'Mark not done')).toBeUndefined();
  });

  it('clears a previous load error once a later week loads successfully', async () => {
    const listOccurrencesInRange = vi
      .fn<CalendarsService['listOccurrencesInRange']>()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue([]);
    const { fixture } = await setup({ calendars: { listOccurrencesInRange } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Something went wrong loading your calendar');

    Array.from(compiled.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Next week'))
      ?.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Something went wrong loading your calendar');
  });

  it('labels each day with its short weekday, month, and day number', async () => {
    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [occurrence()]) },
    });
    await settle(fixture);

    const [year, month, day] = today.split('-').map(Number);
    const expected = new Date(year, month - 1, day).toLocaleDateString('en', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
    const heading = (fixture.nativeElement as HTMLElement).querySelector('h2');
    expect(heading?.textContent?.trim()).toBe(expected);
  });

  it('renders "All day" instead of a time range for an all-day occurrence', async () => {
    const allDay = occurrence({ itemId: 'all-day', title: 'Field trip', isAllDay: true });
    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [allDay]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('All day');
  });

  it('shows no filter toggles when only one calendar is accessible', async () => {
    const { fixture } = await setup();
    await settle(fixture);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('button[role="switch"]'),
    ).toBeFalsy();
  });

  it('shows a per-calendar filter when more than one calendar is accessible', async () => {
    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [
          calendarSummary(),
          calendarSummary({ id: 'cal-2', name: 'School' }),
        ]),
      },
    });
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('School');
  });

  it('hides occurrences for a calendar toggled off', async () => {
    const first = occurrence({
      itemId: 'a',
      title: 'Home item',
      calendarId: 'cal-1',
      calendarName: 'Home',
    });
    const second = occurrence({
      itemId: 'b',
      title: 'School item',
      calendarId: 'cal-2',
      calendarName: 'School',
    });

    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [
          calendarSummary(),
          calendarSummary({ id: 'cal-2', name: 'School' }),
        ]),
        listOccurrencesInRange: vi.fn(async () => [first, second]),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'School')?.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Home item');
    expect(compiled.textContent).not.toContain('School item');
  });

  it("shows a hidden calendar's occurrences again when it is toggled back on", async () => {
    const first = occurrence({
      itemId: 'a',
      title: 'Home item',
      calendarId: 'cal-1',
      calendarName: 'Home',
    });
    const second = occurrence({
      itemId: 'b',
      title: 'School item',
      calendarId: 'cal-2',
      calendarName: 'School',
    });

    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [
          calendarSummary(),
          calendarSummary({ id: 'cal-2', name: 'School' }),
        ]),
        listOccurrencesInRange: vi.fn(async () => [first, second]),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'School')?.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('School item');

    findButtonByAriaLabel(compiled, 'School')?.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Home item');
    expect(compiled.textContent).toContain('School item');
  });

  it('never renders create, edit, or delete controls', async () => {
    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [occurrence()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('form')).toBeFalsy();
    expect(compiled.querySelector('input[type="text"]')).toBeFalsy();
    expect(compiled.querySelector('input[type="color"]')).toBeFalsy();
  });

  // ----- Template-scheduled task runs (grouped rendering + the compound-key fix) -----

  it('renders a 3-subtask run as one block showing the parent title once, with a toggle per subtask', async () => {
    const subtasks = [
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        title: 'Brush teeth',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:00:00Z`,
      }),
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-2',
        parentTitle: 'Morning routine',
        title: 'Get dressed',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:10:00Z`,
      }),
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-3',
        parentTitle: 'Morning routine',
        title: 'Eat breakfast',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:20:00Z`,
      }),
    ];

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => subtasks) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent?.match(/Morning routine/g)?.length).toBe(1);
    expect(compiled.textContent).toContain('Brush teeth');
    expect(compiled.textContent).toContain('Get dressed');
    expect(compiled.textContent).toContain('Eat breakfast');
    expect(compiled.querySelectorAll('ul.ml-2 button')).toHaveLength(3);
  });

  it("orders a run's subtasks by time even when they arrive out of order", async () => {
    const subtasks = [
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-3',
        parentTitle: 'Morning routine',
        title: 'Eat breakfast',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:20:00Z`,
      }),
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        title: 'Brush teeth',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:00:00Z`,
      }),
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-2',
        parentTitle: 'Morning routine',
        title: 'Get dressed',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:10:00Z`,
      }),
    ];

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => subtasks) },
    });
    await settle(fixture);

    const titles = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('ul.ml-2 > li'),
    ).map((li) => li.textContent?.trim());
    expect(titles).toEqual([
      expect.stringContaining('Brush teeth'),
      expect.stringContaining('Get dressed'),
      expect.stringContaining('Eat breakfast'),
    ]);
  });

  it('completing one subtask of a 3-subtask run does not flip the other subtasks (the compound-key fix)', async () => {
    const subtasks = [
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-1',
        parentTitle: 'Morning routine',
        title: 'Brush teeth',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:00:00Z`,
      }),
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-2',
        parentTitle: 'Morning routine',
        title: 'Get dressed',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:10:00Z`,
      }),
      occurrence({
        itemId: 'run-1',
        kind: 1,
        subtaskId: 'sub-3',
        parentTitle: 'Morning routine',
        title: 'Eat breakfast',
        startsAt: null,
        endsAt: null,
        dueAt: `${today}T08:20:00Z`,
      }),
    ];

    const { fixture, calendars } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => subtasks) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggles = Array.from(compiled.querySelectorAll<HTMLButtonElement>('ul.ml-2 button'));
    expect(toggles).toHaveLength(3);

    toggles[0].click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith(
      'cal-1',
      'run-1',
      today,
      true,
      'sub-1',
    );

    const afterToggle = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('ul.ml-2 button'),
    );
    expect(afterToggle[0].textContent?.trim()).toBe('✓');
    // The sibling subtasks must remain untouched -- without the compound (itemId + subtaskId) key,
    // every occurrence sharing itemId "run-1" would have been optimistically flipped too.
    expect(afterToggle[1].textContent?.trim()).toBe('');
    expect(afterToggle[2].textContent?.trim()).toBe('');
  });

  // ----- Meal plan integration -----

  it('interleaves a meal among tasks/events on the same day in slot-chronological order', async () => {
    const breakfast = mealEntry({ mealId: 'breakfast', mealName: 'Pancakes', slot: 0 });
    const snack = mealEntry({ mealId: 'snack', mealName: 'Apple slices', slot: 3 });
    const dinner = mealEntry({ mealId: 'dinner', mealName: 'Pasta', slot: 2 });
    const lunchTask = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Homework',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T13:00:00Z`,
    });

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [lunchTask]) },
      mealplans: { listMealPlan: vi.fn(async () => [breakfast, snack, dinner]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const text = compiled.textContent ?? '';

    // Chronological slot order is Breakfast (07:00) < Homework (13:00) < Snack (15:00) < Dinner
    // (18:00) -- notably Snack sorts before Dinner despite MealSlot's declaration order (Dinner=2,
    // Snack=3) putting it after.
    expect(text.indexOf('Pancakes')).toBeLessThan(text.indexOf('Homework'));
    expect(text.indexOf('Homework')).toBeLessThan(text.indexOf('Apple slices'));
    expect(text.indexOf('Apple slices')).toBeLessThan(text.indexOf('Pasta'));
  });

  it('places breakfast at 07:00 and lunch at 12:00 among timed events', async () => {
    const events = [
      occurrence({
        itemId: 'swim',
        title: 'Early swim',
        startsAt: `${today}T06:00:00Z`,
        endsAt: `${today}T06:30:00Z`,
      }),
      occurrence({
        itemId: 'read',
        title: 'Reading time',
        startsAt: `${today}T10:00:00Z`,
        endsAt: `${today}T10:30:00Z`,
      }),
      occurrence({
        itemId: 'park',
        title: 'Park trip',
        startsAt: `${today}T13:00:00Z`,
        endsAt: `${today}T14:00:00Z`,
      }),
    ];
    const meals = [
      mealEntry({ slot: 0, mealName: 'Pancakes' }),
      mealEntry({ slot: 1, mealId: 'meal-2', mealName: 'Soup' }),
    ];

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => events) },
      mealplans: { listMealPlan: vi.fn(async () => meals) },
    });
    await settle(fixture);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text.indexOf('Early swim')).toBeLessThan(text.indexOf('Pancakes'));
    expect(text.indexOf('Pancakes')).toBeLessThan(text.indexOf('Reading time'));
    expect(text.indexOf('Reading time')).toBeLessThan(text.indexOf('Soup'));
    expect(text.indexOf('Soup')).toBeLessThan(text.indexOf('Park trip'));
  });

  it.each([
    [1 as MealSlot, 'Lunch'],
    [2 as MealSlot, 'Dinner'],
    [3 as MealSlot, 'Snack'],
  ])('labels a meal in slot %s as "%s"', async (slot, label) => {
    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => [mealEntry({ slot, mealName: 'Stew' })]) },
    });
    await settle(fixture);

    const mealRow = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li')).find(
      (li) => li.textContent?.includes('Stew'),
    );
    expect(mealRow?.querySelector(':scope > span:last-child')?.textContent?.trim()).toBe(label);
  });

  it('shows a day with only a meal entry, without folding it into the empty state', async () => {
    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => [mealEntry()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pancakes');
    expect(compiled.textContent).not.toContain('Nothing planned this week');
  });

  it('renders a meal row with its icon, name, and slot label, with no completion affordance', async () => {
    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => [mealEntry()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('🥞');
    expect(compiled.textContent).toContain('Pancakes');
    expect(compiled.textContent).toContain('Breakfast');

    const mealRow = Array.from(compiled.querySelectorAll('li')).find((li) =>
      li.textContent?.includes('Pancakes'),
    );
    expect(mealRow?.querySelector('button')).toBeFalsy();
  });

  it('requests the meal plan for the current week for the signed-in child', async () => {
    const { fixture, mealplans } = await setup();
    await settle(fixture);

    expect(mealplans.listMealPlan).toHaveBeenCalledWith(
      { kind: 'family', childId: 'child-1' },
      today,
      addDays(today, 6),
    );
  });

  it('navigates the visible week forward and backward', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(compiled.querySelectorAll('button'));
    buttons.find((button) => button.textContent?.includes('Next week'))?.click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith(
      addDays(today, 7),
      addDays(today, 13),
    );

    buttons.find((button) => button.textContent?.includes('Previous week'))?.click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith(today, addDays(today, 6));
  });
});
