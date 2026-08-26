import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { toIsoDate, todayIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { Meal, MealPlanEntry, MealplanScope, MealSlot, MealplansService } from '../../../../core/mealplans.service';
import { AssignMealplan } from './assign-mealplan';

describe('AssignMealplan', () => {
  const familyScope: MealplanScope = { kind: 'family', childId: 'child-1' };
  const groupManageScope: MealplanScope = { kind: 'group', groupId: 'group-1', groupName: 'The Fam', accessTier: 2 };
  const groupViewScope: MealplanScope = { kind: 'group', groupId: 'group-1', groupName: 'The Fam', accessTier: 3 };

  const today = todayIsoDate();

  // Mirrors the component's own local-timezone day arithmetic (see parseIsoDate/buildDays in
  // assign-mealplan.ts) so tests can compute the exact date a "next/previous week" navigation or
  // a past-day check should land on, without depending on UTC-vs-local edge cases.
  function addDays(isoDate: string, offset: number): string {
    const [year, month, day] = isoDate.split('-').map(Number);
    return toIsoDate(new Date(year, month - 1, day + offset));
  }

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

  function entry(overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date: today,
      slot: 0,
      mealId: 'meal-1',
      mealName: 'Pancakes',
      icon: '🥞',
      color: '#fff',
      rating: null,
      notes: null,
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides
    };
  }

  interface Stubs {
    scope?: MealplanScope;
    meals?: Meal[];
    mealplans?: Partial<MealplansService>;
    guardians?: Partial<GuardiansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const mealsState = signal<Meal[]>(stubs.meals ?? [pancakes, tacos]);
    const mealplansStub: Partial<MealplansService> = {
      meals: mealsState.asReadonly(),
      listMeals: vi.fn(async () => mealsState()),
      listMealPlan: vi.fn(async () => []),
      assignMealToSlot: vi.fn(async (_scope, date, slot, mealId) => {
        const matched = mealsState().find((candidate) => candidate.id === mealId);
        return entry({ date, slot, mealId, mealName: matched?.name ?? mealId, icon: matched?.icon ?? '🍽️' });
      }),
      clearMealSlot: vi.fn(async () => undefined),
      ...stubs.mealplans
    };
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      ...stubs.guardians
    };

    await TestBed.configureTestingModule({
      imports: [AssignMealplan],
      providers: [
        { provide: MealplansService, useValue: mealplansStub },
        { provide: GuardiansService, useValue: guardiansStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AssignMealplan);
    fixture.componentRef.setInput('scope', stubs.scope ?? familyScope);

    return { fixture, mealplans: mealplansStub, guardians: guardiansStub };
  }

  // The app runs zoneless with these services stubbed directly (not HttpClient), so no
  // PendingTasks entry is registered for the mocked promise chains and fixture.whenStable()
  // resolves immediately without waiting for them -- see docs/testing.md. A macrotask flush
  // reliably drains any depth of chained awaits (including a Promise.all of two mocked calls).
  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  // Every grid cell's cdkDropList div carries `id="${date}|${slot}"` (see assign-mealplan.html),
  // which uniquely locates one of the 7x4 app-meal-picker instances without depending on DOM order.
  function cell(compiled: HTMLElement, date: string, slot: MealSlot): HTMLElement {
    return compiled.querySelector(`[id="${date}|${slot}"]`) as HTMLElement;
  }

  function pickerInput(compiled: HTMLElement, date: string, slot: MealSlot): HTMLInputElement {
    return cell(compiled, date, slot).querySelector('input')!;
  }

  // Unlike a direct MealPicker unit test (already mid-CD-cycle from its own setup), a raw
  // dispatchEvent inside this host component's test isn't guaranteed to be followed by a render
  // pass before the next synchronous line runs, so an explicit detectChanges() makes the opened
  // dropdown's <ul> actually show up in the DOM before it's queried.
  function openPicker(fixture: ComponentFixture<unknown>, date: string, slot: MealSlot): void {
    pickerInput(fixture.nativeElement as HTMLElement, date, slot).dispatchEvent(new Event('focus'));
    fixture.detectChanges();
  }

  function mealOption(compiled: HTMLElement, date: string, slot: MealSlot, label: string): HTMLButtonElement {
    return Array.from(cell(compiled, date, slot).querySelectorAll<HTMLButtonElement>('ul li button')).find((button) =>
      button.textContent?.includes(label)
    )!;
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  interface SlotRef {
    date: string;
    slot: MealSlot;
  }

  // onMealDrop is a `protected` handler wired to (cdkDropListDropped) and reads only
  // event.item.data/event.container.data, so a minimal object covering those two fields (rather
  // than a real pointer-driven CDK drag sequence, impractical to simulate in jsdom) is enough to
  // exercise it directly.
  interface AssignMealplanInternals {
    onMealDrop(event: CdkDragDrop<SlotRef>): Promise<void>;
  }

  function internals(fixture: ComponentFixture<AssignMealplan>): AssignMealplanInternals {
    return fixture.componentInstance as unknown as AssignMealplanInternals;
  }

  function dragEvent(source: SlotRef, target: SlotRef): CdkDragDrop<SlotRef> {
    return { item: { data: source }, container: { data: target } } as unknown as CdkDragDrop<SlotRef>;
  }

  it('shows the loading message before the plan and meal library resolve', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading the meal plan…');
  });

  it('shows the "no meals" state once loading finishes with an empty meal library', async () => {
    const { fixture } = await setup({ meals: [] });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Add a meal below before you can plan a week.');
  });

  it('treats a meal library containing only archived meals as empty', async () => {
    const { fixture } = await setup({ meals: [meal({ isArchived: true })] });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Add a meal below before you can plan a week.');
    expect(compiled.querySelector('table')).toBeFalsy();
  });

  it('shows the translated error message when loading the plan fails, while still rendering the grid', async () => {
    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load the meal plan.');
    expect(pickerInput(compiled, today, 0)).toBeTruthy();
  });

  it('shows the translated error message when loading the meal library fails', async () => {
    const { fixture } = await setup({ mealplans: { listMeals: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    // The meal library shown in the grid comes straight from the shared mealplans.meals() signal
    // (independent of whether the listMeals() call that was supposed to refresh it succeeded), so
    // the grid still renders with the pre-seeded meals -- only the error banner should differ.
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Unable to load the meal plan.');
  });

  it('requests the plan for the current 7-day window starting today', async () => {
    const { fixture, mealplans } = await setup();
    await settle(fixture);

    expect(mealplans.listMealPlan).toHaveBeenCalledWith(familyScope, today, addDays(today, 6));
    expect(mealplans.listMeals).toHaveBeenCalledWith(familyScope);
  });

  describe('assigning a meal', () => {
    it('calls assignMealToSlot with the scope, date, slot and mealId, and reflects the result', async () => {
      const { fixture, mealplans } = await setup();
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      openPicker(fixture, today, 1);
      mealOption(compiled, today, 1, 'Tacos').click();
      await settle(fixture);

      expect(mealplans.assignMealToSlot).toHaveBeenCalledExactlyOnceWith(familyScope, today, 1, 'meal-2');
      expect(pickerInput(compiled, today, 1).value).toBe('🌮 Tacos');
    });

    it('does not send notes -- the grid has no control for entering them', async () => {
      const { fixture, mealplans } = await setup();
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      openPicker(fixture, today, 0);
      mealOption(compiled, today, 0, 'Pancakes').click();
      await settle(fixture);

      // assignMealToSlot's 5th (notes) parameter is simply never passed by the caller here, so
      // the mock only ever observes 4 arguments -- pinned explicitly since it's easy to lose this
      // behavior by accident if a "notes" affordance is later bolted onto the call.
      expect(mealplans.assignMealToSlot).toHaveBeenCalledExactlyOnceWith(familyScope, today, 0, 'meal-1');
      expect((mealplans.assignMealToSlot as ReturnType<typeof vi.fn>).mock.calls[0]).toHaveLength(4);
    });

    it('shows a translated error and leaves the slot unassigned when assignMealToSlot rejects', async () => {
      const { fixture, mealplans } = await setup({
        mealplans: { assignMealToSlot: vi.fn(async () => Promise.reject(new Error('boom'))) }
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      openPicker(fixture, today, 2);
      mealOption(compiled, today, 2, 'Tacos').click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to update the meal plan. Please try again.');
      expect(pickerInput(compiled, today, 2).value).toBe('');
    });
  });

  describe('clearing an assignment', () => {
    it('calls clearMealSlot with the scope, date and slot, and removes the entry', async () => {
      const preassigned = entry({ date: today, slot: 0, mealId: 'meal-1', mealName: 'Pancakes', icon: '🥞' });
      const { fixture, mealplans } = await setup({ mealplans: { listMealPlan: vi.fn(async () => [preassigned]) } });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;
      expect(pickerInput(compiled, today, 0).value).toBe('🥞 Pancakes');

      openPicker(fixture, today, 0);
      mealOption(compiled, today, 0, 'Not planned').click();
      await settle(fixture);

      expect(mealplans.clearMealSlot).toHaveBeenCalledExactlyOnceWith(familyScope, today, 0);
      expect(pickerInput(compiled, today, 0).value).toBe('');
    });

    it('shows a translated error and leaves the entry in place when clearMealSlot rejects', async () => {
      const preassigned = entry({ date: today, slot: 0, mealId: 'meal-1', mealName: 'Pancakes', icon: '🥞' });
      const { fixture } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => [preassigned]), clearMealSlot: vi.fn(async () => Promise.reject(new Error('boom'))) }
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      openPicker(fixture, today, 0);
      mealOption(compiled, today, 0, 'Not planned').click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to update the meal plan. Please try again.');
      expect(pickerInput(compiled, today, 0).value).toBe('🥞 Pancakes');
    });
  });

  describe('date range navigation', () => {
    it('moves the visible week forward by 7 days and re-fetches that range', async () => {
      const { fixture, mealplans } = await setup();
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Next week →')!.click();
      await settle(fixture);

      expect(mealplans.listMealPlan).toHaveBeenLastCalledWith(familyScope, addDays(today, 7), addDays(today, 13));
    });

    it('moves the visible week backward by 7 days and re-fetches that range', async () => {
      const { fixture, mealplans } = await setup();
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, '← Previous week')!.click();
      await settle(fixture);

      expect(mealplans.listMealPlan).toHaveBeenLastCalledWith(familyScope, addDays(today, -7), addDays(today, -1));
    });

    it('marks a day before today as past and disables its meal picker', async () => {
      const { fixture } = await setup();
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, '← Previous week')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('(past)');
      expect(pickerInput(compiled, addDays(today, -7), 0).disabled).toBe(true);
    });

    it('refuses to open the picker (and never calls assignMealToSlot) for a past day', async () => {
      const { fixture, mealplans } = await setup();
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, '← Previous week')!.click();
      await settle(fixture);

      const pastDate = addDays(today, -7);
      openPicker(fixture, pastDate, 0);

      expect(cell(compiled, pastDate, 0).querySelector('ul')).toBeFalsy();
      expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
    });
  });

  describe('scope handling', () => {
    it('is not read-only for a family scope', async () => {
      const { fixture } = await setup({ scope: familyScope });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      expect(compiled.textContent).not.toContain('You have read-only access to this plan.');
      expect(pickerInput(compiled, today, 0).disabled).toBe(false);
    });

    it('is not read-only for a group scope with Manage access', async () => {
      const { fixture } = await setup({ scope: groupManageScope });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      expect(compiled.textContent).not.toContain('You have read-only access to this plan.');
      expect(pickerInput(compiled, today, 0).disabled).toBe(false);
    });

    it('is read-only for a group scope with only View access, disabling every picker', async () => {
      const { fixture, mealplans } = await setup({ scope: groupViewScope });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      expect(compiled.textContent).toContain('You have read-only access to this plan.');
      expect(pickerInput(compiled, today, 0).disabled).toBe(true);

      openPicker(fixture, today, 0);
      expect(cell(compiled, today, 0).querySelector('ul')).toBeFalsy();
      expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
    });

    it('requests the group-scoped plan and meal library rather than a family-scoped one', async () => {
      const { fixture, mealplans } = await setup({ scope: groupManageScope });
      await settle(fixture);

      expect(mealplans.listMealPlan).toHaveBeenCalledWith(groupManageScope, today, addDays(today, 6));
      expect(mealplans.listMeals).toHaveBeenCalledWith(groupManageScope);
    });

    it('re-fetches with the new scope when the scope input changes', async () => {
      const { fixture, mealplans } = await setup({ scope: familyScope });
      await settle(fixture);

      fixture.componentRef.setInput('scope', groupManageScope);
      await settle(fixture);

      expect(mealplans.listMealPlan).toHaveBeenLastCalledWith(groupManageScope, today, addDays(today, 6));
    });
  });

  describe('sibling ratings on past days', () => {
    it("resolves a rating's child name for a past day in family scope", async () => {
      const children: ChildSummary[] = [
        { id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, guardianLinkId: 'link-1', kind: 0, language: 'en' }
      ];

      const { fixture } = await setup({
        mealplans: {
          listMealPlan: vi.fn(async (_scope, from: string) => [
            entry({
              date: from,
              slot: 0,
              mealId: 'meal-1',
              allRatings: [{ childId: 'child-1', stars: 4, comment: 'Yum', ratedAt: '2026-01-01T00:00:00Z' }]
            })
          ])
        },
        guardians: { listMyChildren: vi.fn(async () => children) }
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, '← Previous week')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Sam:');
      expect(compiled.textContent).toContain('“Yum”');
    });

    it('falls back to the raw child id when the name cannot be resolved', async () => {
      const { fixture } = await setup({
        mealplans: {
          listMealPlan: vi.fn(async (_scope, from: string) => [
            entry({ date: from, slot: 0, mealId: 'meal-1', allRatings: [{ childId: 'unresolved-child', stars: 3, comment: null, ratedAt: '2026-01-01T00:00:00Z' }] })
          ])
        },
        guardians: { listMyChildren: vi.fn(async () => []) }
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, '← Previous week')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('unresolved-child:');
    });

    it('does not show ratings for a future day', async () => {
      const futureDate = addDays(today, 1);
      const { fixture } = await setup({
        mealplans: {
          listMealPlan: vi.fn(async () => [
            entry({ date: futureDate, slot: 0, mealId: 'meal-1', allRatings: [{ childId: 'child-1', stars: 4, comment: null, ratedAt: '2026-01-01T00:00:00Z' }] })
          ])
        }
      });
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).querySelector('ul.mt-1')).toBeFalsy();
    });

    it('does not show ratings for a group scope even on a past day', async () => {
      const { fixture } = await setup({
        scope: groupManageScope,
        mealplans: {
          listMealPlan: vi.fn(async (_scope, from: string) => [
            entry({ date: from, slot: 0, mealId: 'meal-1', allRatings: [{ childId: 'child-1', stars: 4, comment: null, ratedAt: '2026-01-01T00:00:00Z' }] })
          ])
        }
      });
      await settle(fixture);
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, '← Previous week')!.click();
      await settle(fixture);

      expect(compiled.querySelector('ul.mt-1')).toBeFalsy();
    });
  });

  describe('dragging a meal between cells', () => {
    it('moves a meal onto an empty cell: assigns the target then clears the source', async () => {
      const source: SlotRef = { date: today, slot: 0 };
      const target: SlotRef = { date: addDays(today, 1), slot: 1 };
      const sourceEntry = entry({ date: source.date, slot: source.slot, mealId: 'meal-1', mealName: 'Pancakes', icon: '🥞' });

      const { fixture, mealplans } = await setup({ mealplans: { listMealPlan: vi.fn(async () => [sourceEntry]) } });
      await settle(fixture);

      await internals(fixture).onMealDrop(dragEvent(source, target));
      await settle(fixture);

      expect(mealplans.assignMealToSlot).toHaveBeenCalledExactlyOnceWith(familyScope, target.date, target.slot, 'meal-1');
      expect(mealplans.clearMealSlot).toHaveBeenCalledExactlyOnceWith(familyScope, source.date, source.slot);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(pickerInput(compiled, target.date, target.slot).value).toBe('🥞 Pancakes');
      expect(pickerInput(compiled, source.date, source.slot).value).toBe('');
    });

    it('swaps two occupied cells rather than clearing either', async () => {
      const source: SlotRef = { date: today, slot: 0 };
      const target: SlotRef = { date: today, slot: 1 };
      const sourceEntry = entry({ date: source.date, slot: source.slot, mealId: 'meal-1', mealName: 'Pancakes', icon: '🥞' });
      const targetEntry = entry({ date: target.date, slot: target.slot, mealId: 'meal-2', mealName: 'Tacos', icon: '🌮' });

      const { fixture, mealplans } = await setup({ mealplans: { listMealPlan: vi.fn(async () => [sourceEntry, targetEntry]) } });
      await settle(fixture);

      await internals(fixture).onMealDrop(dragEvent(source, target));
      await settle(fixture);

      expect(mealplans.clearMealSlot).not.toHaveBeenCalled();
      expect(mealplans.assignMealToSlot).toHaveBeenCalledTimes(2);
      // Sequential, target first then source -- see the comment on onMealDrop in assign-mealplan.ts
      // about why these two writes are awaited one at a time rather than via Promise.all.
      expect(mealplans.assignMealToSlot).toHaveBeenNthCalledWith(1, familyScope, target.date, target.slot, 'meal-1');
      expect(mealplans.assignMealToSlot).toHaveBeenNthCalledWith(2, familyScope, source.date, source.slot, 'meal-2');

      const compiled = fixture.nativeElement as HTMLElement;
      expect(pickerInput(compiled, target.date, target.slot).value).toBe('🥞 Pancakes');
      expect(pickerInput(compiled, source.date, source.slot).value).toBe('🌮 Tacos');
    });

    it('does nothing when dropped back onto the same cell', async () => {
      const source: SlotRef = { date: today, slot: 0 };
      const { fixture, mealplans } = await setup({
        mealplans: { listMealPlan: vi.fn(async () => [entry({ date: today, slot: 0, mealId: 'meal-1' })]) }
      });
      await settle(fixture);

      await internals(fixture).onMealDrop(dragEvent(source, source));

      expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
      expect(mealplans.clearMealSlot).not.toHaveBeenCalled();
    });

    it('does nothing when the source cell has no meal assigned', async () => {
      const source: SlotRef = { date: today, slot: 0 };
      const target: SlotRef = { date: today, slot: 1 };
      const { fixture, mealplans } = await setup(); // default listMealPlan returns no entries

      await settle(fixture);

      await internals(fixture).onMealDrop(dragEvent(source, target));

      expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
    });

    it('does nothing when either side of the drag is a past day', async () => {
      const pastDate = addDays(today, -7);
      const source: SlotRef = { date: pastDate, slot: 0 };
      const target: SlotRef = { date: pastDate, slot: 1 };
      const pastEntry = entry({ date: pastDate, slot: 0, mealId: 'meal-1' });

      const { fixture, mealplans } = await setup({ mealplans: { listMealPlan: vi.fn(async () => [pastEntry]) } });
      await settle(fixture);
      findButtonByText(fixture.nativeElement as HTMLElement, '← Previous week')!.click();
      await settle(fixture);

      await internals(fixture).onMealDrop(dragEvent(source, target));

      expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
    });

    it('does nothing when the scope is read-only', async () => {
      const source: SlotRef = { date: today, slot: 0 };
      const target: SlotRef = { date: today, slot: 1 };
      const sourceEntry = entry({ date: source.date, slot: source.slot, mealId: 'meal-1' });

      const { fixture, mealplans } = await setup({ scope: groupViewScope, mealplans: { listMealPlan: vi.fn(async () => [sourceEntry]) } });
      await settle(fixture);

      await internals(fixture).onMealDrop(dragEvent(source, target));

      expect(mealplans.assignMealToSlot).not.toHaveBeenCalled();
    });
  });
});
