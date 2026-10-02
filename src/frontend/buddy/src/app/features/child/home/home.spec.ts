import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  CalendarOccurrence,
  CalendarsService,
  TaskCompletion,
} from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { GuardianSummary, GuardiansService, SiblingSummary } from '../../../core/guardians.service';
import { Meal, MealPlanEntry, MealplansService } from '../../../core/mealplans.service';
import { MedicineDoseOccurrence, MedicinesService } from '../../../core/medicines.service';
import { PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { ProgressService } from '../../../core/progress.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildHome } from './home';

describe('ChildHome', () => {
  const currentUser: CurrentUser = {
    id: 'child-1',
    email: { value: 'kid@buddy.test', isVerified: true },
    userName: 'kid',
    name: { givenName: 'Kim', familyName: 'Kid' },
    timeZoneId: 'UTC',
    language: 'en',
  };

  const today = todayIsoDate();

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (reason: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  function occurrence(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'item-1',
      kind: 1,
      title: 'Item',
      icon: '🧹',
      iconOverride: null,
      color: '#000',
      startsAt: null,
      endsAt: null,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: 'child-1',
      calendarId: 'cal-1',
      calendarName: 'Home',
      ...overrides,
    };
  }

  function doseOccurrence(overrides: Partial<MedicineDoseOccurrence> = {}): MedicineDoseOccurrence {
    return {
      medicineId: 'med-1',
      name: 'Vitamin',
      dosage: '1 tablet',
      icon: '💊',
      color: '#0f0',
      date: today,
      time: '09:00:00',
      status: 0,
      ...overrides,
    };
  }

  function ratedMeal(ratings: Meal['ratings']): Meal {
    return {
      id: 'meal-1',
      name: 'Pancakes',
      description: '',
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
    };
  }

  // The <li> rows of the dashboard card headed by the given (translated) title, each row's text
  // with whitespace collapsed.
  function sectionRows(compiled: HTMLElement, heading: string): HTMLLIElement[] {
    const h2 = Array.from(compiled.querySelectorAll('h2')).find(
      (element) => element.textContent?.trim() === heading,
    );
    return h2
      ? Array.from(h2.parentElement!.querySelectorAll<HTMLLIElement>(':scope > ul > li'))
      : [];
  }

  function rowText(row: Element): string {
    return (row.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  function mealEntry(overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date: today,
      slot: 0,
      mealId: 'meal-1',
      mealName: 'Pancakes',
      icon: '🥞',
      color: '#f00',
      rating: null,
      notes: '',
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides,
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    pickups?: Partial<PickupsService>;
    users?: Partial<UsersService>;
    mealplans?: Partial<MealplansService>;
    medicines?: Partial<MedicinesService>;
    calendars?: Partial<CalendarsService>;
    progress?: Partial<ProgressService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyGuardians: vi.fn(async () => []),
      listMySiblings: vi.fn(async () => []),
      ...stubs.guardians,
    };
    const pickupsStub: Partial<PickupsService> = {
      listSchedule: vi.fn(async () => []),
      ...stubs.pickups,
    };
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => currentUser),
      timeZoneId: signal('UTC').asReadonly(),
      ...stubs.users,
    };
    const mealplansStub: Partial<MealplansService> = {
      listMealPlan: vi.fn(async () => []),
      rateMeal: vi.fn(),
      ...stubs.mealplans,
    };
    const medicinesStub: Partial<MedicinesService> = {
      listDoses: vi.fn(async () => []),
      setDoseStatus: vi.fn(),
      ...stubs.medicines,
    };
    const calendarsStub: Partial<CalendarsService> = {
      listTodayOccurrences: vi.fn(async () => []),
      setTaskCompletion: vi.fn(),
      ...stubs.calendars,
    };
    const progressStub: Partial<ProgressService> = {
      getMyProgress: vi.fn(async () => ({
        totalStars: 0,
        unlockedMilestones: [],
        currentIcon: null,
        nextGoalThreshold: 5,
        nextGoalIcon: '🌱',
        goalPosts: [],
      })),
      ...stubs.progress,
    };

    await TestBed.configureTestingModule({
      imports: [ChildHome],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: PickupsService, useValue: pickupsStub },
        { provide: UsersService, useValue: usersStub },
        { provide: MealplansService, useValue: mealplansStub },
        { provide: MedicinesService, useValue: medicinesStub },
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: ProgressService, useValue: progressStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildHome);

    return {
      fixture,
      guardians: guardiansStub,
      pickups: pickupsStub,
      users: usersStub,
      mealplans: mealplansStub,
      medicines: medicinesStub,
      calendars: calendarsStub,
      progress: progressStub,
    };
  }

  // The app runs zoneless, and none of these stubbed services register a PendingTasks entry, so
  // fixture.whenStable() resolves immediately without actually waiting for them. A macrotask
  // flush lets every already-scheduled microtask in the mocked promise chains drain first.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
  }

  function findButtonByAriaLabel(
    compiled: HTMLElement,
    label: string,
  ): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.getAttribute('aria-label') === label,
    );
  }

  it('shows the loading spinner while the dashboard is still loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the empty state once loading finishes with nothing to show', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Nothing to show yet');
  });

  it('shows the translated error message when loading the dashboard fails', async () => {
    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Something went wrong. Try again in a bit.');
  });

  it('rates a meal and reflects the rating on every slot sharing that meal', async () => {
    const entries = [mealEntry({ slot: 0 }), mealEntry({ slot: 1 })];
    const rateMeal = vi.fn(async () => ({
      id: 'meal-1',
      name: 'Pancakes',
      description: '',
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings: [{ childId: 'child-1', stars: 4, comment: '', ratedAt: '2026-01-01T00:00:00Z' }],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
    }));

    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => entries), rateMeal },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButtons = Array.from(
      compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'),
    );
    expect(starButtons).toHaveLength(10);

    starButtons[3].click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-1', 4, '');
    expect(starButtons[3].classList.contains('text-amber-400')).toBe(true);
    expect(starButtons[8].classList.contains('text-amber-400')).toBe(true);
  });

  it('adds a note to a meal', async () => {
    const rateMeal = vi.fn(async () => ({
      id: 'meal-1',
      name: 'Pancakes',
      description: '',
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings: [
        { childId: 'child-1', stars: 5, comment: 'Yummy!', ratedAt: '2026-01-01T00:00:00Z' },
      ],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
    }));

    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => [mealEntry()]), rateMeal },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a note')?.click();
    fixture.detectChanges();

    const textarea = compiled.querySelector<HTMLTextAreaElement>('textarea')!;
    textarea.value = 'Yummy!';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    findButtonByText(compiled, 'Save')?.click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-1', 5, 'Yummy!');
    expect(compiled.querySelector('textarea')).toBeFalsy();
    expect(compiled.textContent).toContain('Yummy!');
  });

  it('marks a medicine dose as taken', async () => {
    const dose: MedicineDoseOccurrence = {
      medicineId: 'med-1',
      name: 'Vitamin',
      dosage: '1 tablet',
      icon: '💊',
      color: '#0f0',
      date: today,
      time: '09:00:00',
      status: 0,
    };
    const setDoseStatus = vi.fn(async () => ({ ...dose, status: 1 as const }));

    const { fixture, medicines } = await setup({
      medicines: { listDoses: vi.fn(async () => [dose]), setDoseStatus },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Taken')?.click();
    await settle(fixture);

    expect(medicines.setDoseStatus).toHaveBeenCalledWith('child-1', 'med-1', today, '09:00:00', 1);
    expect(compiled.textContent).toContain('Taken ✓');
  });

  it("toggles a task's completion", async () => {
    const task: CalendarOccurrence = {
      itemId: 'task-1',
      kind: 1,
      title: 'Clean room',
      icon: '🧹',
      iconOverride: null,
      color: '#000',
      startsAt: null,
      endsAt: null,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: 'child-1',
      calendarId: 'cal-1',
      calendarName: 'Home',
    };
    const completion: TaskCompletion = {
      itemId: 'task-1',
      occurrenceDate: today,
      isCompleted: true,
    };
    const setTaskCompletion = vi.fn(async () => completion);

    const { fixture, calendars } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => [task]), setTaskCompletion },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'Mark done')?.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true, null);
    expect(findButtonByAriaLabel(compiled, 'Mark not done')).toBeTruthy();
  });

  it('completing one subtask of a template-scheduled run does not flip its sibling subtasks (the compound-key fix)', async () => {
    function subtask(subtaskId: string, title: string): CalendarOccurrence {
      return {
        itemId: 'run-1',
        kind: 1,
        title,
        icon: '🧹',
        iconOverride: null,
        color: '#000',
        startsAt: null,
        endsAt: null,
        dueAt: null,
        isAllDay: false,
        isCompleted: false,
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: 'child-1',
        calendarId: 'cal-1',
        calendarName: 'Home',
        parentTitle: 'Morning routine',
        subtaskId,
      };
    }

    const subtasks = [subtask('sub-1', 'Brush teeth'), subtask('sub-2', 'Get dressed')];
    const setTaskCompletion = vi.fn(
      async () => ({ itemId: 'run-1', occurrenceDate: today, isCompleted: true }) as TaskCompletion,
    );

    const { fixture, calendars } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => subtasks), setTaskCompletion },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggles = compiled.querySelectorAll('button[aria-label="Mark done"]');
    expect(toggles).toHaveLength(2);

    toggles[0].dispatchEvent(new Event('click'));
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith(
      'cal-1',
      'run-1',
      today,
      true,
      'sub-1',
    );

    const afterToggle = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'button[aria-label]',
    );
    const doneCount = Array.from(afterToggle).filter(
      (button) => button.getAttribute('aria-label') === 'Mark not done',
    ).length;
    // Only the toggled subtask flips to "Mark not done" -- the sibling stays "Mark done".
    expect(doneCount).toBe(1);
  });

  it("groups a template-scheduled run's subtasks under their parent task's title", async () => {
    function subtask(subtaskId: string, title: string): CalendarOccurrence {
      return {
        itemId: 'run-1',
        kind: 1,
        title,
        icon: '🧹',
        iconOverride: null,
        color: '#000',
        startsAt: null,
        endsAt: null,
        dueAt: null,
        isAllDay: false,
        isCompleted: false,
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: 'child-1',
        calendarId: 'cal-1',
        calendarName: 'Home',
        parentTitle: 'Go to bed',
        subtaskId,
      };
    }

    const subtasks = [subtask('sub-1', 'Brush teeth'), subtask('sub-2', 'Put on pajamas')];

    const { fixture } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => subtasks) },
    });
    await settle(fixture);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Go to bed');
    expect(text).toContain('Brush teeth');
    expect(text).toContain('Put on pajamas');
  });

  it("shows today's events including their time", async () => {
    const event: CalendarOccurrence = {
      itemId: 'event-1',
      kind: 0,
      title: 'Soccer practice',
      icon: '⚽',
      iconOverride: null,
      color: '#00f',
      startsAt: `${today}T16:00:00Z`,
      endsAt: `${today}T17:00:00Z`,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      calendarId: 'cal-1',
      calendarName: 'Home',
    };

    const { fixture } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => [event]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Events today');
    expect(compiled.textContent).toContain('Soccer practice');
    expect(compiled.textContent).toContain('4:00');
  });

  it('links to the full child calendar', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const link = Array.from(compiled.querySelectorAll('a')).find(
      (anchor) => anchor.getAttribute('href') === '/child/calendar',
    );
    expect(link).toBeTruthy();
  });

  it('resolves the assignee name for a guardian pickup occurrence', async () => {
    const guardianList: GuardianSummary[] = [
      {
        id: 'guardian-1',
        name: { givenName: 'Gina', familyName: 'G' },
        guardianLinkId: 'link-1',
        kind: 0,
      },
    ];
    const occurrence: PickupOccurrence = {
      assignee: { kind: 0, guardianId: 'guardian-1' },
      date: today,
      slot: 0,
      time: '08:00:00',
      notes: '',
      assignedBy: 'guardian-1',
    };

    const { fixture } = await setup({
      guardians: { listMyGuardians: vi.fn(async () => guardianList) },
      pickups: { listSchedule: vi.fn(async () => [occurrence]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Drop-off');
    expect(compiled.textContent).toContain('Gina');
  });

  it('resolves the assignee name for a sibling pickup occurrence', async () => {
    const siblingList: SiblingSummary[] = [
      { id: 'sib-1', name: { givenName: 'Sam', familyName: 'S' } },
    ];
    const occurrence: PickupOccurrence = {
      assignee: { kind: 2, siblingChildId: 'sib-1' },
      date: today,
      slot: 1,
      time: null,
      notes: '',
      assignedBy: 'guardian-1',
    };

    const { fixture } = await setup({
      guardians: { listMySiblings: vi.fn(async () => siblingList) },
      pickups: { listSchedule: vi.fn(async () => [occurrence]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Sam');
  });

  describe('progress badge', () => {
    it('shows the default seedling badge while progress is still loading', async () => {
      const { fixture } = await setup({
        progress: { getMyProgress: vi.fn(() => new Promise<never>(() => undefined)) },
      });
      await settle(fixture);

      const badge = (fixture.nativeElement as HTMLElement).querySelector('app-progress-badge')!;
      expect(badge.querySelector('.text-3xl')?.textContent?.trim()).toBe('🌱');
      expect(rowText(badge)).toContain('0 stars');
    });

    it('shows the loaded progress', async () => {
      const { fixture } = await setup({
        progress: {
          getMyProgress: vi.fn(async () => ({
            totalStars: 7,
            unlockedMilestones: [],
            currentIcon: '🌳',
            nextGoalThreshold: 10,
            nextGoalIcon: '🏆',
            goalPosts: [],
          })),
        },
      });
      await settle(fixture);

      const badge = (fixture.nativeElement as HTMLElement).querySelector('app-progress-badge')!;
      expect(badge.querySelector('.text-3xl')?.textContent?.trim()).toBe('🌳');
      expect(rowText(badge)).toContain('7 stars');
      expect(rowText(badge)).toContain('Next: 🏆 at 10');
    });

    it('keeps the default badge without an error message when progress fails to load', async () => {
      const { fixture } = await setup({
        progress: { getMyProgress: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('app-progress-badge .text-3xl')?.textContent?.trim()).toBe(
        '🌱',
      );
      expect(compiled.textContent).not.toContain('Something went wrong');
    });

    it('re-reads progress after toggling a task', async () => {
      const getMyProgress = vi
        .fn()
        .mockResolvedValueOnce({
          totalStars: 1,
          unlockedMilestones: [],
          currentIcon: null,
          nextGoalThreshold: 5,
          nextGoalIcon: '🌱',
          goalPosts: [],
        })
        .mockResolvedValueOnce({
          totalStars: 2,
          unlockedMilestones: [],
          currentIcon: null,
          nextGoalThreshold: 5,
          nextGoalIcon: '🌱',
          goalPosts: [],
        });
      const { fixture } = await setup({
        progress: { getMyProgress },
        calendars: {
          listTodayOccurrences: vi.fn(async () => [
            occurrence({ itemId: 'task-1', title: 'Clean' }),
          ]),
          setTaskCompletion: vi.fn(async () => ({
            itemId: 'task-1',
            occurrenceDate: today,
            isCompleted: true,
          })),
        },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(rowText(compiled.querySelector('app-progress-badge')!)).toContain('1 stars');

      findButtonByAriaLabel(compiled, 'Mark done')!.click();
      await settle(fixture);

      expect(getMyProgress).toHaveBeenCalledTimes(2);
      expect(rowText(compiled.querySelector('app-progress-badge')!)).toContain('2 stars');
    });
  });

  describe('meals', () => {
    it('lists every planned meal in slot order with its translated slot label', async () => {
      const entries = [
        mealEntry({ slot: 3, mealId: 'meal-4', mealName: 'Apple' }),
        mealEntry({ slot: 1, mealId: 'meal-2', mealName: 'Sandwich' }),
        mealEntry({ slot: 2, mealId: 'meal-3', mealName: 'Pasta' }),
        mealEntry({ slot: 0, mealId: 'meal-1', mealName: 'Pancakes' }),
      ];
      const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => entries) } });
      await settle(fixture);

      const rows = sectionRows(fixture.nativeElement as HTMLElement, 'Meals today');
      expect(rows.map((row) => row.querySelector('span')?.textContent?.trim())).toEqual([
        'Breakfast',
        'Lunch',
        'Dinner',
        'Snack',
      ]);
      expect(rows.map((row) => rowText(row.querySelectorAll('span')[1]))).toEqual([
        '🥞 Pancakes',
        '🥞 Sandwich',
        '🥞 Pasta',
        '🥞 Apple',
      ]);
    });

    it("disables the meal's star buttons while a rating is saving and re-enables them afterwards", async () => {
      const pending = deferred<Meal>();
      const rateMeal = vi.fn(() => pending.promise);
      const entries = [
        mealEntry({ slot: 0 }),
        mealEntry({ slot: 1, mealId: 'meal-2', mealName: 'Soup' }),
      ];
      const { fixture } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => entries), rateMeal },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const [breakfast, lunch] = sectionRows(compiled, 'Meals today');
      const breakfastStars = () =>
        Array.from(breakfast.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'));
      const lunchStars = () =>
        Array.from(lunch.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'));

      breakfastStars()[1].click();
      fixture.detectChanges();

      expect(breakfastStars().every((button) => button.disabled)).toBe(true);
      expect(lunchStars().some((button) => button.disabled)).toBe(false);

      pending.resolve(
        ratedMeal([{ childId: 'child-1', stars: 2, comment: '', ratedAt: '2026-01-01T00:00:00Z' }]),
      );
      await settle(fixture);

      expect(breakfastStars().some((button) => button.disabled)).toBe(false);
    });

    it('shows the rate error when rating fails, re-enables the stars, and clears the error on the next rating', async () => {
      const rateMeal = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockResolvedValueOnce(
          ratedMeal([
            { childId: 'child-1', stars: 3, comment: '', ratedAt: '2026-01-01T00:00:00Z' },
          ]),
        );
      const { fixture } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => [mealEntry()]), rateMeal },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const stars = () =>
        Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'));

      stars()[2].click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to save your rating. Try again.');
      expect(stars().some((button) => button.disabled)).toBe(false);
      expect(stars().some((button) => button.classList.contains('text-amber-400'))).toBe(false);

      stars()[2].click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to save your rating. Try again.');
      expect(stars().map((button) => button.classList.contains('text-amber-400'))).toEqual([
        true,
        true,
        true,
        false,
        false,
      ]);
    });

    it("shows only the current child's rating and leaves other meals untouched", async () => {
      const rateMeal = vi.fn(async () =>
        ratedMeal([
          { childId: 'sibling-1', stars: 1, comment: 'Yuck', ratedAt: '2026-01-01T00:00:00Z' },
          { childId: 'child-1', stars: 4, comment: '', ratedAt: '2026-01-01T00:00:00Z' },
        ]),
      );
      const entries = [
        mealEntry({ slot: 0 }),
        mealEntry({ slot: 1, mealId: 'meal-2', mealName: 'Soup' }),
      ];
      const { fixture } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => entries), rateMeal },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const [breakfast, lunch] = sectionRows(compiled, 'Meals today');
      const amber = (row: HTMLElement) =>
        Array.from(row.querySelectorAll('button[aria-label^="Rate"]')).map((button) =>
          button.classList.contains('text-amber-400'),
        );

      breakfast.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]')[3].click();
      await settle(fixture);

      expect(amber(breakfast)).toEqual([true, true, true, true, false]);
      expect(compiled.textContent).not.toContain('Yuck');
      expect(amber(lunch)).toEqual([false, false, false, false, false]);
    });

    it('starts an empty note for an unrated meal and saves the trimmed comment', async () => {
      const rateMeal = vi.fn(async () =>
        ratedMeal([
          { childId: 'child-1', stars: 5, comment: 'Yummy!', ratedAt: '2026-01-01T00:00:00Z' },
        ]),
      );
      const { fixture } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => [mealEntry()]), rateMeal },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Add a note')!.click();
      fixture.detectChanges();

      const textarea = compiled.querySelector<HTMLTextAreaElement>('textarea')!;
      expect(textarea.value).toBe('');

      textarea.value = '  Yummy!  ';
      textarea.dispatchEvent(new Event('input'));
      findButtonByText(compiled, 'Save')!.click();
      await settle(fixture);

      expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-1', 5, 'Yummy!');
    });

    it('saves a whitespace-only note as no comment, keeping the existing star count', async () => {
      const rateMeal = vi.fn(async () =>
        ratedMeal([{ childId: 'child-1', stars: 2, comment: '', ratedAt: '2026-01-01T00:00:00Z' }]),
      );
      const entry = mealEntry({
        rating: { stars: 2, comment: 'Old note', ratedAt: '2026-01-01T00:00:00Z' },
      });
      const { fixture } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => [entry]), rateMeal },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Edit note')!.click();
      fixture.detectChanges();

      const textarea = compiled.querySelector<HTMLTextAreaElement>('textarea')!;
      expect(textarea.value).toBe('Old note');

      textarea.value = '   ';
      textarea.dispatchEvent(new Event('input'));
      findButtonByText(compiled, 'Save')!.click();
      await settle(fixture);

      expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-1', 2, '');
    });
  });

  describe('medicine', () => {
    it('lists doses sorted by time', async () => {
      const doses = [
        doseOccurrence({ medicineId: 'med-2', name: 'Evening', time: '19:00:00' }),
        doseOccurrence({ medicineId: 'med-1', name: 'Morning', time: '08:30:00' }),
      ];
      const { fixture } = await setup({ medicines: { listDoses: vi.fn(async () => doses) } });
      await settle(fixture);

      const rows = sectionRows(fixture.nativeElement as HTMLElement, 'Medicine today');
      expect(
        rows.map((row) => row.querySelector('span')?.textContent?.replace(/\s+/g, ' ').trim()),
      ).toEqual(['💊 Morning 08:30', '💊 Evening 19:00']);
    });

    it('disables only the saving dose while in flight, updates only that dose, and re-enables afterwards', async () => {
      const doses = [
        doseOccurrence({ medicineId: 'med-1', name: 'Morning' }),
        doseOccurrence({ medicineId: 'med-2', name: 'Other' }),
      ];
      const pending = deferred<MedicineDoseOccurrence>();
      const setDoseStatus = vi.fn(() => pending.promise);
      const { fixture } = await setup({
        medicines: { listDoses: vi.fn(async () => doses), setDoseStatus },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const rows = () => sectionRows(compiled, 'Medicine today');
      const buttons = (index: number) => Array.from(rows()[index].querySelectorAll('button'));

      findButtonByText(rows()[0], 'Skip')!.click();
      await settle(fixture);

      expect(setDoseStatus).toHaveBeenCalledWith('child-1', 'med-1', today, '09:00:00', 2);
      expect(buttons(0).map((button) => button.disabled)).toEqual([true, true]);
      expect(buttons(1).map((button) => button.disabled)).toEqual([false, false]);

      pending.resolve({ ...doses[0], status: 2 });
      await settle(fixture);

      expect(rowText(rows()[0])).toContain('Skipped');
      expect(buttons(0).map((button) => [button.textContent?.trim(), button.disabled])).toEqual([
        ['Undo', false],
      ]);
      expect(buttons(1).map((button) => button.textContent?.trim())).toEqual(['Taken', 'Skip']);
    });

    it('shows the error message and re-enables the dose when saving its status fails', async () => {
      const setDoseStatus = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({
        medicines: { listDoses: vi.fn(async () => [doseOccurrence()]), setDoseStatus },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Taken')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Something went wrong. Try again in a bit.');
      expect(findButtonByText(compiled, 'Taken')!.disabled).toBe(false);
      expect(findButtonByText(compiled, 'Skip')!.disabled).toBe(false);
    });
  });

  describe('tasks', () => {
    const taskTitles = (compiled: HTMLElement) =>
      sectionRows(compiled, 'Tasks today').map((row) => rowText(row));

    it('lists only tasks, those with a due time first in due order, then undated ones in their original order', async () => {
      const occurrences = [
        occurrence({ itemId: 't-a', title: 'Alpha', dueAt: null }),
        occurrence({ itemId: 't-b', title: 'Bravo', dueAt: `${today}T10:00:00Z` }),
        occurrence({
          itemId: 'e-1',
          kind: 0,
          title: 'Party',
          icon: '🎉',
          startsAt: `${today}T15:00:00Z`,
          endsAt: `${today}T16:00:00Z`,
        }),
        occurrence({ itemId: 't-c', title: 'Charlie', dueAt: `${today}T09:00:00Z` }),
        occurrence({ itemId: 't-d', title: 'Delta', dueAt: null }),
        occurrence({ itemId: 't-e', title: 'Echo', dueAt: `${today}T11:00:00Z` }),
        occurrence({ itemId: 't-f', title: 'Foxtrot', dueAt: null }),
      ];
      const { fixture } = await setup({
        calendars: { listTodayOccurrences: vi.fn(async () => occurrences) },
      });
      await settle(fixture);

      expect(taskTitles(fixture.nativeElement as HTMLElement)).toEqual([
        '🧹 Charlie',
        '🧹 Bravo',
        '🧹 Echo',
        '🧹 Alpha',
        '🧹 Delta',
        '🧹 Foxtrot',
      ]);
    });

    it('disables only the saving task while in flight and re-enables it afterwards', async () => {
      const pending = deferred<TaskCompletion>();
      const setTaskCompletion = vi.fn(() => pending.promise);
      const occurrences = [
        occurrence({ itemId: 't-1', title: 'One' }),
        occurrence({ itemId: 't-2', title: 'Two' }),
      ];
      const { fixture } = await setup({
        calendars: { listTodayOccurrences: vi.fn(async () => occurrences), setTaskCompletion },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const toggles = () =>
        sectionRows(compiled, 'Tasks today').map((row) => row.querySelector('button')!);

      toggles()[0].click();
      fixture.detectChanges();

      expect(toggles().map((button) => button.disabled)).toEqual([true, false]);

      pending.resolve({ itemId: 't-1', occurrenceDate: today, isCompleted: true });
      await settle(fixture);

      expect(toggles().map((button) => button.disabled)).toEqual([false, false]);
      expect(toggles().map((button) => button.getAttribute('aria-label'))).toEqual([
        'Mark not done',
        'Mark done',
      ]);
    });

    it('shows the error message and re-enables the task when toggling fails', async () => {
      const setTaskCompletion = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({
        calendars: { listTodayOccurrences: vi.fn(async () => [occurrence()]), setTaskCompletion },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByAriaLabel(compiled, 'Mark done')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Something went wrong. Try again in a bit.');
      const toggle = findButtonByAriaLabel(compiled, 'Mark done')!;
      expect(toggle.disabled).toBe(false);
    });
  });

  describe('events', () => {
    const nowMs = Date.parse(`${today}T12:00:00Z`);
    const at = (time: string) => `${today}T${time}Z`;

    async function setupEvents(occurrences: CalendarOccurrence[]) {
      vi.spyOn(Date, 'now').mockReturnValue(nowMs);
      const context = await setup({
        calendars: { listTodayOccurrences: vi.fn(async () => occurrences) },
      });
      await settle(context.fixture);
      return context;
    }

    function eventState(row: HTMLLIElement) {
      const label = row.querySelector('span')!;
      return {
        title: rowText(label),
        isPast: label.classList.contains('line-through'),
        background: row.style.background,
      };
    }

    // jsdom normalises the background shorthand, adding commas to rgb() and an explicit background-color.
    const gradient = (percent: number) =>
      `linear-gradient(to right, rgb(203, 213, 225) ${percent}%, transparent ${percent}%) transparent`;

    it('lists only events, timed ones first by start, then untimed ones in their original order', async () => {
      const { fixture } = await setupEvents([
        occurrence({ itemId: 'e-a', kind: 0, title: 'Alpha', icon: '⚽', isAllDay: true }),
        occurrence({
          itemId: 'e-b',
          kind: 0,
          title: 'Bravo',
          icon: '⚽',
          startsAt: at('18:00:00'),
          endsAt: at('19:00:00'),
        }),
        occurrence({ itemId: 't-1', kind: 1, title: 'Chore', dueAt: at('08:00:00') }),
        occurrence({
          itemId: 'e-c',
          kind: 0,
          title: 'Charlie',
          icon: '⚽',
          startsAt: at('14:00:00'),
          endsAt: at('15:00:00'),
        }),
        occurrence({ itemId: 'e-d', kind: 0, title: 'Delta', icon: '⚽', isAllDay: true }),
        occurrence({
          itemId: 'e-e',
          kind: 0,
          title: 'Echo',
          icon: '⚽',
          startsAt: at('16:00:00'),
          endsAt: at('17:00:00'),
        }),
        occurrence({ itemId: 'e-f', kind: 0, title: 'Foxtrot', icon: '⚽', isAllDay: true }),
      ]);

      const titles = sectionRows(fixture.nativeElement as HTMLElement, 'Events today').map(
        (row) => rowText(row).split(' ')[1],
      );
      expect(titles).toEqual(['Charlie', 'Echo', 'Bravo', 'Alpha', 'Delta', 'Foxtrot']);
    });

    it('marks past, ongoing, and upcoming events from the current time', async () => {
      const { fixture } = await setupEvents([
        occurrence({
          itemId: 'past',
          kind: 0,
          title: 'Past',
          icon: '⚽',
          startsAt: at('09:00:00'),
          endsAt: at('10:00:00'),
        }),
        occurrence({
          itemId: 'ends-now',
          kind: 0,
          title: 'EndsNow',
          icon: '⚽',
          startsAt: at('11:00:00'),
          endsAt: at('12:00:00'),
        }),
        occurrence({
          itemId: 'ongoing',
          kind: 0,
          title: 'Ongoing',
          icon: '⚽',
          startsAt: at('11:00:00'),
          endsAt: at('15:00:00'),
        }),
        occurrence({
          itemId: 'starts-now',
          kind: 0,
          title: 'StartsNow',
          icon: '⚽',
          startsAt: at('12:00:00'),
          endsAt: at('13:00:00'),
        }),
        occurrence({
          itemId: 'upcoming',
          kind: 0,
          title: 'Upcoming',
          icon: '⚽',
          startsAt: at('12:00:01'),
          endsAt: at('13:00:00'),
        }),
        occurrence({
          itemId: 'instant',
          kind: 0,
          title: 'Instant',
          icon: '⚽',
          startsAt: at('12:00:00'),
          endsAt: null,
        }),
        occurrence({
          itemId: 'all-day',
          kind: 0,
          title: 'AllDay',
          icon: '⚽',
          isAllDay: true,
          startsAt: at('00:00:00'),
          endsAt: at('23:59:00'),
        }),
        occurrence({ itemId: 'untimed', kind: 0, title: 'Untimed', icon: '⚽' }),
      ]);

      const states = sectionRows(fixture.nativeElement as HTMLElement, 'Events today').map(
        eventState,
      );
      expect(states).toEqual([
        { title: '⚽ AllDay', isPast: false, background: '' },
        { title: '⚽ Past ✓', isPast: true, background: '' },
        { title: '⚽ EndsNow ✓', isPast: true, background: '' },
        { title: '⚽ Ongoing', isPast: false, background: gradient(25) },
        { title: '⚽ StartsNow', isPast: false, background: gradient(0) },
        { title: '⚽ Instant ✓', isPast: true, background: '' },
        { title: '⚽ Upcoming', isPast: false, background: '' },
        { title: '⚽ Untimed', isPast: false, background: '' },
      ]);
    });

    it('refreshes the ongoing progress every minute and stops the timer on destroy', async () => {
      const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');
      const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');
      const { fixture } = await setupEvents([
        occurrence({
          itemId: 'ongoing',
          kind: 0,
          title: 'Ongoing',
          icon: '⚽',
          startsAt: at('11:00:00'),
          endsAt: at('13:00:00'),
        }),
      ]);

      const compiled = fixture.nativeElement as HTMLElement;
      const row = () => sectionRows(compiled, 'Events today')[0];
      expect(row().style.background).toBe(gradient(50));

      const callIndex = setIntervalSpy.mock.calls.findIndex(([, delay]) => delay === 60_000);
      expect(callIndex).toBeGreaterThanOrEqual(0);
      const tick = setIntervalSpy.mock.calls[callIndex][0] as () => void;
      const intervalId = setIntervalSpy.mock.results[callIndex].value;

      vi.mocked(Date.now).mockReturnValue(Date.parse(at('12:30:00')));
      tick();
      fixture.detectChanges();
      expect(row().style.background).toBe(gradient(75));

      vi.mocked(Date.now).mockReturnValue(Date.parse(at('13:00:00')));
      tick();
      fixture.detectChanges();
      expect(eventState(row())).toEqual({ title: '⚽ Ongoing ✓', isPast: true, background: '' });

      fixture.destroy();
      expect(clearIntervalSpy).toHaveBeenCalledWith(intervalId);
    });
  });

  describe('pickup assignee names', () => {
    function pickup(overrides: Partial<PickupOccurrence>): PickupOccurrence {
      return {
        assignee: { kind: 0, guardianId: 'guardian-1' },
        date: today,
        slot: 0,
        time: null,
        notes: '',
        assignedBy: 'guardian-1',
        ...overrides,
      };
    }

    const guardianList: GuardianSummary[] = [
      {
        id: 'guardian-1',
        name: { givenName: 'Gina', familyName: 'G' },
        guardianLinkId: 'link-1',
        kind: 0,
      },
      {
        id: 'guardian-2',
        name: { givenName: 'Gus', familyName: 'G' },
        guardianLinkId: 'link-2',
        kind: 0,
      },
    ];
    const siblingList: SiblingSummary[] = [
      { id: 'sib-1', name: { givenName: 'Sam', familyName: 'S' } },
      { id: 'sib-2', name: { givenName: 'Sue', familyName: 'S' } },
    ];

    it('picks the matching guardian or sibling by id', async () => {
      const { fixture } = await setup({
        guardians: {
          listMyGuardians: vi.fn(async () => guardianList),
          listMySiblings: vi.fn(async () => siblingList),
        },
        pickups: {
          listSchedule: vi.fn(async () => [
            pickup({ assignee: { kind: 0, guardianId: 'guardian-2' }, slot: 0 }),
            pickup({ assignee: { kind: 2, siblingChildId: 'sib-2' }, slot: 1 }),
          ]),
        },
      });
      await settle(fixture);

      const rows = sectionRows(
        fixture.nativeElement as HTMLElement,
        'Today’s pickup & drop-off',
      ).map(rowText);
      expect(rows).toEqual(['Drop-off👤 Gus', 'Pickup🧒 Sue']);
    });

    it('falls back to the generic label when the guardian or sibling is unknown', async () => {
      const { fixture } = await setup({
        guardians: {
          listMyGuardians: vi.fn(async () => guardianList),
          listMySiblings: vi.fn(async () => Promise.reject(new Error('boom'))),
        },
        pickups: {
          listSchedule: vi.fn(async () => [
            pickup({ assignee: { kind: 0, guardianId: 'guardian-9' }, slot: 0 }),
            pickup({ assignee: { kind: 2, siblingChildId: 'sib-1' }, slot: 1 }),
          ]),
        },
      });
      await settle(fixture);

      const rows = sectionRows(
        fixture.nativeElement as HTMLElement,
        'Today’s pickup & drop-off',
      ).map(rowText);
      expect(rows).toEqual(['Drop-off👤 A guardian', 'Pickup🧒 A sibling']);
    });
  });
});
