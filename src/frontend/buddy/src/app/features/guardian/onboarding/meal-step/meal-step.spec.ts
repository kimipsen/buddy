import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { addDaysIso, todayIsoDate } from '../../../../core/date-utils';
import { Meal, MealplansService } from '../../../../core/mealplans.service';
import {
  buttonByText,
  child,
  settle,
  setupWith,
  submit,
  typeInto,
} from '../../../../../testing/onboarding-fixture';
import { MealStep } from './meal-step';

function meal(overrides: Partial<Meal> = {}): Meal {
  return {
    id: 'meal-1',
    name: 'Pasta',
    description: '',
    icon: '🍝',
    color: '#f97316',
    isArchived: false,
    ratings: [],
    createdBy: 'me',
    lastModifiedBy: 'me',
    ...overrides,
  };
}

describe('MealStep', () => {
  async function setup(mealplans: Partial<MealplansService> = {}) {
    const stub: Partial<MealplansService> = {
      listMeals: vi.fn(async () => []),
      createMeal: vi.fn(async () => meal({ id: 'meal-new', name: 'Tacos' })),
      assignMealToSlot: vi.fn(async () => ({}) as never),
      getSharedGroup: vi.fn(async () => null),
      shareWithGroup: vi.fn(async () => {}),
      unshareFromGroup: vi.fn(async () => {}),
      ...mealplans,
    };

    await TestBed.configureTestingModule({
      imports: [MealStep],
      providers: [{ provide: MealplansService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealStep);
    fixture.componentRef.setInput('setup', setupWith({ children: [child()] }));
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, mealplans: stub, changed };
  }

  const family = { kind: 'family', childId: 'child-1' };

  async function addNewMeal(fixture: Awaited<ReturnType<typeof setup>>['fixture']) {
    const compiled = fixture.nativeElement as HTMLElement;
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingMealName')!, 'Tacos');
    await settle(fixture);
    submit(compiled);
    await settle(fixture);
  }

  it('creates a meal in the family library and plans it for dinner today', async () => {
    const { fixture, compiled, mealplans, changed } = await setup();

    await addNewMeal(fixture);

    expect(mealplans.createMeal).toHaveBeenCalledWith(family, {
      name: 'Tacos',
      description: '',
      icon: '🍽️',
      color: '#f97316',
    });
    expect(mealplans.assignMealToSlot).toHaveBeenCalledWith(
      family,
      todayIsoDate(),
      2,
      'meal-new',
      '',
    );
    expect(compiled.textContent).toContain('Planned: Tacos');
    expect(changed).toHaveBeenCalled();
  });

  it('assigns the meal it already created when the retry follows a failed assignment', async () => {
    const assignMealToSlot = vi
      .fn()
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }))
      .mockResolvedValue({});
    const { fixture, compiled, mealplans } = await setup({ assignMealToSlot });

    await addNewMeal(fixture);
    expect(compiled.textContent).toContain('Unable to add the meal to the plan.');

    submit(compiled);
    await settle(fixture);

    expect(mealplans.createMeal).toHaveBeenCalledTimes(1);
    expect(assignMealToSlot).toHaveBeenLastCalledWith(family, todayIsoDate(), 2, 'meal-new', '');
  });

  it('plans an existing meal without creating one', async () => {
    const { fixture, compiled, mealplans } = await setup({
      listMeals: vi.fn(async () => [meal()]),
    });

    expect(compiled.querySelector<HTMLSelectElement>('#onboardingMealChoice')!.value).toBe(
      'meal-1',
    );
    submit(compiled);
    await settle(fixture);

    expect(mealplans.createMeal).not.toHaveBeenCalled();
    expect(mealplans.assignMealToSlot).toHaveBeenCalledWith(
      family,
      todayIsoDate(),
      2,
      'meal-1',
      '',
    );
  });

  it('leaves group sharing off until the guardian turns it on', async () => {
    const { fixture, compiled, mealplans } = await setup();

    const toggle = compiled.querySelector<HTMLButtonElement>('button[role="switch"]')!;
    expect(toggle.getAttribute('aria-checked')).toBe('false');
    expect(toggle.getAttribute('aria-label')).toBe('Share the meal plan with The Hansens');

    toggle.click();
    await settle(fixture);

    expect(mealplans.shareWithGroup).toHaveBeenCalledWith('child-1', 'group-1');
    expect(mealplans.getSharedGroup).toHaveBeenCalledTimes(2);
  });

  it('shows the meal of the day choices', async () => {
    const { compiled } = await setup();

    expect(buttonByText(compiled, 'Breakfast')).toBeDefined();
    const slots = compiled.querySelector('[role="radiogroup"][aria-label="Meal of the day"]')!;
    const checked = Array.from(slots.querySelectorAll('button[role="radio"]')).filter(
      (radio) => radio.getAttribute('aria-checked') === 'true',
    );
    expect(checked.map((radio) => radio.textContent?.trim())).toEqual(['Dinner']);
  });

  it('only plans within the window the guide can find the meal in again', async () => {
    const { fixture, compiled, mealplans } = await setup({
      listMeals: vi.fn(async () => [meal()]),
    });

    const component = fixture.componentInstance as unknown as { date: { set(v: string): void } };
    component.date.set(addDaysIso(todayIsoDate(), 30));
    await settle(fixture);
    submit(compiled);
    await settle(fixture);

    expect(compiled.textContent).toContain('Pick a date between today and three weeks ahead.');
    expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
  });

  it('keeps the meal the guardian picked when the library reloads', async () => {
    const listMeals = vi
      .fn()
      .mockResolvedValueOnce([meal(), meal({ id: 'meal-2', name: 'Soup' })])
      .mockResolvedValue([meal(), meal({ id: 'meal-2', name: 'Soup' }), meal({ id: 'meal-3' })]);
    const { fixture, compiled } = await setup({ listMeals });
    const choice = compiled.querySelector<HTMLSelectElement>('#onboardingMealChoice')!;

    typeInto(choice, 'meal-2');
    await settle(fixture);
    submit(compiled);
    await settle(fixture);

    expect(choice.value).toBe('meal-2');
  });
});
