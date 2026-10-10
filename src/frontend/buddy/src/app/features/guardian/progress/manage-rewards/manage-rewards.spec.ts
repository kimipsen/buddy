import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import {
  ProgressService,
  ProgressSummary,
  RewardDraft,
  RewardRequest,
} from '../../../../core/progress.service';
import { ManageRewards } from './manage-rewards';

function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
  return {
    id: 'child-1',
    name: { givenName: 'Sam', familyName: 'Kid' },
    guardianLinkId: 'link-1',
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'UTC',
    ...overrides,
  };
}

function request(id: string, overrides: Partial<RewardRequest> = {}): RewardRequest {
  return {
    id,
    rewardId: 'screen',
    name: 'Screen time',
    icon: '📱',
    cost: 3,
    status: 'Pending',
    requestedAt: '2026-10-01T10:00:00Z',
    resolvedAt: null,
    ...overrides,
  };
}

function summary(overrides: Partial<ProgressSummary> = {}): ProgressSummary {
  return {
    totalStars: 9,
    unlockedMilestones: [],
    displayIcon: '🌱',
    nextGoalThreshold: 10,
    nextGoalIcon: '🌿',
    goalPosts: [],
    spendableStars: 2,
    spentStars: 4,
    rewards: [{ id: 'screen', name: 'Screen time', icon: '📱', cost: 3 }],
    rewardRequests: [request('r1')],
    ...overrides,
  };
}

function conflict(code: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 409, error: { code } });
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

interface Stubs {
  guardians?: Partial<GuardiansService>;
  progress?: Partial<ProgressService>;
  // The ?child= query parameter, as the dashboard's "Rewards waiting" link sets it.
  childParam?: string;
}

