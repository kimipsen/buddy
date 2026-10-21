import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { MealieRecipeDetails, MealieService } from '../../../../../core/mealie.service';
import { MealieRecipePanel } from './mealie-recipe-panel';

describe('MealieRecipePanel', () => {
  function details(overrides: Partial<MealieRecipeDetails> = {}): MealieRecipeDetails {
    return {
      id: 'recipe-1',
      slug: 'lasagne-al-forno',
      name: 'Lasagne al forno',
      description: 'Layers of pasta.',
      totalTime: '1 hour 30 minutes',
      recipeYield: '6 servings',
      hasImage: true,
      ingredients: ['500 g minced beef', '12 lasagne sheets'],
      steps: [
        { title: '', text: 'Brown the beef.' },
        { title: 'Sauce', text: 'Make a white sauce.' },
      ],
      originalUrl: 'https://recipes.example/lasagne',
      mealieUrl: 'https://mealie.holm.example/g/holm/r/lasagne-al-forno',
      linkedMealId: 'meal-1',
      ...overrides,
    };
  }

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:photo-1');
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => vi.restoreAllMocks());

  async function setup(mealie: Partial<MealieService>) {
    const stub: Partial<MealieService> = {
      getRecipe: vi.fn(async () => details()),
      getRecipeImage: vi.fn(async () => new Blob(['RIFF'], { type: 'image/webp' })),
      ...mealie,
    };

    await TestBed.configureTestingModule({
      imports: [MealieRecipePanel],
      providers: [{ provide: MealieService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealieRecipePanel);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('recipeId', 'recipe-1');
    fixture.componentRef.setInput('mealName', 'Lasagne');
    const outputs = { changeRecipe: vi.fn(), unlink: vi.fn(), closed: vi.fn() };
    fixture.componentInstance.changeRecipe.subscribe(outputs.changeRecipe);
    fixture.componentInstance.unlink.subscribe(outputs.unlink);
    fixture.componentInstance.closed.subscribe(outputs.closed);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, mealie: stub, outputs };
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

  it('shows the recipe with its photo, ingredients, steps and links', async () => {
    const { fixture, compiled, mealie } = await setup({});
    await settle(fixture);

    expect(mealie.getRecipe).toHaveBeenCalledWith('child-1', 'recipe-1');
    expect(mealie.getRecipeImage).toHaveBeenCalledWith('child-1', 'recipe-1');
    const img = compiled.querySelector('img')!;
    expect(img.getAttribute('src')).toBe('blob:photo-1');
    expect(img.getAttribute('alt')).toBe('Photo of Lasagne al forno');
    expect(compiled.textContent).toContain('Linked to the meal “Lasagne”');
    expect(compiled.textContent).toContain('⏱ 1 hour 30 minutes');
    expect(compiled.textContent).toContain('🍽 6 servings');
    expect(compiled.textContent).toContain('Layers of pasta.');
    expect(
      Array.from(compiled.querySelectorAll('ul li')).map((li) => li.textContent?.trim()),
    ).toEqual(['500 g minced beef', '12 lasagne sheets']);
    const steps = Array.from(compiled.querySelectorAll('ol li')).map((li) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(steps).toEqual(['Brown the beef.', 'Sauce: Make a white sauce.']);
    const links = Array.from(compiled.querySelectorAll('a'));
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      'https://mealie.holm.example/g/holm/r/lasagne-al-forno',
      'https://recipes.example/lasagne',
    ]);
    expect(links.every((a) => a.getAttribute('rel') === 'noopener noreferrer')).toBe(true);
  });

  it('leaves out what the recipe doesn’t have', async () => {
    const { fixture, compiled, mealie } = await setup({
      getRecipe: vi.fn(async () =>
        details({
          hasImage: false,
          totalTime: '',
          recipeYield: '',
          description: '',
          ingredients: [],
          steps: [],
          originalUrl: '',
        }),
      ),
    });
    await settle(fixture);

    expect(mealie.getRecipeImage).not.toHaveBeenCalled();
    expect(compiled.querySelector('img')).toBeNull();
    expect(compiled.textContent).not.toContain('⏱');
    expect(compiled.textContent).not.toContain('Ingredients');
    expect(compiled.textContent).not.toContain('Steps');
    expect(compiled.querySelectorAll('a')).toHaveLength(1);
  });

  it('shows the recipe without a photo when the photo fails', async () => {
    const { fixture, compiled } = await setup({
      getRecipeImage: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);

    expect(compiled.querySelector('img')).toBeNull();
    expect(compiled.textContent).toContain('Lasagne al forno');
  });

  it('says when the recipe is gone from Mealie, and still offers to unlink', async () => {
    const { fixture, compiled, outputs } = await setup({
      getRecipe: vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 404 }))),
    });
    await settle(fixture);

    expect(compiled.textContent).toContain('This recipe is no longer in Mealie.');
    button(compiled, 'Unlink')!.click();
    expect(outputs.unlink).toHaveBeenCalled();
  });

  it('shows why the recipe couldn’t load', async () => {
    const { fixture, compiled } = await setup({
      getRecipe: vi.fn(async () =>
        Promise.reject(
          new HttpErrorResponse({ status: 502, error: { code: 'mealie_token_rejected' } }),
        ),
      ),
    });
    await settle(fixture);

    expect(compiled.textContent).toContain('Your Mealie rejected Buddy’s API token.');
  });

  it('emits change, unlink and close, and disables the actions while busy', async () => {
    const { fixture, compiled, outputs } = await setup({});
    await settle(fixture);

    button(compiled, 'Change recipe')!.click();
    button(compiled, 'Unlink')!.click();
    button(compiled, 'Close')!.click();
    expect(outputs.changeRecipe).toHaveBeenCalledTimes(1);
    expect(outputs.unlink).toHaveBeenCalledTimes(1);
    expect(outputs.closed).toHaveBeenCalledTimes(1);

    fixture.componentRef.setInput('busy', true);
    await settle(fixture);
    expect(button(compiled, 'Change recipe')!.disabled).toBe(true);
    expect(button(compiled, 'Unlink')!.disabled).toBe(true);
  });

  it('never shows the photo of a recipe it has moved on from', async () => {
    let releaseFirstPhoto!: (blob: Blob) => void;
    const getRecipeImage = vi
      .fn()
      .mockImplementationOnce(() => new Promise<Blob>((resolve) => (releaseFirstPhoto = resolve)))
      .mockResolvedValueOnce(new Blob(['second']));
    const createObjectURL = vi.fn().mockReturnValue('blob:second');
    URL.createObjectURL = createObjectURL;
    const { fixture, compiled } = await setup({ getRecipeImage });
    await settle(fixture);

    fixture.componentRef.setInput('recipeId', 'recipe-2');
    await settle(fixture);
    releaseFirstPhoto(new Blob(['first']));
    await settle(fixture);

    expect(createObjectURL).toHaveBeenCalledTimes(1);
    expect(compiled.querySelector('img')?.getAttribute('src')).toBe('blob:second');
  });

  it('creates no photo URL when it closes before the photo arrives', async () => {
    let releasePhoto!: (blob: Blob) => void;
    const { fixture } = await setup({
      getRecipeImage: vi.fn(() => new Promise<Blob>((resolve) => (releasePhoto = resolve))),
    });
    await settle(fixture);

    fixture.destroy();
    releasePhoto(new Blob(['late']));
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });

  it('revokes the first photo when it moves on to another recipe', async () => {
    const { fixture } = await setup({});
    await settle(fixture);

    fixture.componentRef.setInput('recipeId', 'recipe-2');
    await settle(fixture);

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:photo-1');
  });

  it('falls back to a general message for an error without a Mealie code', async () => {
    const { fixture, compiled } = await setup({
      getRecipe: vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 500 }))),
    });
    await settle(fixture);

    expect(compiled.textContent).toContain('Your Mealie answered with an error.');
  });

  it('revokes the photo URL when destroyed', async () => {
    const { fixture } = await setup({});
    await settle(fixture);

    fixture.destroy();

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:photo-1');
  });
  it('drops the photo of an earlier recipe that arrives after it closed', async () => {
    let releaseFirstPhoto!: (blob: Blob) => void;
    const getRecipeImage = vi
      .fn()
      .mockImplementationOnce(() => new Promise<Blob>((resolve) => (releaseFirstPhoto = resolve)))
      .mockImplementationOnce(() => new Promise<Blob>(() => undefined));
    const { fixture } = await setup({ getRecipeImage });
    await settle(fixture);
    fixture.componentRef.setInput('recipeId', 'recipe-2');
    await settle(fixture);

    fixture.destroy();
    releaseFirstPhoto(new Blob(['first']));
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });

  it('takes the old photo away as soon as it moves to a recipe without one', async () => {
    const getRecipe = vi
      .fn()
      .mockResolvedValueOnce(details())
      .mockResolvedValueOnce(details({ id: 'recipe-2', name: 'Tacos', hasImage: false }));
    const { fixture, compiled } = await setup({ getRecipe });
    await settle(fixture);
    expect(compiled.querySelector('img')).not.toBeNull();

    fixture.componentRef.setInput('recipeId', 'recipe-2');
    await settle(fixture);

    expect(compiled.textContent).toContain('Tacos');
    expect(compiled.querySelector('img')).toBeNull();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:photo-1');
  });
  it('doesn’t fetch the photo of a recipe it moved on from before it loaded', async () => {
    let releaseFirst!: (recipe: MealieRecipeDetails) => void;
    const getRecipe = vi
      .fn()
      .mockImplementationOnce(
        () => new Promise<MealieRecipeDetails>((resolve) => (releaseFirst = resolve)),
      )
      .mockResolvedValueOnce(details({ id: 'recipe-2', name: 'Tacos' }));
    const { fixture, mealie } = await setup({ getRecipe });
    await settle(fixture);

    fixture.componentRef.setInput('recipeId', 'recipe-2');
    await settle(fixture);
    releaseFirst(details());
    await settle(fixture);

    expect(mealie.getRecipeImage).toHaveBeenCalledTimes(1);
    expect(mealie.getRecipeImage).toHaveBeenCalledWith('child-1', 'recipe-2');
  });
});
