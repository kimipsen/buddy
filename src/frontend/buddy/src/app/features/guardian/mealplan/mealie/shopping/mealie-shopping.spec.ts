import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AddedToShoppingList, MealieService } from '../../../../../core/mealie.service';
import { MealieShopping } from './mealie-shopping';

describe('MealieShopping', () => {
  const lists = [
    { id: 'list-1', name: 'Weekly shop' },
    { id: 'list-2', name: 'Costco' },
  ];

  function added(overrides: Partial<AddedToShoppingList> = {}): AddedToShoppingList {
    return {
      added: [
        { recipeId: 'recipe-1', recipeName: 'Lasagne al forno', times: 2 },
        { recipeId: 'recipe-2', recipeName: 'Chicken tacos', times: 1 },
      ],
      skippedUnlinked: [],
      skippedEatingOut: 0,
      ...overrides,
    };
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  async function setup(mealie: Partial<MealieService> = {}) {
    const stub: Partial<MealieService> = {
      listShoppingLists: vi.fn(async () => lists),
      addPlanToShoppingList: vi.fn(async () => added()),
      ...mealie,
    };

    await TestBed.configureTestingModule({
      imports: [MealieShopping],
      providers: [{ provide: MealieService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealieShopping);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('from', '2026-10-19');
    fixture.componentRef.setInput('to', '2026-10-25');
    return { fixture, compiled: fixture.nativeElement as HTMLElement, mealie: stub };
  }

  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();

    for (let i = 0; i < 6; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  async function open(fixture: { detectChanges: () => void }, compiled: HTMLElement) {
    fixture.detectChanges();
    button(compiled, '🛒 Add to shopping list')!.click();
    await settle(fixture);
  }

  it('opens with the first list selected and adds the week’s recipes to it', async () => {
    const { fixture, compiled, mealie } = await setup();
    fixture.detectChanges();
    const toggle = button(compiled, '🛒 Add to shopping list')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');

    await open(fixture, compiled);

    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(mealie.listShoppingLists).toHaveBeenCalledWith('child-1');
    expect(compiled.querySelector<HTMLSelectElement>('#mealieShoppingList')!.value).toBe('list-1');

    button(compiled, 'Add')!.click();
    await settle(fixture);

    expect(mealie.addPlanToShoppingList).toHaveBeenCalledWith(
      'child-1',
      'list-1',
      '2026-10-19',
      '2026-10-25',
      ['Breakfast', 'Lunch', 'Dinner', 'Snack'],
    );
    expect(compiled.querySelector('#mealieShoppingPanel')).toBeNull();
    expect(compiled.textContent).toContain(
      'Added to “Weekly shop” in Mealie: Lasagne al forno ×2, Chicken tacos ×1.',
    );
    expect(localStorage.getItem('buddy_mealie_shopping_list')).toBe('list-1');
  });

  it('drops the result once the plan shows another week', async () => {
    const { fixture, compiled } = await setup();
    await open(fixture, compiled);
    button(compiled, 'Add')!.click();
    await settle(fixture);
    expect(compiled.querySelector('[role="status"]')).not.toBeNull();

    fixture.componentRef.setInput('from', '2026-10-26');
    fixture.componentRef.setInput('to', '2026-11-01');
    await settle(fixture);

    expect(compiled.querySelector('[role="status"]')).toBeNull();
  });

  it('keeps the lists of the latest opening when an earlier load answers last', async () => {
    let releaseFirst!: (lists: { id: string; name: string }[]) => void;
    const listShoppingLists = vi
      .fn()
      .mockImplementationOnce(() => new Promise((resolve) => (releaseFirst = resolve)))
      .mockResolvedValueOnce([{ id: 'list-2', name: 'Costco' }]);
    const { fixture, compiled } = await setup({ listShoppingLists });

    await open(fixture, compiled);
    await open(fixture, compiled);
    await open(fixture, compiled);
    releaseFirst([{ id: 'list-1', name: 'Weekly shop' }]);
    await settle(fixture);

    expect(compiled.querySelector<HTMLSelectElement>('#mealieShoppingList')!.value).toBe('list-2');
    expect(compiled.querySelectorAll('#mealieShoppingList option')).toHaveLength(1);
  });

  it('points aria-controls at the panel only while it is open', async () => {
    const { fixture, compiled } = await setup();
    fixture.detectChanges();
    const toggle = button(compiled, '🛒 Add to shopping list')!;
    expect(toggle.hasAttribute('aria-controls')).toBe(false);

    await open(fixture, compiled);

    expect(toggle.getAttribute('aria-controls')).toBe('mealieShoppingPanel');
  });

  it('names the list that was chosen, not the first one', async () => {
    const { fixture, compiled, mealie } = await setup();
    await open(fixture, compiled);
    const select = compiled.querySelector<HTMLSelectElement>('#mealieShoppingList')!;
    select.value = 'list-2';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    button(compiled, 'Add')!.click();
    await settle(fixture);

    expect(mealie.addPlanToShoppingList).toHaveBeenCalledWith(
      'child-1',
      'list-2',
      '2026-10-19',
      '2026-10-25',
      expect.any(Array),
    );
    expect(compiled.textContent).toContain('Added to “Costco” in Mealie');
  });

  it('starts a reopened panel clean: no old result, add error or list error', async () => {
    const listShoppingLists = vi
      .fn()
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }))
      .mockResolvedValue(lists);
    const addPlanToShoppingList = vi
      .fn()
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }))
      .mockResolvedValue(added());
    const { fixture, compiled } = await setup({ listShoppingLists, addPlanToShoppingList });

    await open(fixture, compiled);
    expect(compiled.textContent).toContain('Your Mealie answered with an error.');
    await open(fixture, compiled);
    await open(fixture, compiled);
    expect(compiled.textContent).not.toContain('Your Mealie answered with an error.');

    button(compiled, 'Add')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Couldn’t add to the shopping list.');
    await open(fixture, compiled);
    await open(fixture, compiled);
    expect(compiled.textContent).not.toContain('Couldn’t add to the shopping list.');

    button(compiled, 'Add')!.click();
    await settle(fixture);
    expect(compiled.querySelector('[role="status"]')).not.toBeNull();
    await open(fixture, compiled);
    expect(compiled.querySelector('[role="status"]')).toBeNull();
  });

  it('preselects the list used last time', async () => {
    localStorage.setItem('buddy_mealie_shopping_list', 'list-2');
    const { fixture, compiled } = await setup();

    await open(fixture, compiled);

    expect(compiled.querySelector<HTMLSelectElement>('#mealieShoppingList')!.value).toBe('list-2');
  });

  it('falls back to the first list when the remembered one is gone', async () => {
    localStorage.setItem('buddy_mealie_shopping_list', 'list-deleted');
    const { fixture, compiled } = await setup();

    await open(fixture, compiled);

    expect(compiled.querySelector<HTMLSelectElement>('#mealieShoppingList')!.value).toBe('list-1');
  });

  it('reports what was skipped and when nothing was added', async () => {
    const { fixture, compiled } = await setup({
      addPlanToShoppingList: vi.fn(async () =>
        added({ added: [], skippedUnlinked: ['Fish fingers', 'Toast'], skippedEatingOut: 1 }),
      ),
    });
    await open(fixture, compiled);

    button(compiled, 'Add')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(
      'Nothing was added: no meal this week has a Mealie recipe.',
    );
    expect(compiled.textContent).toContain('Skipped meals without a recipe: Fish fingers, Toast.');
    expect(compiled.textContent).toContain('Eating-out meals are never added.');
  });

  it('says when Mealie has no lists', async () => {
    const { fixture, compiled } = await setup({ listShoppingLists: vi.fn(async () => []) });

    await open(fixture, compiled);

    expect(compiled.textContent).toContain('Your Mealie has no shopping lists yet.');
    expect(button(compiled, 'Add')).toBeUndefined();
  });

  it('shows why the lists couldn’t load', async () => {
    const error = new HttpErrorResponse({ status: 502, error: { code: 'mealie_unreachable' } });
    const { fixture, compiled } = await setup({
      listShoppingLists: vi.fn(async () => Promise.reject(error)),
    });

    await open(fixture, compiled);

    expect(compiled.textContent).toContain('Buddy couldn’t reach your Mealie.');
  });

  it('keeps the panel open with the backend’s reason when adding fails', async () => {
    const error = new HttpErrorResponse({ status: 502, error: { code: 'mealie_error' } });
    const { fixture, compiled } = await setup({
      addPlanToShoppingList: vi.fn(async () => Promise.reject(error)),
    });
    await open(fixture, compiled);

    button(compiled, 'Add')!.click();
    await settle(fixture);

    expect(compiled.querySelector('#mealieShoppingPanel')).not.toBeNull();
    expect(compiled.textContent).toContain('Your Mealie answered with an error.');
    expect(localStorage.getItem('buddy_mealie_shopping_list')).toBeNull();
  });

  it('closes again on Cancel and on a second click of the button', async () => {
    const { fixture, compiled } = await setup();
    await open(fixture, compiled);

    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.querySelector('#mealieShoppingPanel')).toBeNull();

    await open(fixture, compiled);
    await open(fixture, compiled);
    expect(compiled.querySelector('#mealieShoppingPanel')).toBeNull();
  });
  it('drops the result when either end of the week changes', async () => {
    for (const [input, value] of [
      ['from', '2026-10-20'],
      ['to', '2026-10-24'],
    ] as const) {
      TestBed.resetTestingModule();
      const { fixture, compiled } = await setup();
      await open(fixture, compiled);
      button(compiled, 'Add')!.click();
      await settle(fixture);
      expect(compiled.querySelector('[role="status"]')).not.toBeNull();

      fixture.componentRef.setInput(input, value);
      await settle(fixture);

      expect(compiled.querySelector('[role="status"]')).toBeNull();
    }
  });

  it('shows it is loading again, not the old lists, when reopened', async () => {
    const listShoppingLists = vi
      .fn()
      .mockResolvedValueOnce(lists)
      .mockImplementationOnce(() => new Promise(() => undefined));
    const { fixture, compiled } = await setup({ listShoppingLists });
    await open(fixture, compiled);
    expect(compiled.querySelector('#mealieShoppingList')).not.toBeNull();

    await open(fixture, compiled);
    await open(fixture, compiled);

    expect(compiled.querySelector('#mealieShoppingList')).toBeNull();
    expect(compiled.textContent).toContain('Loading shopping lists…');
  });

  it('shows no error from an earlier opening that fails after a newer one', async () => {
    let failFirst!: (error: unknown) => void;
    const listShoppingLists = vi
      .fn()
      .mockImplementationOnce(() => new Promise((_, reject) => (failFirst = reject)))
      .mockResolvedValueOnce(lists);
    const { fixture, compiled } = await setup({ listShoppingLists });

    await open(fixture, compiled);
    await open(fixture, compiled);
    await open(fixture, compiled);
    failFirst(new HttpErrorResponse({ status: 502, error: { code: 'mealie_unreachable' } }));
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Buddy couldn’t reach your Mealie.');
    expect(compiled.querySelector('#mealieShoppingList')).not.toBeNull();
  });
});
