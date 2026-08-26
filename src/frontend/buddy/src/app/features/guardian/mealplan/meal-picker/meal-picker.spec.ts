import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Meal } from '../../../../core/mealplans.service';
import { MealPicker } from './meal-picker';

describe('MealPicker', () => {
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

  const pancakes = meal();
  const tacos = meal({ id: 'meal-2', name: 'Tacos', icon: '🌮' });
  const soup = meal({ id: 'meal-3', name: 'Tomato Soup', icon: '🍅' });

  async function setup(options: { meals?: Meal[]; mealId?: string; disabled?: boolean } = {}) {
    await TestBed.configureTestingModule({ imports: [MealPicker] }).compileComponents();

    const fixture = TestBed.createComponent(MealPicker);
    const onMealIdChange = vi.fn();
    fixture.componentInstance.mealIdChange.subscribe(onMealIdChange);

    fixture.componentRef.setInput('meals', options.meals ?? [pancakes, tacos, soup]);
    fixture.componentRef.setInput('mealId', options.mealId ?? '');
    fixture.componentRef.setInput('disabled', options.disabled ?? false);
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, onMealIdChange };
  }

  function textInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input')!;
  }

  // Everything in MealPicker is driven by plain signals/computed (no promises, no HttpClient), so
  // there's no async work to wait out -- but Angular's zoneless event-listener wrapper only
  // *schedules* change detection after a handler runs rather than applying it inline, so the DOM
  // needs an explicit detectChanges() call after each dispatched event to reflect it synchronously.
  function fireEvent(fixture: ComponentFixture<MealPicker>, event: Event): void {
    textInput(fixture.nativeElement as HTMLElement).dispatchEvent(event);
    fixture.detectChanges();
  }

  function openDropdown(fixture: ComponentFixture<MealPicker>): void {
    fireEvent(fixture, new Event('focus'));
  }

  function typeQuery(fixture: ComponentFixture<MealPicker>, value: string): void {
    const input = textInput(fixture.nativeElement as HTMLElement);
    input.value = value;
    fireEvent(fixture, new Event('input'));
  }

  function pressKey(fixture: ComponentFixture<MealPicker>, key: string): void {
    fireEvent(fixture, new KeyboardEvent('keydown', { key }));
  }

  function clickOption(fixture: ComponentFixture<MealPicker>, name: string): void {
    const compiled = fixture.nativeElement as HTMLElement;
    findMealOption(compiled, name).click();
    fixture.detectChanges();
  }

  // The "not planned" option is always the first <li>, followed by one <li> per (filtered) meal.
  function optionButtons(compiled: HTMLElement): HTMLButtonElement[] {
    return Array.from(compiled.querySelectorAll('ul li button'));
  }

  function optionLabels(compiled: HTMLElement): string[] {
    return optionButtons(compiled).map((button) => button.textContent!.trim().replace(/\s+/g, ' '));
  }

  function findMealOption(compiled: HTMLElement, name: string): HTMLButtonElement {
    return optionButtons(compiled).find((button) => button.textContent?.includes(name))!;
  }

  describe('closed display value', () => {
    it('shows nothing when no meal is selected', async () => {
      const { compiled } = await setup({ mealId: '' });

      expect(textInput(compiled).value).toBe('');
    });

    it("shows the selected meal's icon and name", async () => {
      const { compiled } = await setup({ mealId: 'meal-2' });

      expect(textInput(compiled).value).toBe('🌮 Tacos');
    });

    it('shows nothing when mealId does not match any meal in the list', async () => {
      const { compiled } = await setup({ mealId: 'does-not-exist' });

      expect(textInput(compiled).value).toBe('');
    });

    it('renders the native input as disabled and does not open the dropdown on focus', async () => {
      const { fixture, compiled } = await setup({ disabled: true });

      expect(textInput(compiled).disabled).toBe(true);

      openDropdown(fixture);

      expect(compiled.querySelector('ul')).toBeFalsy();
    });
  });

  describe('opening the dropdown', () => {
    it('renders the "not planned" option followed by every meal, showing icon and name', async () => {
      const { fixture, compiled } = await setup({ mealId: 'meal-2' });

      openDropdown(fixture);

      expect(optionLabels(compiled)).toEqual(['Not planned', '🥞 Pancakes', '🌮 Tacos', '🍅 Tomato Soup']);
    });

    it('clears the visible text so the input starts blank even when a meal was already selected', async () => {
      const { fixture, compiled } = await setup({ mealId: 'meal-2' });

      openDropdown(fixture);

      expect(textInput(compiled).value).toBe('');
    });
  });

  describe('filtering', () => {
    it('filters the option list to meals whose name contains the query, case-insensitively', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, 'TAC');

      expect(optionLabels(compiled)).toEqual(['Not planned', '🌮 Tacos']);
    });

    it('matches on a substring anywhere in the name, not only a prefix', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, 'soup');

      expect(optionLabels(compiled)).toEqual(['Not planned', '🍅 Tomato Soup']);
    });

    it('shows every meal again once the query is cleared', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, 'tac');
      typeQuery(fixture, '');

      expect(optionButtons(compiled)).toHaveLength(4);
    });

    it('shows a "no matches" message and no meal options when nothing matches', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, 'pizza');

      expect(optionLabels(compiled)).toEqual(['Not planned']);
      expect(compiled.textContent).toContain('No meals match.');
    });

    it('ignores leading/trailing whitespace in the query', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, '  taco  ');

      expect(optionLabels(compiled)).toEqual(['Not planned', '🌮 Tacos']);
    });
  });

  describe('selecting a meal', () => {
    it('emits the id of the clicked meal', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      clickOption(fixture, 'Tacos');

      expect(onMealIdChange).toHaveBeenCalledExactlyOnceWith('meal-2');
    });

    it('closes the dropdown after selecting a meal', async () => {
      const { fixture, compiled } = await setup({ mealId: '' });

      openDropdown(fixture);
      clickOption(fixture, 'Tacos');

      expect(compiled.querySelector('ul')).toBeFalsy();
    });

    it('emits an empty string when choosing "not planned"', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: 'meal-1' });

      openDropdown(fixture);
      clickOption(fixture, 'Not planned');

      expect(onMealIdChange).toHaveBeenCalledExactlyOnceWith('');
    });

    it('does not emit when re-selecting the already-selected meal', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: 'meal-2' });

      openDropdown(fixture);
      clickOption(fixture, 'Tacos');

      expect(onMealIdChange).not.toHaveBeenCalled();
    });

    it('does not emit when choosing "not planned" while already unselected', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      clickOption(fixture, 'Not planned');

      expect(onMealIdChange).not.toHaveBeenCalled();
    });

    it('selects a meal that only appears after filtering', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      typeQuery(fixture, 'soup');
      clickOption(fixture, 'Tomato Soup');

      expect(onMealIdChange).toHaveBeenCalledExactlyOnceWith('meal-3');
    });
  });

  describe('keyboard interaction', () => {
    it('Escape closes the dropdown without emitting', async () => {
      const { fixture, compiled, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      pressKey(fixture, 'Escape');

      expect(compiled.querySelector('ul')).toBeFalsy();
      expect(onMealIdChange).not.toHaveBeenCalled();
    });

    it('Enter selects the first meal in the (unfiltered) list', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      pressKey(fixture, 'Enter');

      expect(onMealIdChange).toHaveBeenCalledExactlyOnceWith('meal-1');
    });

    it('Enter selects the first filtered match, not the first meal in the full list', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      // "o" excludes "Pancakes" (the first meal overall) but matches "Tacos" and "Tomato Soup", in
      // that list order -- so the first *filtered* match is Tacos, not Pancakes.
      typeQuery(fixture, 'o');
      pressKey(fixture, 'Enter');

      expect(onMealIdChange).toHaveBeenCalledExactlyOnceWith('meal-2');
    });

    it('Enter does nothing when the filter matches no meal', async () => {
      const { fixture, onMealIdChange } = await setup({ mealId: '' });

      openDropdown(fixture);
      typeQuery(fixture, 'pizza');
      pressKey(fixture, 'Enter');

      expect(onMealIdChange).not.toHaveBeenCalled();
    });
  });

  describe('clicking outside', () => {
    it('closes the dropdown when clicking the backdrop overlay', async () => {
      const { fixture, compiled } = await setup({ mealId: '' });

      openDropdown(fixture);
      const backdrop = compiled.querySelector('.fixed.inset-0') as HTMLElement;
      expect(backdrop).toBeTruthy();

      backdrop.click();
      fixture.detectChanges();

      expect(compiled.querySelector('ul')).toBeFalsy();
    });
  });
});
