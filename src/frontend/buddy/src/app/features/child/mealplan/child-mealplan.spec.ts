import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildMealplan } from './child-mealplan';

describe('ChildMealplan', () => {
  const currentUser: CurrentUser = {
    id: 'child-1',
    email: { value: 'kid@buddy.test', isVerified: true },
    userName: 'kid',
    name: { givenName: 'Kim', familyName: 'Kid' },
    timeZoneId: 'UTC',
    language: 'en'
  };

  function entryAt(date: string, slot: MealSlot, mealId: string, overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date,
      slot,
      mealId,
      mealName: `Meal ${mealId}`,
      icon: '🍽️',
      color: '#f00',
      rating: null,
      notes: null,
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides
    };
  }

  // Keys every returned plan to the actual date range the component asked for, so tests don't
  // need to duplicate the component's private anchor-date math to know which dates are in view.
  function rangeKeyedMealplansStub(overrides: Partial<MealplansService> = {}): Partial<MealplansService> {
    return {
      listMealPlan: vi.fn(async (_scope, from: string, to: string) => [entryAt(from, 0, `meal-from-${from}`), entryAt(to, 1, `meal-to-${to}`)]),
      rateMeal: vi.fn(),
      ...overrides
    };
  }

  interface Stubs {
    users?: Partial<UsersService>;
    mealplans?: Partial<MealplansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const usersStub: Partial<UsersService> = { ensureCurrentUser: vi.fn(async () => currentUser), ...stubs.users };
    const mealplansStub: Partial<MealplansService> = { listMealPlan: vi.fn(async () => []), rateMeal: vi.fn(), ...stubs.mealplans };

    await TestBed.configureTestingModule({
      imports: [ChildMealplan],
      providers: [provideRouter([]), { provide: UsersService, useValue: usersStub }, { provide: MealplansService, useValue: mealplansStub }]
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

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
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
    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Something went wrong loading your meals. Try again in a bit.');
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
      description: null,
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings: [{ childId: 'child-1', stars: 3, comment: null, ratedAt: '2026-01-01T00:00:00Z' }],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1'
    }));

    const { fixture } = await setup({
      mealplans: {
        listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-shared'), entryAt(from, 1, 'meal-shared')]),
        rateMeal
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButtons = Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'));
    expect(starButtons).toHaveLength(10);

    starButtons[2].click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-shared', 3, null);
    expect(starButtons[2].classList.contains('text-amber-400')).toBe(true);
    expect(starButtons[7].classList.contains('text-amber-400')).toBe(true);
  });

  it('adds a note to a meal', async () => {
    const rateMeal = vi.fn(async () => ({
      id: 'meal-from',
      name: 'Meal meal-from',
      description: null,
      icon: '🍽️',
      color: '#f00',
      isArchived: false,
      ratings: [{ childId: 'child-1', stars: 5, comment: 'So good', ratedAt: '2026-01-01T00:00:00Z' }],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1'
    }));

    const { fixture } = await setup({
      mealplans: { listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]), rateMeal }
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
      mealplans: { listMealPlan: vi.fn(async (_scope, from: string) => [entryAt(from, 0, 'meal-from')]), rateMeal }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButton = compiled.querySelector<HTMLButtonElement>('button[aria-label^="Rate"]')!;
    starButton.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save your rating. Try again.');
    expect(starButton.disabled).toBe(false);
  });
});
