import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { GoalPost, ProgressService, ProgressSummary } from '../../../../core/progress.service';
import { ManageProgressGoals } from './manage-progress-goals';

describe('ManageProgressGoals', () => {
  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return { id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, guardianLinkId: 'link-1', kind: 0, language: 'en', timeZoneId: 'UTC', ...overrides };
  }

  function summary(overrides: Partial<ProgressSummary> = {}): ProgressSummary {
    return {
      totalStars: 0,
      unlockedMilestones: [],
      currentIcon: null,
      nextGoalThreshold: 5,
      nextGoalIcon: '🌱',
      goalPosts: [
        { threshold: 5, icon: '🌱', label: null },
        { threshold: 10, icon: '🌿', label: null }
      ],
      ...overrides
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    progress?: Partial<ProgressService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...stubs.guardians
    };
    const progressStub: Partial<ProgressService> = {
      getChildProgress: vi.fn(async () => summary()),
      configureGoalPosts: vi.fn(async (_childId: string, goalPosts: GoalPost[]) => summary({ goalPosts })),
      ...stubs.progress
    };

    await TestBed.configureTestingModule({
      imports: [ManageProgressGoals],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: ProgressService, useValue: progressStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageProgressGoals);

    return { fixture, guardians: guardiansStub, progress: progressStub };
  }

  // Mirrors manage-medicines.spec.ts's settle() -- loadChildren chains loadGoalPosts before the
  // signals driving the template settle.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function rowInputs(compiled: HTMLElement, index: number): { threshold: HTMLInputElement; icon: HTMLInputElement; label: HTMLInputElement } {
    return {
      threshold: compiled.querySelector<HTMLInputElement>(`input[name="goalThreshold${index}"]`)!,
      icon: compiled.querySelector<HTMLInputElement>(`input[name="goalIcon${index}"]`)!,
      label: compiled.querySelector<HTMLInputElement>(`input[name="goalLabel${index}"]`)!
    };
  }

  it('shows the loading message before children resolve', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading goal posts');
  });

  it('shows the no-children message when the guardian has no linked children', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings before configuring goal posts.');
  });

  it('shows a translated error when loading children fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Unable to load goal posts.');
  });

  it('loads the selected child’s current goal posts into editable rows', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const first = rowInputs(compiled, 0);
    const second = rowInputs(compiled, 1);

    expect(first.threshold.value).toBe('5');
    expect(first.icon.value).toBe('🌱');
    expect(second.threshold.value).toBe('10');
    expect(second.icon.value).toBe('🌿');
  });

  it('adds and removes goal post rows', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('input[type="number"]')).toHaveLength(2);

    findButtonByText(compiled, '+ Add another goal')!.click();
    fixture.detectChanges();
    expect(compiled.querySelectorAll('input[type="number"]')).toHaveLength(3);

    findButtonByText(compiled, 'Remove')!.click();
    fixture.detectChanges();
    expect(compiled.querySelectorAll('input[type="number"]')).toHaveLength(2);
  });

  it('disables save until every row has a positive threshold and a non-empty icon', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const submit = findButtonByText(compiled, 'Save goal posts')!;
    expect(submit.disabled).toBe(false);

    setInputValue(rowInputs(compiled, 0).icon, '');
    fixture.detectChanges();
    expect(submit.disabled).toBe(true);

    setInputValue(rowInputs(compiled, 0).icon, '🥇');
    fixture.detectChanges();
    expect(submit.disabled).toBe(false);

    setInputValue(rowInputs(compiled, 1).threshold, '0');
    fixture.detectChanges();
    expect(submit.disabled).toBe(true);
  });

  it('saves the edited rows, parsing thresholds and blanking empty labels to null', async () => {
    const { fixture, progress } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(rowInputs(compiled, 0).threshold, '3');
    fixture.detectChanges();

    findButtonByText(compiled, 'Save goal posts')!.click();
    await settle(fixture);

    expect(progress.configureGoalPosts).toHaveBeenCalledWith('child-1', [
      { threshold: 3, icon: '🌱', label: null },
      { threshold: 10, icon: '🌿', label: null }
    ]);
    expect(compiled.textContent).toContain('Goal posts saved.');
  });

  it('shows a translated error and keeps editing when saving fails', async () => {
    const { fixture } = await setup({ progress: { configureGoalPosts: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Save goal posts')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save goal posts.');
    expect(compiled.textContent).not.toContain('Goal posts saved.');
  });
});
