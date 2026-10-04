import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { addDaysIso, parseIsoDate, todayIsoDate } from '../../../core/date-utils';
import { TranslationService } from '../../../core/i18n/translation.service';
import { Meal, MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildMealplan } from './child-mealplan';

describe('ChildMealplan', () => {
  const currentUser: CurrentUser = {
    id: 'child-1',
    email: { value: 'kid@buddy.test', isVerified: true },
    userName: 'kid',
    name: { givenName: 'Kim', familyName: 'Kid' },
    timeZoneId: 'UTC',
    language: 'en',
  };

  function entryAt(
    date: string,
    slot: MealSlot,
    mealId: string,
    overrides: Partial<MealPlanEntry> = {},
  ): MealPlanEntry {
    return {
      date,
      slot,
      mealId,
      mealName: `Meal ${mealId}`,
      icon: '🍽️',
      color: '#f00',
      rating: null,
      notes: '',
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides,
    };
  }

  // Keys every returned plan to the actual date range the component asked for, so tests don't
  // need to duplicate the component's private anchor-date math to know which dates are in view.
  function rangeKeyedMealplansStub(
    overrides: Partial<MealplansService> = {},
  ): Partial<MealplansService> {
    return {
      listMealPlan: vi.fn(async (_scope, from: string, to: string) => [
        entryAt(from, 0, `meal-from-${from}`),
        entryAt(to, 1, `meal-to-${to}`),
      ]),
      rateMeal: vi.fn(),
      ...overrides,
    };
  }

  interface Stubs {
    users?: Partial<UsersService>;
    mealplans?: Partial<MealplansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => currentUser),
      ...stubs.users,
    };
    const mealplansStub: Partial<MealplansService> = {
      listMealPlan: vi.fn(async () => []),
      rateMeal: vi.fn(),
      ...stubs.mealplans,
    };

    await TestBed.configureTestingModule({
      imports: [ChildMealplan],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: usersStub },
        { provide: MealplansService, useValue: mealplansStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildMealplan);

    return { fixture, users: usersStub, mealplans: mealplansStub };
  }

  // The app runs zoneless, and none of these stubbed services register a PendingTasks entry, so
  // fixture.whenStable() resolves immediately without actually waiting for them. A macrotask
  // flush lets every already-scheduled microtask in the mocked promise chains drain first.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function mealWithRatings(ratings: Meal['ratings']): Meal {
    return {
      id: 'meal-from',
      name: 'Meal meal-from',
      description: '',
      icon: '🍽️',
      color: '#f00',
      isArchived: false,
      ratings,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
    };
  }

  function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (reason: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
  }

  it('shows a loading message while the plan is loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading your meals…');
  });

  it('shows the empty state when no meals are planned', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No meals planned for this week');
  });

  it('shows the translated error message when loading the plan fails', async () => {
    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain(
      'Something went wrong loading your meals. Try again in a bit.',
    );
  });

  it('renders planned meals with rating controls for days up to today', async () => {
    const { fixture } = await setup({ mealplans: rangeKeyedMealplansStub() });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    // The default view opens on the past week, so both the range's first and last day are
    // today or earlier and should offer rating controls.
    expect(compiled.querySelectorAll('button[aria-label^="Rate"]').length).toBeGreaterThan(0);
  });

  it('hides rating controls for a meal planned on a future day', async () => {
    const { fixture, mealplans } = await setup({ mealplans: rangeKeyedMealplansStub() });
    await settle(fixture);

    findButtonByText(fixture.nativeElement as HTMLElement, 'Next week →')?.click();
    await settle(fixture);

    const [, from, to] = (mealplans.listMealPlan as ReturnType<typeof vi.fn>).mock.calls.at(-1)!;
    expect(to > from).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    // The range's last day is now in the future -- only the (still not-in-the-future) first
    // day's row should offer stars.
    expect(compiled.querySelectorAll('button[aria-label^="Rate"]')).toHaveLength(5);
  });

  it('moves the visible week forward and backward', async () => {
    const { fixture, mealplans } = await setup({ mealplans: rangeKeyedMealplansStub() });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const [, initialFrom] = (mealplans.listMealPlan as ReturnType<typeof vi.fn>).mock.calls[0];

    findButtonByText(compiled, 'Next week →')?.click();
    await settle(fixture);
    const [, forwardFrom] = (mealplans.listMealPlan as ReturnType<typeof vi.fn>).mock.calls.at(-1)!;
    expect(forwardFrom > initialFrom).toBe(true);

    findButtonByText(compiled, '← Previous week')?.click();
    await settle(fixture);
    const [, backFrom] = (mealplans.listMealPlan as ReturnType<typeof vi.fn>).mock.calls.at(-1)!;
    expect(backFrom).toBe(initialFrom);
  });

  it('rates a meal and reflects the rating on every entry sharing that meal', async () => {
    const rateMeal = vi.fn(async () => ({
      id: 'meal-shared',
      name: 'Pancakes',
      description: '',
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings: [{ childId: 'child-1', stars: 3, comment: '', ratedAt: '2026-01-01T00:00:00Z' }],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
    }));

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [
          entryAt(from, 0, 'meal-shared'),
          entryAt(from, 1, 'meal-shared'),
        ]),
        rateMeal,
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButtons = Array.from(
      compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'),
    );
    expect(starButtons).toHaveLength(10);

    starButtons[2].click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-shared', 3, '');
    expect(starButtons[2].classList.contains('text-amber-400')).toBe(true);
    expect(starButtons[7].classList.contains('text-amber-400')).toBe(true);
  });

  it('adds a note to a meal', async () => {
    const rateMeal = vi.fn(async () => ({
      id: 'meal-from',
      name: 'Meal meal-from',
      description: '',
      icon: '🍽️',
      color: '#f00',
      isArchived: false,
      ratings: [
        { childId: 'child-1', stars: 5, comment: 'So good', ratedAt: '2026-01-01T00:00:00Z' },
      ],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
    }));

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]),
        rateMeal,
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a note')?.click();
    fixture.detectChanges();

    const textarea = compiled.querySelector<HTMLTextAreaElement>('textarea')!;
    textarea.value = 'So good';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    findButtonByText(compiled, 'Save')?.click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-from', 5, 'So good');
    expect(compiled.querySelector('textarea')).toBeFalsy();
    expect(compiled.textContent).toContain('So good');
  });

  it('shows a translated error and re-enables the stars when rating fails', async () => {
    const rateMeal = vi.fn(async () => Promise.reject(new Error('boom')));

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]),
        rateMeal,
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButton = compiled.querySelector<HTMLButtonElement>('button[aria-label^="Rate"]')!;
    starButton.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save your rating. Try again.');
    expect(starButton.disabled).toBe(false);
  });

  it('requests exactly the past seven days and labels each day and slot', async () => {
    const listMealPlan = vi.fn(async (_scope, from: string) => [
      entryAt(from, 0, 'meal-a'),
      entryAt(from, 1, 'meal-b'),
      entryAt(from, 2, 'meal-c'),
      entryAt(from, 3, 'meal-d'),
    ]);
    const { fixture } = await setup({ mealplans: { listMealPlan } });
    await settle(fixture);

    const expectedFrom = addDaysIso(todayIsoDate(), -7);
    expect(listMealPlan).toHaveBeenCalledExactlyOnceWith(
      { kind: 'family', childId: 'child-1' },
      expectedFrom,
      addDaysIso(todayIsoDate(), -1),
    );

    const compiled = fixture.nativeElement as HTMLElement;
    const locale = TestBed.inject(TranslationService).language();
    const expectedLabel = parseIsoDate(expectedFrom).toLocaleDateString(locale, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
    expect(
      Array.from(compiled.querySelectorAll('h2')).map((heading) => heading.textContent?.trim()),
    ).toEqual([expectedLabel]);

    const slotLabels = Array.from(compiled.querySelectorAll('li span.uppercase')).map((span) =>
      span.textContent?.trim(),
    );
    expect(slotLabels).toEqual(['Breakfast', 'Lunch', 'Dinner', 'Snack']);
  });

  it('disables the stars while a rating is saving and clears a previous rating error', async () => {
    const pending = deferred<Meal>();
    const rateMeal = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockReturnValueOnce(pending.promise);

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]),
        rateMeal,
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButtons = () =>
      Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'));

    starButtons()[0].click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to save your rating. Try again.');
    expect(starButtons().every((button) => !button.disabled)).toBe(true);

    starButtons()[3].click();
    fixture.detectChanges();
    expect(rateMeal).toHaveBeenLastCalledWith('child-1', 'meal-from', 4, '');
    expect(compiled.textContent).not.toContain('Unable to save your rating. Try again.');
    expect(starButtons().every((button) => button.disabled)).toBe(true);

    pending.resolve(
      mealWithRatings([
        { childId: 'child-1', stars: 4, comment: '', ratedAt: '2026-01-01T00:00:00Z' },
      ]),
    );
    await settle(fixture);
    expect(starButtons().every((button) => !button.disabled)).toBe(true);
    expect(compiled.textContent).not.toContain('Unable to save your rating. Try again.');
  });

  it("shows only the current child's rating when other children also rated the meal", async () => {
    const rateMeal = vi.fn(async () =>
      mealWithRatings([
        { childId: 'child-2', stars: 1, comment: 'Yuck', ratedAt: '2026-01-01T00:00:00Z' },
        { childId: 'child-1', stars: 4, comment: 'Tasty', ratedAt: '2026-01-01T00:00:00Z' },
      ]),
    );

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]),
        rateMeal,
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]')[3].click();
    await settle(fixture);

    const lit = Array.from(
      compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'),
    ).map((button) => button.classList.contains('text-amber-400'));
    expect(lit).toEqual([true, true, true, true, false]);
    expect(compiled.textContent).toContain('Tasty');
    expect(compiled.textContent).not.toContain('Yuck');
  });

  it('opens an empty note for an unrated meal and saves the trimmed comment', async () => {
    const rateMeal = vi.fn(async () =>
      mealWithRatings([
        { childId: 'child-1', stars: 5, comment: 'Yum', ratedAt: '2026-01-01T00:00:00Z' },
      ]),
    );

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]),
        rateMeal,
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a note')?.click();
    fixture.detectChanges();

    const textarea = compiled.querySelector<HTMLTextAreaElement>('textarea')!;
    expect(textarea.value).toBe('');

    textarea.value = '  Yum  ';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    findButtonByText(compiled, 'Save')?.click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledExactlyOnceWith('child-1', 'meal-from', 5, 'Yum');
  });

  describe('a rating that resolves after switching week', () => {
    async function rateThenSwitchWeek(nextWeek: () => Promise<MealPlanEntry[]>) {
      const pendingRating = deferred<Meal>();
      const listMealPlan = vi
        .fn()
        .mockImplementationOnce(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')])
        .mockImplementationOnce(nextWeek);
      const rateMeal = vi.fn(() => pendingRating.promise);
      const { fixture } = await setup({ mealplans: { listMealPlan, rateMeal } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      compiled.querySelector<HTMLButtonElement>('button[aria-label^="Rate"]')!.click();
      findButtonByText(compiled, '← Previous week')?.click();
      await settle(fixture);

      return { fixture, compiled, pendingRating };
    }

    const savedRating = () =>
      mealWithRatings([
        { childId: 'child-1', stars: 1, comment: '', ratedAt: '2026-01-01T00:00:00Z' },
      ]);

    it('leaves the new week loading and then shows it', async () => {
      const nextWeek = deferred<MealPlanEntry[]>();
      const { fixture, compiled, pendingRating } = await rateThenSwitchWeek(() => nextWeek.promise);

      pendingRating.resolve(savedRating());
      await settle(fixture);
      expect(compiled.textContent).toContain('Loading your meals…');

      nextWeek.resolve([entryAt(addDaysIso(todayIsoDate(), -14), 0, 'meal-older')]);
      await settle(fixture);

      expect(compiled.textContent).toContain('Meal meal-older');
      expect(compiled.textContent).not.toContain('Meal meal-from');
      expect(compiled.textContent).not.toContain('Unable to save your rating. Try again.');
    });

    it("keeps the new week's load error and shows no rating error", async () => {
      const { fixture, compiled, pendingRating } = await rateThenSwitchWeek(() =>
        Promise.reject(new Error('boom')),
      );
      expect(compiled.textContent).toContain(
        'Something went wrong loading your meals. Try again in a bit.',
      );

      pendingRating.resolve(savedRating());
      await settle(fixture);

      expect(compiled.textContent).toContain(
        'Something went wrong loading your meals. Try again in a bit.',
      );
      expect(compiled.textContent).not.toContain('Unable to save your rating. Try again.');
    });
  });

  it('clears a load error once a later week loads successfully', async () => {
    const listMealPlan = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockImplementation(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]);
    const { fixture } = await setup({ mealplans: { listMealPlan } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain(
      'Something went wrong loading your meals. Try again in a bit.',
    );

    findButtonByText(compiled, '← Previous week')?.click();
    await settle(fixture);

    expect(listMealPlan).toHaveBeenLastCalledWith(
      { kind: 'family', childId: 'child-1' },
      addDaysIso(todayIsoDate(), -14),
      addDaysIso(todayIsoDate(), -8),
    );
    expect(compiled.textContent).not.toContain(
      'Something went wrong loading your meals. Try again in a bit.',
    );
    expect(compiled.textContent).toContain('Meal meal-from');
  });
});