async function setup(stubs: Stubs = {}) {
  const guardians: Partial<GuardiansService> = {
    listMyChildren: vi.fn(async () => [child()]),
    ...stubs.guardians,
  };
  const progress: Partial<ProgressService> = {
    getChildProgress: vi.fn(async () => summary()),
    configureRewards: vi.fn(async (_childId: string, rewards: RewardDraft[]) =>
      summary({
        rewards: rewards.map((r, i) => ({
          id: r.id ?? `new-${i}`,
          name: r.name,
          icon: r.icon,
          cost: r.cost,
        })),
      }),
    ),
    approveRewardRequest: vi.fn(async () =>
      summary({ spentStars: 7, rewardRequests: [request('r1', { status: 'Approved' })] }),
    ),
    declineRewardRequest: vi.fn(async () =>
      summary({ rewardRequests: [request('r1', { status: 'Declined' })] }),
    ),
    ...stubs.progress,
  };

  await TestBed.configureTestingModule({
    imports: [ManageRewards],
    providers: [
      { provide: GuardiansService, useValue: guardians },
      { provide: ProgressService, useValue: progress },
      ...(stubs.childParam
        ? [
            {
              provide: ActivatedRoute,
              useValue: {
                snapshot: { queryParamMap: convertToParamMap({ child: stubs.childParam }) },
              },
            },
          ]
        : []),
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(ManageRewards);
  await settle(fixture);

  return { fixture, compiled: fixture.nativeElement as HTMLElement, guardians, progress };
}

function buttonIn(element: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(element.querySelectorAll('button')).find(
    (button) => button.textContent?.trim() === text,
  );
}

function setInputValue(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function input(compiled: HTMLElement, name: string): HTMLInputElement {
  return compiled.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
}

function pendingRow(compiled: HTMLElement, id: string): HTMLElement {
  return compiled.querySelector<HTMLElement>(`[data-request-id="${id}"]`)!;
}

describe('ManageRewards', () => {
  it("loads the first child's balance, pending requests and catalog", async () => {
    const { compiled, progress } = await setup();

    expect(progress.getChildProgress).toHaveBeenCalledWith('child-1');
    expect(compiled.querySelector('[data-testid="reward-balance"]')?.textContent).toContain(
      '2 stars to spend, 4 spent, 9 earned in all.',
    );
    expect(pendingRow(compiled, 'r1').textContent).toContain('Screen time');
    expect(pendingRow(compiled, 'r1').textContent).toContain('3 stars');
    expect(input(compiled, 'rewardName0').value).toBe('Screen time');
    expect(input(compiled, 'rewardCost0').value).toBe('3');
  });

  it('says so when no requests are waiting', async () => {
    const { compiled } = await setup({
      progress: { getChildProgress: vi.fn(async () => summary({ rewardRequests: [] })) },
    });

    expect(compiled.textContent).toContain('No reward requests are waiting.');
  });

  it('shows the no-children message when the guardian has no linked children', async () => {
    const { compiled } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });

    expect(compiled.textContent).toContain('Link a child from Settings before adding rewards.');
  });

  it('shows a load error', async () => {
    const { compiled } = await setup({
      progress: {
        getChildProgress: vi.fn(async () => {
          throw new Error('boom');
        }),
      },
    });

    expect(compiled.textContent).toContain('Unable to load rewards.');
  });

  it('approves a request and shows the new balance and outcome', async () => {
    const { fixture, compiled, progress } = await setup();

    buttonIn(pendingRow(compiled, 'r1'), 'Approve')!.click();
    await settle(fixture);

    expect(progress.approveRewardRequest).toHaveBeenCalledWith('child-1', 'r1');
    expect(compiled.querySelector('[data-request-id="r1"]')).toBeNull();
    expect(compiled.textContent).toContain('7 spent');
    expect(compiled.textContent).toContain('Approved');
  });

  it('declines a request', async () => {
    const { fixture, compiled, progress } = await setup();

    buttonIn(pendingRow(compiled, 'r1'), 'Decline')!.click();
    await settle(fixture);

    expect(progress.declineRewardRequest).toHaveBeenCalledWith('child-1', 'r1');
    expect(compiled.textContent).toContain('Declined');
  });

  it('keeps unsaved catalog edits when a request is answered', async () => {
    const { fixture, compiled } = await setup();
    setInputValue(input(compiled, 'rewardName0'), 'Edited name');
    await settle(fixture);

    buttonIn(pendingRow(compiled, 'r1'), 'Approve')!.click();
    await settle(fixture);

    expect(input(compiled, 'rewardName0').value).toBe('Edited name');
  });

  it('explains an approval refused for too few stars, without reloading', async () => {
    const { fixture, compiled, progress } = await setup({
      progress: {
        approveRewardRequest: vi.fn(async () => {
          throw conflict('insufficient_stars');
        }),
      },
    });

    buttonIn(pendingRow(compiled, 'r1'), 'Approve')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'There are no longer enough stars for this reward.',
    );
    expect(progress.getChildProgress).toHaveBeenCalledTimes(1);
  });

  it('reloads when the request was already handled elsewhere', async () => {
    const getChildProgress = vi
      .fn()
      .mockResolvedValueOnce(summary())
      .mockResolvedValue(summary({ rewardRequests: [request('r1', { status: 'Cancelled' })] }));
    const { fixture, compiled } = await setup({
      progress: {
        getChildProgress,
        declineRewardRequest: vi.fn(async () => {
          throw conflict('reward_request_resolved');
        }),
      },
    });

    buttonIn(pendingRow(compiled, 'r1'), 'Decline')!.click();
    await settle(fixture);

    expect(getChildProgress).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('[data-request-id="r1"]')).toBeNull();
    expect(compiled.textContent).toContain('Withdrawn');
    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'This request was already handled',
    );
  });

  it('shows a generic error when answering fails', async () => {
    const { fixture, compiled } = await setup({
      progress: {
        approveRewardRequest: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 500 });
        }),
      },
    });

    buttonIn(pendingRow(compiled, 'r1'), 'Approve')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'Unable to update this request.',
    );
  });

  it('saves the catalog, keeping existing ids and leaving them off new rows', async () => {
    const { fixture, compiled, progress } = await setup();

    buttonIn(compiled, '+ Add a reward')!.click();
    await settle(fixture);
    setInputValue(input(compiled, 'rewardName1'), '  Choose dinner ');
    setInputValue(input(compiled, 'rewardIcon1'), '🍕');
    setInputValue(input(compiled, 'rewardCost1'), '8');
    await settle(fixture);

    buttonIn(compiled, 'Save rewards')!.click();
    await settle(fixture);

    expect(progress.configureRewards).toHaveBeenCalledWith('child-1', [
      { id: 'screen', name: 'Screen time', icon: '📱', cost: 3 },
      { name: 'Choose dinner', icon: '🍕', cost: 8 },
    ]);
    expect(compiled.textContent).toContain('Rewards saved.');
  });

  it('saves an empty catalog after removing every reward', async () => {
    const { fixture, compiled, progress } = await setup();

    buttonIn(compiled, 'Remove')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('No rewards yet.');

    buttonIn(compiled, 'Save rewards')!.click();
    await settle(fixture);

    expect(progress.configureRewards).toHaveBeenCalledWith('child-1', []);
  });

  it('disables Save while a row is incomplete', async () => {
    const { fixture, compiled } = await setup();

    setInputValue(input(compiled, 'rewardCost0'), '0');
    await settle(fixture);
    expect(buttonIn(compiled, 'Save rewards')!.disabled).toBe(true);

    setInputValue(input(compiled, 'rewardCost0'), '2');
    setInputValue(input(compiled, 'rewardName0'), '   ');
    await settle(fixture);
    expect(buttonIn(compiled, 'Save rewards')!.disabled).toBe(true);

    setInputValue(input(compiled, 'rewardName0'), 'Screen time');
    await settle(fixture);
    expect(buttonIn(compiled, 'Save rewards')!.disabled).toBe(false);
  });

  it('shows a save error', async () => {
    const { fixture, compiled } = await setup({
      progress: {
        configureRewards: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 400 });
        }),
      },
    });

    buttonIn(compiled, 'Save rewards')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save rewards.');
  });

  it("switches to another child's rewards", async () => {
    const getChildProgress = vi.fn(async (childId: string) =>
      childId === 'child-2'
        ? summary({
            rewards: [{ id: 'bike', name: 'Bike ride', icon: '🚲', cost: 4 }],
            rewardRequests: [],
          })
        : summary(),
    );
    const { fixture, compiled } = await setup({
      guardians: {
        listMyChildren: vi.fn(async () => [
          child(),
          child({ id: 'child-2', name: { givenName: 'Ida', familyName: 'Kid' } }),
        ]),
      },
      progress: { getChildProgress },
    });

    const select = compiled.querySelector<HTMLSelectElement>('#rewardsChildId')!;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(getChildProgress).toHaveBeenLastCalledWith('child-2');
    expect(input(compiled, 'rewardName0').value).toBe('Bike ride');
    expect(compiled.textContent).toContain('No reward requests are waiting.');
  });

  const twoChildren = (): Partial<GuardiansService> => ({
    listMyChildren: vi.fn(async () => [
      child(),
      child({ id: 'child-2', name: { givenName: 'Ida', familyName: 'Kid' } }),
    ]),
  });

  function selectChild(compiled: HTMLElement, index: number): void {
    const select = compiled.querySelector<HTMLSelectElement>('#rewardsChildId')!;
    select.value = select.options[index].value;
    select.dispatchEvent(new Event('change'));
  }

  it('offers no editor or Save after a failed load, so it cannot save over the real catalog', async () => {
    const { compiled, progress } = await setup({
      progress: {
        getChildProgress: vi.fn(async () => {
          throw new Error('boom');
        }),
      },
    });

    expect(buttonIn(compiled, 'Save rewards')).toBeUndefined();
    expect(buttonIn(compiled, '+ Add a reward')).toBeUndefined();
    expect(progress.configureRewards).not.toHaveBeenCalled();
  });

  it('drops an unsaved draft when switching to a child with the same (empty) catalog', async () => {
    const { fixture, compiled } = await setup({
      guardians: twoChildren(),
      progress: {
        getChildProgress: vi.fn(async () => summary({ rewards: [], rewardRequests: [] })),
      },
    });

    buttonIn(compiled, '+ Add a reward')!.click();
    await settle(fixture);
    setInputValue(input(compiled, 'rewardName0'), 'Ice cream');
    await settle(fixture);

    selectChild(compiled, 1);
    await settle(fixture);

    expect(compiled.querySelector('input[name="rewardName0"]')).toBeNull();
    expect(compiled.textContent).toContain('No rewards yet.');
  });

  it('opens on the child named in ?child=', async () => {
    const getChildProgress = vi.fn(async () => summary());
    await setup({
      guardians: twoChildren(),
      progress: { getChildProgress },
      childParam: 'child-2',
    });

    expect(getChildProgress).toHaveBeenCalledWith('child-2');
    expect(getChildProgress).not.toHaveBeenCalledWith('child-1');
  });

  it('falls back to the first child when ?child= names no linked child', async () => {
    const getChildProgress = vi.fn(async () => summary());
    await setup({
      guardians: twoChildren(),
      progress: { getChildProgress },
      childParam: 'stranger',
    });

    expect(getChildProgress).toHaveBeenCalledWith('child-1');
  });

  it('disables Save for a fractional or too-large cost', async () => {
    const { fixture, compiled } = await setup();

    setInputValue(input(compiled, 'rewardCost0'), '1.5');
    await settle(fixture);
    expect(buttonIn(compiled, 'Save rewards')!.disabled).toBe(true);

    setInputValue(input(compiled, 'rewardCost0'), '10001');
    await settle(fixture);
    expect(buttonIn(compiled, 'Save rewards')!.disabled).toBe(true);

    setInputValue(input(compiled, 'rewardCost0'), '10000');
    await settle(fixture);
    expect(buttonIn(compiled, 'Save rewards')!.disabled).toBe(false);
  });

  it("doesn't show a failed save's error under the child switched to meanwhile", async () => {
    let failSave: (error: unknown) => void = () => undefined;
    const { fixture, compiled } = await setup({
      guardians: twoChildren(),
      progress: {
        configureRewards: vi.fn(
          () => new Promise<ProgressSummary>((_, reject) => (failSave = reject)),
        ),
      },
    });

    buttonIn(compiled, 'Save rewards')!.click();
    await settle(fixture);
    selectChild(compiled, 1);
    await settle(fixture);
    failSave(new HttpErrorResponse({ status: 500 }));
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Unable to save rewards.');
  });
});
