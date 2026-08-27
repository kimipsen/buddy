import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { MealPlanEntry, MealplansService, MealSlot } from '../../../core/mealplans.service';
import { MealplanToday } from './mealplan-today';

describe('MealplanToday', () => {
  const child: ChildSummary = {
    id: 'child-1',
    name: { givenName: 'Sam', familyName: 'Kid' },
    guardianLinkId: 'link-1',
    kind: 0,
    language: 'en',
    timeZoneId: 'UTC'
  };

  function entry(overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date: '2026-08-26',
      slot: 1,
      mealId: 'meal-1',
      mealName: 'Pancakes',
      icon: '🥞',
      color: '#000',
      rating: null,
      notes: null,
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    mealplans?: Partial<MealplansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child]),
      ...stubs.guardians
    };
    const mealplansStub: Partial<MealplansService> = {
      listMealPlan: vi.fn(async () => []),
      ...stubs.mealplans
    };

    await TestBed.configureTestingModule({
      imports: [MealplanToday],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: MealplansService, useValue: mealplansStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(MealplanToday);

    return { fixture, guardians: guardiansStub, mealplans: mealplansStub };
  }

  // loadPlan chains an await for listMyChildren followed by an await for listMealPlan before the
  // signals driving the template settle -- a single whenStable() flush isn't always enough, so
  // flush a generous fixed number of times rather than guessing when it's "probably" done.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  it('shows the loading spinner while the plan is loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the translated error message when loading the plan fails', async () => {
    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s meal plan.');
    expect(compiled.querySelector('app-loading-spinner')).toBeFalsy();
  });

  it('shows the translated error message when loading children fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s meal plan.');
  });

  it('shows the no-children message when the guardian has no linked children, without calling listMealPlan', async () => {
    const listMealPlan = vi.fn(async () => []);
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) }, mealplans: { listMealPlan } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings to see their meal plan.');
    expect(listMealPlan).not.toHaveBeenCalled();
  });

  it('requests the plan for the first linked child scoped to today only', async () => {
    const secondChild: ChildSummary = { ...child, id: 'child-2' };
    const listMealPlan = vi.fn(async () => []);
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child, secondChild]) }, mealplans: { listMealPlan } });
    await settle(fixture);

    const today = new Date().toISOString().slice(0, 10);
    expect(listMealPlan).toHaveBeenCalledWith({ kind: 'family', childId: 'child-1' }, today, today);
  });

  it('shows "Not planned" for every slot when nothing is planned today', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const notPlanned = compiled.textContent?.match(/Not planned/g) ?? [];
    expect(notPlanned).toHaveLength(4);
    expect(compiled.textContent).toContain('Breakfast');
    expect(compiled.textContent).toContain('Lunch');
    expect(compiled.textContent).toContain('Dinner');
    expect(compiled.textContent).toContain('Snack');
  });

  it('renders a planned meal in its slot with icon and name, leaving other slots not planned', async () => {
    const lunch = entry({ slot: 1, mealName: 'Pancakes', icon: '🥞' });
    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => [lunch]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pancakes');
    expect(compiled.textContent).toContain('🥞');

    const notPlanned = compiled.textContent?.match(/Not planned/g) ?? [];
    expect(notPlanned).toHaveLength(3);
  });

  it('renders every slot when all four are planned', async () => {
    const entries: MealPlanEntry[] = ([0, 1, 2, 3] as MealSlot[]).map((slot) =>
      entry({ slot, mealId: `meal-${slot}`, mealName: `Meal ${slot}`, icon: '🍽️' })
    );
    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => entries) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Not planned');
    for (const e of entries) {
      expect(compiled.textContent).toContain(e.mealName);
    }
  });

  it('links to the full meal plan page', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const link = compiled.querySelector('a[href="/guardian/mealplan"]');
    expect(link).toBeTruthy();
    expect(link?.textContent).toContain('Plan meals');
  });
});
