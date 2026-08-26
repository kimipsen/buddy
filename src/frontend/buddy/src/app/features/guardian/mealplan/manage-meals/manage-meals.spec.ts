import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Meal, MealDetails, MealplanScope, MealplansService } from '../../../../core/mealplans.service';
import { ManageMeals } from './manage-meals';

describe('ManageMeals', () => {
  const familyScope: MealplanScope = { kind: 'family', childId: 'child-1' };
  const groupViewScope: MealplanScope = { kind: 'group', groupId: 'group-1', groupName: 'The Fam', accessTier: 3 };
  const groupManageScope: MealplanScope = { kind: 'group', groupId: 'group-1', groupName: 'The Fam', accessTier: 2 };

  function meal(overrides: Partial<Meal> = {}): Meal {
    return {
      id: 'meal-1',
      name: 'Pancakes',
      description: null,
      icon: '🥞',
      color: '#fff',
      isArchived: false,
      ratings: [],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      ...overrides
    };
  }

  interface SetupOptions {
    initialMeals?: Meal[];
    mealplans?: Partial<MealplansService>;
  }

  // Mirrors MealplansService's real signal-mutation semantics (listMeals replaces, createMeal
  // appends, archiveMeal removes) so the component -- which reads `mealplans.meals()` directly
  // rather than a local copy -- behaves under test the same way it does against the real service.
  async function setup(scope: MealplanScope, options: SetupOptions = {}) {
    const mealsState: WritableSignal<Meal[]> = signal(options.initialMeals ?? []);

    const mealplansStub: Partial<MealplansService> = {
      meals: mealsState.asReadonly(),
      listMeals: vi.fn(async () => mealsState()),
      createMeal: vi.fn(async (_scope: MealplanScope, request: MealDetails) => {
        const created = meal({ id: `meal-created-${mealsState().length + 1}`, ...request });
        mealsState.update((current) => [...current, created]);
        return created;
      }),
      archiveMeal: vi.fn(async (_scope: MealplanScope, mealId: string) => {
        mealsState.update((current) => current.filter((m) => m.id !== mealId));
      }),
      updateMealDetails: vi.fn(),
      ...options.mealplans
    };

    await TestBed.configureTestingModule({
      imports: [ManageMeals],
      providers: [{ provide: MealplansService, useValue: mealplansStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageMeals);
    fixture.componentRef.setInput('scope', scope);

    return { fixture, mealplans: mealplansStub, mealsState };
  }

  // The stubbed MealplansService methods return plain (unregistered) Promises, so zoneless
  // ApplicationRef.whenStable() resolves immediately without waiting for them (see
  // docs/testing.md). A macrotask flush reliably drains any depth of chained awaits instead.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function nameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="mealName"]')!;
  }

  function descriptionInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="mealDescription"]')!;
  }

  function iconInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="mealIcon"]')!;
  }

  function colorInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="mealColor"]')!;
  }

  it('shows a loading message while meals are loading', async () => {
    const { fixture } = await setup(familyScope);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading meals…');
  });

  it('requests meals for the given scope on init', async () => {
    const { fixture, mealplans } = await setup(familyScope);
    await settle(fixture);

    expect(mealplans.listMeals).toHaveBeenCalledWith(familyScope);
  });

  it('shows the empty state once loading finishes with no meals', async () => {
    const { fixture } = await setup(familyScope);
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No meals yet. Add one below.');
  });

  it('shows the translated error message when loading meals fails', async () => {
    const { fixture } = await setup(familyScope, { mealplans: { listMeals: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load meals.');
  });

  it('renders each meal from the shared service signal, including its icon and name', async () => {
    const meals = [meal({ id: 'meal-1', name: 'Pancakes', icon: '🥞' }), meal({ id: 'meal-2', name: 'Toast', icon: '🍞' })];
    const { fixture } = await setup(familyScope, { initialMeals: meals });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pancakes');
    expect(compiled.textContent).toContain('🥞');
    expect(compiled.textContent).toContain('Toast');
    expect(compiled.textContent).toContain('🍞');
  });

  it('reads meals straight from the shared signal rather than from listMeals’ own return value', async () => {
    // Regression pin for the component's documented behaviour: it filters mealplans.meals()
    // directly, so a meal already present in shared state (e.g. created elsewhere on the same
    // page) renders even though this component's own listMeals call resolves with nothing new.
    const preseeded = [meal({ id: 'preseeded', name: 'Already there' })];
    const { fixture } = await setup(familyScope, {
      initialMeals: preseeded,
      mealplans: { listMeals: vi.fn(async () => []) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Already there');
  });

  it('excludes archived meals from the displayed list', async () => {
    const meals = [meal({ id: 'meal-1', name: 'Active Meal', isArchived: false }), meal({ id: 'meal-2', name: 'Archived Meal', isArchived: true })];
    const { fixture } = await setup(familyScope, { initialMeals: meals });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Active Meal');
    expect(compiled.textContent).not.toContain('Archived Meal');
  });

  describe('pagination', () => {
    function mealsPage(count: number): Meal[] {
      return Array.from({ length: count }, (_, index) => meal({ id: `meal-${index}`, name: `Meal ${index}` }));
    }

    it('does not show pagination controls when there are 5 or fewer meals', async () => {
      const { fixture } = await setup(familyScope, { initialMeals: mealsPage(5) });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(findButtonByText(compiled, 'Next')).toBeUndefined();
    });

    it('paginates at 5 meals per page and navigates forward/backward', async () => {
      const { fixture } = await setup(familyScope, { initialMeals: mealsPage(7) });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Page 1 of 2');
      expect(compiled.textContent).toContain('Meal 0');
      expect(compiled.textContent).not.toContain('Meal 5');

      const previousButton = findButtonByText(compiled, 'Previous')!;
      expect(previousButton.disabled).toBe(true);

      findButtonByText(compiled, 'Next')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Page 2 of 2');
      expect(compiled.textContent).toContain('Meal 5');
      expect(compiled.textContent).toContain('Meal 6');
      expect(compiled.textContent).not.toContain('Meal 0');
      expect(findButtonByText(compiled, 'Next')!.disabled).toBe(true);

      findButtonByText(compiled, 'Previous')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Page 1 of 2');
      expect(compiled.textContent).toContain('Meal 0');
    });
  });

  describe('create meal form', () => {
    it('disables the submit button until a name is entered', async () => {
      const { fixture } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const submit = findButtonByText(compiled, 'Add meal')!;
      expect(submit.disabled).toBe(true);

      setInputValue(nameInput(compiled), 'Waffles');
      fixture.detectChanges();

      expect(submit.disabled).toBe(false);
    });

    it('disables the submit button when the icon field is cleared', async () => {
      const { fixture } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Waffles');
      setInputValue(iconInput(compiled), '   ');
      fixture.detectChanges();

      expect(findButtonByText(compiled, 'Add meal')!.disabled).toBe(true);
    });

    it('does not call createMeal when submitted with only whitespace in the name', async () => {
      const { fixture, mealplans } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), '   ');
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(mealplans.createMeal).not.toHaveBeenCalled();
    });

    it('submits trimmed field values, including a null description when left blank', async () => {
      const { fixture, mealplans } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), '  Waffles  ');
      setInputValue(iconInput(compiled), ' 🧇 ');
      setInputValue(colorInput(compiled), '#123456');
      fixture.detectChanges();

      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(mealplans.createMeal).toHaveBeenCalledWith(familyScope, {
        name: 'Waffles',
        description: null,
        icon: '🧇',
        color: '#123456'
      });
    });

    it('submits a trimmed description when one is provided', async () => {
      const { fixture, mealplans } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Waffles');
      setInputValue(descriptionInput(compiled), '  Crispy and golden  ');
      fixture.detectChanges();

      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(mealplans.createMeal).toHaveBeenCalledWith(
        familyScope,
        expect.objectContaining({ name: 'Waffles', description: 'Crispy and golden' })
      );
    });

    it('resets the form and shows the new meal after a successful create', async () => {
      const { fixture } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Waffles');
      setInputValue(descriptionInput(compiled), 'Crispy');
      fixture.detectChanges();

      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(nameInput(compiled).value).toBe('');
      expect(descriptionInput(compiled).value).toBe('');
      expect(compiled.textContent).toContain('Waffles');
    });

    it('jumps to the newly-added last page once the created meal pushes the count past a page boundary', async () => {
      const { fixture } = await setup(familyScope, {
        initialMeals: Array.from({ length: 5 }, (_, index) => meal({ id: `meal-${index}`, name: `Meal ${index}` }))
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).not.toContain('Page');

      setInputValue(nameInput(compiled), 'Waffles');
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(compiled.textContent).toContain('Page 2 of 2');
      expect(compiled.textContent).toContain('Waffles');
    });

    it('shows the translated error message and keeps the entered name when create fails', async () => {
      const { fixture } = await setup(familyScope, { mealplans: { createMeal: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Waffles');
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to create the meal.');
      expect(nameInput(compiled).value).toBe('Waffles');
    });

    it('re-enables the submit button after a failed create', async () => {
      const { fixture } = await setup(familyScope, { mealplans: { createMeal: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Waffles');
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(findButtonByText(compiled, 'Add meal')!.disabled).toBe(false);
    });
  });

  describe('archiving a meal', () => {
    it('archives the clicked meal and removes it from the list once the request resolves', async () => {
      const meals = [meal({ id: 'meal-1', name: 'Pancakes' }), meal({ id: 'meal-2', name: 'Toast' })];
      const { fixture, mealplans } = await setup(familyScope, { initialMeals: meals });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Pancakes');

      findButtonByText(compiled, 'Archive')!.click();
      await settle(fixture);

      expect(mealplans.archiveMeal).toHaveBeenCalledWith(familyScope, 'meal-1');
      expect(compiled.textContent).not.toContain('Pancakes');
      expect(compiled.textContent).toContain('Toast');
    });

    it('disables only the archive button for the meal being archived, and re-enables it afterwards', async () => {
      let resolveArchive!: () => void;
      const archiveMeal = vi.fn(() => new Promise<void>((resolve) => (resolveArchive = resolve)));
      const meals = [meal({ id: 'meal-1', name: 'Pancakes' }), meal({ id: 'meal-2', name: 'Toast' })];
      const { fixture } = await setup(familyScope, { initialMeals: meals, mealplans: { archiveMeal } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const archiveButtons = () => Array.from(compiled.querySelectorAll('button')).filter((b) => b.textContent?.trim() === 'Archive');

      archiveButtons()[0].click();
      fixture.detectChanges();

      const [pancakesButton, toastButton] = archiveButtons();
      expect(pancakesButton.disabled).toBe(true);
      expect(toastButton.disabled).toBe(false);

      resolveArchive();
      await settle(fixture);

      const [pancakesButtonAfter] = archiveButtons();
      expect(pancakesButtonAfter.disabled).toBe(false);
    });

    it('shows the translated error message and keeps the meal in the list when archiving fails', async () => {
      const meals = [meal({ id: 'meal-1', name: 'Pancakes' })];
      const { fixture } = await setup(familyScope, {
        initialMeals: meals,
        mealplans: { archiveMeal: vi.fn(async () => Promise.reject(new Error('boom'))) }
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Archive')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to archive this meal.');
      expect(compiled.textContent).toContain('Pancakes');
    });
  });

  describe('read-only group access', () => {
    it('hides the archive button and create form, and shows a read-only notice, for a View-tier group scope', async () => {
      const meals = [meal({ id: 'meal-1', name: 'Pancakes' })];
      const { fixture } = await setup(groupViewScope, { initialMeals: meals });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Pancakes');
      expect(findButtonByText(compiled, 'Archive')).toBeUndefined();
      expect(compiled.querySelector('form')).toBeNull();
      expect(compiled.textContent).toContain("You have read-only access to this group's meal library.");
    });

    it('shows the create form and archive button for a Manage-tier group scope', async () => {
      const meals = [meal({ id: 'meal-1', name: 'Pancakes' })];
      const { fixture } = await setup(groupManageScope, { initialMeals: meals });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(findButtonByText(compiled, 'Archive')).toBeDefined();
      expect(compiled.querySelector('form')).not.toBeNull();
      expect(compiled.textContent).not.toContain('read-only access');
    });

    it('is never read-only for a family scope', async () => {
      const meals = [meal({ id: 'meal-1', name: 'Pancakes' })];
      const { fixture } = await setup(familyScope, { initialMeals: meals });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(findButtonByText(compiled, 'Archive')).toBeDefined();
      expect(compiled.textContent).not.toContain('read-only access');
    });
  });
});
