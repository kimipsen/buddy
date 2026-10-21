import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  MealieRecipePage,
  MealieRecipeSummary,
  MealieService,
} from '../../../../../core/mealie.service';
import { MealieRecipePicker } from './mealie-recipe-picker';

describe('MealieRecipePicker', () => {
  function recipe(overrides: Partial<MealieRecipeSummary> = {}): MealieRecipeSummary {
    return {
      id: 'recipe-1',
      slug: 'lasagne',
      name: 'Lasagne al forno',
      description: '',
      totalTime: '1 hour 30 minutes',
      hasImage: true,
      linkedMealId: null,
      ...overrides,
    };
  }

  function page(items: MealieRecipeSummary[], current = 1, totalPages = 1): MealieRecipePage {
    return { items, page: current, totalPages, total: items.length };
  }

  async function setup(searchRecipes: MealieService['searchRecipes']) {
    await TestBed.configureTestingModule({
      imports: [MealieRecipePicker],
      providers: [{ provide: MealieService, useValue: { searchRecipes } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealieRecipePicker);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('heading', 'Link “Lasagne” to a Mealie recipe');
    const picked = vi.fn();
    const closed = vi.fn();
    fixture.componentInstance.picked.subscribe(picked);
    fixture.componentInstance.closed.subscribe(closed);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, picked, closed };
  }

  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();

    for (let i = 0; i < 5; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  it('lists every recipe at first, with time and the linked badge, and hands back the choice', async () => {
    const searchRecipes = vi.fn(async () =>
      page([
        recipe(),
        recipe({ id: 'recipe-2', name: 'Tacos', totalTime: '', linkedMealId: 'meal-1' }),
      ]),
    );
    const { fixture, compiled, picked } = await setup(searchRecipes);
    await settle(fixture);

    expect(searchRecipes).toHaveBeenCalledWith('child-1', '', 1);
    expect(compiled.textContent).toContain('Link “Lasagne” to a Mealie recipe');
    const items = compiled.querySelectorAll('li');
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('⏱ 1 hour 30 minutes');
    expect(items[0].textContent).not.toContain('Already linked');
    expect(items[1].textContent).not.toContain('⏱');
    expect(items[1].textContent).toContain('Already linked');

    button(items[1] as HTMLElement, 'Choose')!.click();
    expect(picked).toHaveBeenCalledWith(expect.objectContaining({ id: 'recipe-2' }));
  });

  it('searches with the trimmed text and replaces the results', async () => {
    const searchRecipes = vi
      .fn()
      .mockResolvedValueOnce(page([recipe(), recipe({ id: 'recipe-2', name: 'Tacos' })]))
      .mockResolvedValueOnce(page([recipe({ id: 'recipe-2', name: 'Tacos' })]));
    const { fixture, compiled } = await setup(searchRecipes);
    await settle(fixture);

    const input = compiled.querySelector<HTMLInputElement>('#mealieRecipeSearch')!;
    input.value = '  tacos ';
    input.dispatchEvent(new Event('input'));
    button(compiled, 'Search')!.click();
    await settle(fixture);

    expect(searchRecipes).toHaveBeenLastCalledWith('child-1', 'tacos', 1);
    expect(compiled.querySelectorAll('li')).toHaveLength(1);
  });

  it('appends the next page on "Show more" and hides it on the last page', async () => {
    const searchRecipes = vi
      .fn()
      .mockResolvedValueOnce(page([recipe()], 1, 2))
      .mockResolvedValueOnce(page([recipe({ id: 'recipe-2', name: 'Tacos' })], 2, 2));
    const { fixture, compiled } = await setup(searchRecipes);
    await settle(fixture);

    button(compiled, 'Show more')!.click();
    await settle(fixture);

    expect(searchRecipes).toHaveBeenLastCalledWith('child-1', '', 2);
    expect(compiled.querySelectorAll('li')).toHaveLength(2);
    expect(button(compiled, 'Show more')).toBeUndefined();
  });

  it('ignores an earlier answer that arrives after a newer search', async () => {
    let releaseMore!: (result: MealieRecipePage) => void;
    const searchRecipes = vi
      .fn()
      .mockResolvedValueOnce(page([recipe()], 1, 2))
      .mockImplementationOnce(
        () => new Promise<MealieRecipePage>((resolve) => (releaseMore = resolve)),
      )
      .mockResolvedValueOnce(page([recipe({ id: 'recipe-3', name: 'Tacos' })]));
    const { fixture, compiled } = await setup(searchRecipes);
    await settle(fixture);

    button(compiled, 'Show more')!.click();
    const input = compiled.querySelector<HTMLInputElement>('#mealieRecipeSearch')!;
    input.value = 'tacos';
    input.dispatchEvent(new Event('input'));
    button(compiled, 'Search')!.click();
    await settle(fixture);
    releaseMore(page([recipe({ id: 'recipe-2', name: 'Old page two' })], 2, 2));
    await settle(fixture);

    const names = Array.from(compiled.querySelectorAll('li')).map((li) => li.textContent);
    expect(names).toHaveLength(1);
    expect(names[0]).toContain('Tacos');
    expect(compiled.textContent).not.toContain('Loading recipes');
  });

  it('puts the cursor in the search box when it opens', async () => {
    const focus = vi.spyOn(HTMLInputElement.prototype, 'focus');
    const { fixture, compiled } = await setup(vi.fn(async () => page([recipe()])));
    await settle(fixture);

    expect(focus).toHaveBeenCalled();
    expect(focus.mock.contexts.at(-1)).toBe(compiled.querySelector('#mealieRecipeSearch'));
    focus.mockRestore();
  });

  it('shows it is loading and drops the last error while a new search runs', async () => {
    let releaseSearch!: (result: MealieRecipePage) => void;
    const searchRecipes = vi
      .fn()
      .mockRejectedValueOnce(
        new HttpErrorResponse({ status: 502, error: { code: 'mealie_unreachable' } }),
      )
      .mockImplementationOnce(
        () => new Promise<MealieRecipePage>((resolve) => (releaseSearch = resolve)),
      );
    const { fixture, compiled } = await setup(searchRecipes);
    await settle(fixture);
    expect(compiled.textContent).toContain('Buddy couldn’t reach your Mealie.');

    button(compiled, 'Search')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Loading recipes…');
    expect(compiled.textContent).not.toContain('Buddy couldn’t reach your Mealie.');
    releaseSearch(page([recipe()]));
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Loading recipes…');
  });

  it('falls back to a general message for an error without a Mealie code', async () => {
    const { fixture, compiled } = await setup(
      vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 500 }))),
    );
    await settle(fixture);

    expect(compiled.textContent).toContain('Your Mealie answered with an error.');
  });

  it('says when nothing matches', async () => {
    const { fixture, compiled } = await setup(vi.fn(async () => page([])));
    await settle(fixture);

    expect(compiled.textContent).toContain('No recipes match.');
  });

  it('shows why Mealie could not be searched', async () => {
    const error = new HttpErrorResponse({ status: 502, error: { code: 'mealie_unreachable' } });
    const { fixture, compiled } = await setup(vi.fn(async () => Promise.reject(error)));
    await settle(fixture);

    expect(compiled.textContent).toContain('Buddy couldn’t reach your Mealie.');
    expect(compiled.textContent).not.toContain('No recipes match.');
  });

  it('disables choosing while busy and closes on request', async () => {
    const { fixture, compiled, closed } = await setup(vi.fn(async () => page([recipe()])));
    fixture.componentRef.setInput('busy', true);
    await settle(fixture);

    expect(button(compiled, 'Choose')!.disabled).toBe(true);
    button(compiled, 'Close')!.click();
    expect(closed).toHaveBeenCalled();
  });
  it('shows no error from an earlier search that fails after a newer one', async () => {
    let failFirst!: (error: unknown) => void;
    const searchRecipes = vi
      .fn()
      .mockImplementationOnce(
        () => new Promise<MealieRecipePage>((_, reject) => (failFirst = reject)),
      )
      .mockResolvedValueOnce(page([recipe()]));
    const { fixture, compiled } = await setup(searchRecipes);
    await settle(fixture);

    button(compiled, 'Search')!.click();
    await settle(fixture);
    failFirst(new HttpErrorResponse({ status: 502, error: { code: 'mealie_unreachable' } }));
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Buddy couldn’t reach your Mealie.');
    expect(compiled.querySelectorAll('li')).toHaveLength(1);
  });

  it('keeps saying it is loading while the newest search runs', async () => {
    let releaseFirst!: (result: MealieRecipePage) => void;
    const searchRecipes = vi
      .fn()
      .mockImplementationOnce(
        () => new Promise<MealieRecipePage>((resolve) => (releaseFirst = resolve)),
      )
      .mockImplementationOnce(() => new Promise<MealieRecipePage>(() => undefined));
    const { fixture, compiled } = await setup(searchRecipes);
    await settle(fixture);

    button(compiled, 'Search')!.click();
    await settle(fixture);
    releaseFirst(page([recipe()]));
    await settle(fixture);

    expect(compiled.textContent).toContain('Loading recipes…');
  });
});
