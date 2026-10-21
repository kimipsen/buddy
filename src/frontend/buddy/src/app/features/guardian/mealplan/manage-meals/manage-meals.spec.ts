import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  Meal,
  MealDetails,
  MealplanScope,
  MealplansService,
} from '../../../../core/mealplans.service';
import { MealieService } from '../../../../core/mealie.service';
import { ManageMeals } from './manage-meals';

describe('ManageMeals', () => {
  const familyScope: MealplanScope = { kind: 'family', childId: 'child-1' };
  const groupViewScope: MealplanScope = {
    kind: 'group',
    groupId: 'group-1',
    groupName: 'The Fam',
    accessTier: 'View',
  };
  const groupManageScope: MealplanScope = {
    kind: 'group',
    groupId: 'group-1',
    groupName: 'The Fam',
    accessTier: 'Manage',
  };

  function meal(overrides: Partial<Meal> = {}): Meal {
    return {
      id: 'meal-1',
      name: 'Pancakes',
      description: '',
      icon: '🥞',
      color: '#fff',
      isArchived: false,
      ratings: [],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      eatingOut: 'None',
      recipe: null,
      ...overrides,
    };
  }

  interface SetupOptions {
    initialMeals?: Meal[];
    mealplans?: Partial<MealplansService>;
    mealie?: Partial<MealieService>;
    mealieChildId?: string | null;
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
        const created = meal({
          id: `meal-created-${mealsState().length + 1}`,
          ...request,
          description: request.description ?? '',
        });
        mealsState.update((current) => [...current, created]);
        return created;
      }),
      archiveMeal: vi.fn(async (_scope: MealplanScope, mealId: string) => {
        mealsState.update((current) => current.filter((m) => m.id !== mealId));
      }),
      updateMealDetails: vi.fn(),
      ...options.mealplans,
    };

    // The recipe picker and panel are real children; they read Mealie through this stub.
    const mealieStub: Partial<MealieService> = {
      searchRecipes: vi.fn(async () => ({
        items: [
          {
            id: 'recipe-1',
            slug: 'lasagne',
            name: 'Lasagne al forno',
            description: '',
            totalTime: '',
            hasImage: false,
            linkedMealId: null,
          },
        ],
        page: 1,
        totalPages: 1,
        total: 1,
      })),
      getRecipe: vi.fn(async () => ({
        id: 'recipe-1',
        slug: 'lasagne',
        name: 'Lasagne al forno',
        description: '',
        totalTime: '',
        recipeYield: '',
        hasImage: false,
        ingredients: ['500 g minced beef'],
        steps: [],
        originalUrl: '',
        mealieUrl: 'https://mealie.example/g/holm/r/lasagne',
        linkedMealId: 'meal-1',
      })),
      ...options.mealie,
    };

    await TestBed.configureTestingModule({
      imports: [ManageMeals],
      providers: [
        { provide: MealplansService, useValue: mealplansStub },
        { provide: MealieService, useValue: mealieStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageMeals);
    fixture.componentRef.setInput('scope', scope);
    fixture.componentRef.setInput('mealieChildId', options.mealieChildId ?? null);

    return { fixture, mealplans: mealplansStub, mealie: mealieStub, mealsState };
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
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
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

  function colorSwatch(compiled: HTMLElement, color: string): HTMLButtonElement {
    return compiled.querySelector(`app-color-swatch-picker [role="radio"][aria-label="${color}"]`)!;
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
    const { fixture } = await setup(familyScope, {
      mealplans: { listMeals: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load meals.');
  });

  it('renders each meal from the shared service signal, including its icon and name', async () => {
    const meals = [
      meal({ id: 'meal-1', name: 'Pancakes', icon: '🥞' }),
      meal({ id: 'meal-2', name: 'Toast', icon: '🍞' }),
    ];
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
      mealplans: { listMeals: vi.fn(async () => []) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Already there');
  });

  it('excludes archived meals from the displayed list', async () => {
    const meals = [
      meal({ id: 'meal-1', name: 'Active Meal', isArchived: false }),
      meal({ id: 'meal-2', name: 'Archived Meal', isArchived: true }),
    ];
    const { fixture } = await setup(familyScope, { initialMeals: meals });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Active Meal');
    expect(compiled.textContent).not.toContain('Archived Meal');
  });

  describe('pagination', () => {
    function mealsPage(count: number): Meal[] {
      return Array.from({ length: count }, (_, index) =>
        meal({ id: `meal-${index}`, name: `Meal ${index}` }),
      );
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
      colorSwatch(compiled, '#0ea5e9').click();
      fixture.detectChanges();

      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(mealplans.createMeal).toHaveBeenCalledWith(familyScope, {
        name: 'Waffles',
        description: '',
        icon: '🧇',
        color: '#0ea5e9',
        eatingOut: 'None',
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
        expect.objectContaining({ name: 'Waffles', description: 'Crispy and golden' }),
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
        initialMeals: Array.from({ length: 5 }, (_, index) =>
          meal({ id: `meal-${index}`, name: `Meal ${index}` }),
        ),
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
      const { fixture } = await setup(familyScope, {
        mealplans: { createMeal: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
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
      const { fixture } = await setup(familyScope, {
        mealplans: { createMeal: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
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
      const meals = [
        meal({ id: 'meal-1', name: 'Pancakes' }),
        meal({ id: 'meal-2', name: 'Toast' }),
      ];
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
      const meals = [
        meal({ id: 'meal-1', name: 'Pancakes' }),
        meal({ id: 'meal-2', name: 'Toast' }),
      ];
      const { fixture } = await setup(familyScope, {
        initialMeals: meals,
        mealplans: { archiveMeal },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const archiveButtons = () =>
        Array.from(compiled.querySelectorAll('button')).filter(
          (b) => b.textContent?.trim() === 'Archive',
        );

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
        mealplans: { archiveMeal: vi.fn(async () => Promise.reject(new Error('boom'))) },
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
      expect(compiled.textContent).toContain(
        "You have read-only access to this group's meal library.",
      );
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

  describe('eating out', () => {
    function eatingOutSelect(compiled: HTMLElement, mealName: string): HTMLSelectElement {
      return compiled.querySelector(`select[aria-label="Eating out: ${mealName}"]`)!;
    }

    it('shows a badge with the kind on eating-out meals only', async () => {
      const { fixture } = await setup(familyScope, {
        initialMeals: [
          meal({ id: 'home', name: 'Tacos' }),
          meal({ id: 'out', name: 'Restaurant', eatingOut: 'Restaurant' }),
        ],
      });
      await settle(fixture);

      const badges = Array.from(
        (fixture.nativeElement as HTMLElement).querySelectorAll('app-eating-out-badge'),
      ).map((badge) => badge.textContent?.trim() ?? '');

      expect(badges).toEqual(['', 'Eating out · Restaurant']);
    });

    it('creates a meal as eating out with the chosen kind, then resets the choice', async () => {
      const { fixture, mealplans } = await setup(familyScope);
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Dinner at Grandma’s');
      findButtonByText(compiled, 'Friends & family')!.click();
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(mealplans.createMeal).toHaveBeenCalledWith(
        familyScope,
        expect.objectContaining({ name: 'Dinner at Grandma’s', eatingOut: 'FriendsAndFamily' }),
      );
      expect(findButtonByText(compiled, 'No')!.getAttribute('aria-checked')).toBe('true');
      expect(compiled.textContent).toContain(
        'Eating-out meals are never suggested by the AI assistant.',
      );
    });

    it('changes an existing meal’s kind with a full details update', async () => {
      const updateMealDetails = vi.fn(async () => meal());
      const { fixture } = await setup(familyScope, {
        initialMeals: [meal({ id: 'meal-1', name: 'Pizza', description: 'Friday', color: '#f00' })],
        mealplans: { updateMealDetails },
      });
      await settle(fixture);

      const select = eatingOutSelect(fixture.nativeElement, 'Pizza');
      expect(Array.from(select.options).map((option) => option.textContent?.trim())).toEqual([
        'At home',
        'Friends & family',
        'Restaurant',
      ]);

      select.value = 'Restaurant';
      select.dispatchEvent(new Event('change'));
      await settle(fixture);

      expect(updateMealDetails).toHaveBeenCalledWith(familyScope, 'meal-1', {
        name: 'Pizza',
        description: 'Friday',
        icon: '🥞',
        color: '#f00',
        eatingOut: 'Restaurant',
      });
    });

    it('shows an error when changing the kind fails', async () => {
      const { fixture } = await setup(familyScope, {
        initialMeals: [meal({ name: 'Pizza' })],
        mealplans: { updateMealDetails: vi.fn(async () => Promise.reject(new Error('nope'))) },
      });
      await settle(fixture);

      const select = eatingOutSelect(fixture.nativeElement, 'Pizza');
      select.value = 'Restaurant';
      select.dispatchEvent(new Event('change'));
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain(
        'Unable to update this meal.',
      );
      expect(select.value).toBe('None');
    });

    it('hides the kind select for a read-only group', async () => {
      const { fixture } = await setup(groupViewScope, { initialMeals: [meal({ name: 'Pizza' })] });
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).querySelector('select')).toBeNull();
    });
  });

  describe('Mealie recipes', () => {
    const recipe = { recipeId: 'recipe-1', slug: 'lasagne', name: 'Lasagne al forno' };

    async function settleLong(fixture: { detectChanges: () => void }) {
      for (let i = 0; i < 6; i++) {
        await settle(fixture);
      }
    }

    it('shows no Mealie control without a connected family', async () => {
      const { fixture } = await setup(familyScope, { initialMeals: [meal({ recipe })] });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      expect(compiled.textContent).not.toContain('Mealie');
      expect(findButtonByText(compiled, 'View recipe')).toBeUndefined();
      expect(findButtonByText(compiled, 'Add from Mealie')).toBeUndefined();
    });

    it('marks linked meals and offers view or link per meal', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [
          meal({ id: 'meal-1', name: 'Lasagne', recipe }),
          meal({ id: 'meal-2', name: 'Soup' }),
        ],
      });
      await settle(fixture);
      const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));

      expect(rows[0].textContent).toContain('Mealie');
      expect(findButtonByText(rows[0], 'View recipe')).toBeDefined();
      expect(rows[1].textContent).not.toContain('Mealie');
      expect(findButtonByText(rows[1], 'Link recipe')).toBeDefined();
    });

    it('opens the recipe panel for a linked meal, and unlinks from it', async () => {
      const unlinkMealRecipe = vi.fn(async () => undefined);
      const { fixture, mealie } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Lasagne', recipe })],
        mealplans: { unlinkMealRecipe },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      const view = findButtonByText(compiled, 'View recipe')!;

      view.click();
      await settleLong(fixture);

      expect(view.getAttribute('aria-expanded')).toBe('true');
      expect(mealie.getRecipe).toHaveBeenCalledWith('child-1', 'recipe-1');
      expect(compiled.textContent).toContain('Linked to the meal “Lasagne”');

      findButtonByText(compiled, 'Unlink')!.click();
      await settleLong(fixture);

      expect(unlinkMealRecipe).toHaveBeenCalledWith('child-1', 'meal-1');
      expect(compiled.querySelector('app-mealie-recipe-panel')).toBeNull();
    });

    it('closes the panel on a second click', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Lasagne', recipe })],
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'View recipe')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'View recipe')!.click();
      await settle(fixture);

      expect(compiled.querySelector('app-mealie-recipe-panel')).toBeNull();
    });

    it('links a meal to the chosen recipe and shows it', async () => {
      const linkMealToRecipe = vi.fn(async () => meal({ id: 'meal-2', name: 'Soup', recipe }));
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-2', name: 'Soup' })],
        mealplans: { linkMealToRecipe },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Link recipe')!.click();
      await settleLong(fixture);
      expect(compiled.textContent).toContain('Link “Soup” to a Mealie recipe');

      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);

      expect(linkMealToRecipe).toHaveBeenCalledWith('child-1', 'meal-2', 'recipe-1');
      expect(compiled.querySelector('app-mealie-recipe-picker')).toBeNull();
      expect(compiled.querySelector('app-mealie-recipe-panel')).not.toBeNull();
    });

    it('shows why a link failed', async () => {
      const linkMealToRecipe = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-2', name: 'Soup' })],
        mealplans: { linkMealToRecipe },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Link recipe')!.click();
      await settleLong(fixture);
      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);

      expect(compiled.textContent).toContain('Couldn’t link the recipe.');
    });

    it('adds a meal from a Mealie recipe with the default icon and colour', async () => {
      const createMealFromRecipe = vi.fn(async () =>
        meal({ id: 'meal-9', name: 'Lasagne al forno', recipe }),
      );
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        mealplans: { createMealFromRecipe },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      const add = findButtonByText(compiled, 'Add from Mealie')!;

      add.click();
      await settleLong(fixture);
      expect(add.getAttribute('aria-expanded')).toBe('true');
      expect(compiled.textContent).toContain('Add a meal from a Mealie recipe');

      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);

      expect(createMealFromRecipe).toHaveBeenCalledWith('child-1', 'recipe-1', '🍽️', '#10b981');
      expect(compiled.querySelector('app-mealie-recipe-picker')).toBeNull();
    });

    it('opens the page the new meal from Mealie lands on, sorted by name', async () => {
      const names = ['Apple', 'Bread', 'Carrots', 'Dumplings', 'Eggs', 'Fish', 'Gravy'];
      const { fixture, mealsState } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: names.map((name, i) => meal({ id: `meal-${i}`, name })),
        mealplans: {
          createMealFromRecipe: vi.fn(async () => {
            const created = meal({ id: 'meal-new', name: 'Banana bread', recipe });
            // The real service keeps the library sorted by name.
            mealsState.update((current) =>
              [...current, created].sort((a, b) => a.name.localeCompare(b.name)),
            );
            return created;
          }),
        },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Next')!.click();
      await settle(fixture);

      findButtonByText(compiled, 'Add from Mealie')!.click();
      await settleLong(fixture);
      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);

      expect(compiled.textContent).toContain('Page 1 of 2');
      expect(compiled.textContent).toContain('Banana bread');
    });

    it('moves on to a later page when the new meal from Mealie sorts there', async () => {
      const names = ['Apple', 'Bread', 'Carrots', 'Dumplings', 'Eggs', 'Fish', 'Gravy'];
      const { fixture, mealsState } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: names.map((name, i) => meal({ id: `meal-${i}`, name })),
        mealplans: {
          createMealFromRecipe: vi.fn(async () => {
            const created = meal({ id: 'meal-new', name: 'Zucchini fritters', recipe });
            mealsState.update((current) =>
              [...current, created].sort((a, b) => a.name.localeCompare(b.name)),
            );
            return created;
          }),
        },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Page 1 of 2');

      findButtonByText(compiled, 'Add from Mealie')!.click();
      await settleLong(fixture);
      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);

      expect(compiled.textContent).toContain('Page 2 of 2');
      expect(compiled.textContent).toContain('Zucchini fritters');
    });

    it('closes the Mealie panel when the scope changes', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Lasagne', recipe })],
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'View recipe')!.click();
      await settle(fixture);
      expect(compiled.querySelector('app-mealie-recipe-panel')).not.toBeNull();

      fixture.componentRef.setInput('scope', groupManageScope);
      await settleLong(fixture);

      expect(compiled.querySelector('app-mealie-recipe-panel')).toBeNull();
    });

    it('drops a link error when the scope changes', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-2', name: 'Soup' })],
        mealplans: { linkMealToRecipe: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Link recipe')!.click();
      await settleLong(fixture);
      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);
      expect(compiled.textContent).toContain('Couldn’t link the recipe.');

      fixture.componentRef.setInput('scope', groupManageScope);
      await settleLong(fixture);

      expect(compiled.textContent).not.toContain('Couldn’t link the recipe.');
    });

    it('marks only the meal whose panel is open as expanded', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [
          meal({ id: 'meal-1', name: 'Lasagne', recipe }),
          meal({ id: 'meal-2', name: 'Soup' }),
        ],
      });
      await settle(fixture);
      const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));
      const view = findButtonByText(rows[0], 'View recipe')!;
      const link = findButtonByText(rows[1], 'Link recipe')!;

      view.click();
      await settle(fixture);

      expect(view.getAttribute('aria-expanded')).toBe('true');
      expect(link.getAttribute('aria-expanded')).toBe('false');
    });

    it('switches from the recipe panel to the picker with "Change recipe", and closes the picker', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Lasagne', recipe })],
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'View recipe')!.click();
      await settleLong(fixture);

      findButtonByText(compiled, 'Change recipe')!.click();
      await settleLong(fixture);

      expect(compiled.querySelector('app-mealie-recipe-panel')).toBeNull();
      expect(compiled.textContent).toContain('Link “Lasagne” to a Mealie recipe');

      findButtonByText(compiled, 'Close')!.click();
      await settle(fixture);
      expect(compiled.querySelector('app-mealie-recipe-picker')).toBeNull();
    });

    it('shows why unlinking failed', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Lasagne', recipe })],
        mealplans: { unlinkMealRecipe: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'View recipe')!.click();
      await settleLong(fixture);

      findButtonByText(compiled, 'Unlink')!.click();
      await settleLong(fixture);

      expect(compiled.textContent).toContain('Couldn’t unlink the recipe.');
      expect(compiled.querySelector('app-mealie-recipe-panel')).not.toBeNull();
    });

    it('shows why adding from Mealie failed, and drops the error once another Mealie action starts', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Soup' })],
        mealplans: { createMealFromRecipe: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Add from Mealie')!.click();
      await settleLong(fixture);
      findButtonByText(compiled, 'Choose')!.click();
      await settleLong(fixture);
      expect(compiled.textContent).toContain('Couldn’t add the meal.');

      findButtonByText(compiled, 'Link recipe')!.click();
      await settle(fixture);
      expect(compiled.textContent).not.toContain('Couldn’t add the meal.');
    });

    it('drops a link error when "Add from Mealie" or "Change recipe" starts over', async () => {
      const linkMealToRecipe = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [
          meal({ id: 'meal-1', name: 'Lasagne', recipe }),
          meal({ id: 'meal-2', name: 'Soup' }),
        ],
        mealplans: { linkMealToRecipe },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      const failLink = async () => {
        findButtonByText(compiled, 'Choose')!.click();
        await settleLong(fixture);
        expect(compiled.textContent).toContain('Couldn’t link the recipe.');
      };

      findButtonByText(compiled, 'Link recipe')!.click();
      await settleLong(fixture);
      await failLink();
      findButtonByText(compiled, 'Add from Mealie')!.click();
      await settle(fixture);
      expect(compiled.textContent).not.toContain('Couldn’t link the recipe.');
    });

    it('drops an unlink error when "Change recipe" starts over', async () => {
      const { fixture } = await setup(familyScope, {
        mealieChildId: 'child-1',
        initialMeals: [meal({ id: 'meal-1', name: 'Lasagne', recipe })],
        mealplans: { unlinkMealRecipe: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'View recipe')!.click();
      await settleLong(fixture);
      findButtonByText(compiled, 'Unlink')!.click();
      await settleLong(fixture);
      expect(compiled.textContent).toContain('Couldn’t unlink the recipe.');

      findButtonByText(compiled, 'Change recipe')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Couldn’t unlink the recipe.');
    });

    it('toggles the create picker closed on a second click', async () => {
      const { fixture } = await setup(familyScope, { mealieChildId: 'child-1' });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Add from Mealie')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Add from Mealie')!.click();
      await settle(fixture);

      expect(compiled.querySelector('app-mealie-recipe-picker')).toBeNull();
    });
  });
});
