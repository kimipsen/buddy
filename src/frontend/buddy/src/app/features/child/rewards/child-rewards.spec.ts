import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import {
  ProgressService,
  ProgressSummary,
  Reward,
  RewardRequest,
} from '../../../core/progress.service';
import { ChildRewards } from './child-rewards';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function reward(id: string, name: string, cost: number): Reward {
  return { id, name, icon: '🎁', cost };
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
    spendableStars: 5,
    spentStars: 4,
    rewards: [reward('screen', 'Screen time', 3), reward('dinner', 'Choose dinner', 8)],
    rewardRequests: [],
    ...overrides,
  };
}

function conflict(code: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 409, error: { code } });
}

async function setup(progress: Partial<ProgressService> = {}) {
  const stub: Partial<ProgressService> = {
    getMyProgress: vi.fn(async () => summary()),
    requestReward: vi.fn(async () => summary()),
    cancelRewardRequest: vi.fn(async () => summary()),
    ...progress,
  };

  await TestBed.configureTestingModule({
    imports: [ChildRewards],
    providers: [provideRouter([]), { provide: ProgressService, useValue: stub }],
  }).compileComponents();

  const fixture = TestBed.createComponent(ChildRewards);
  await settle(fixture);

  return { fixture, compiled: fixture.nativeElement as HTMLElement, progress: stub };
}

function rewardCard(compiled: HTMLElement, id: string): HTMLElement {
  return compiled.querySelector<HTMLElement>(`[data-reward-id="${id}"]`)!;
}

function buttonIn(element: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(element.querySelectorAll('button')).find(
    (button) => button.textContent?.trim() === text,
  );
}

describe('ChildRewards', () => {
  it('shows the spendable balance and every reward with its cost', async () => {
    const { compiled } = await setup();

    expect(compiled.querySelector('[data-testid="reward-balance"]')?.textContent).toContain(
      '5 stars to spend',
    );
    expect(rewardCard(compiled, 'screen').textContent).toContain('Screen time');
    expect(rewardCard(compiled, 'screen').textContent).toContain('3 stars');
    expect(rewardCard(compiled, 'dinner').textContent).toContain('Choose dinner');
  });

  it('offers only affordable rewards, and says how many stars the others still need', async () => {
    const { compiled } = await setup();

    expect(buttonIn(rewardCard(compiled, 'screen'), 'Ask for this')).toBeDefined();
    expect(buttonIn(rewardCard(compiled, 'dinner'), 'Ask for this')).toBeUndefined();
    expect(rewardCard(compiled, 'dinner').textContent).toContain('3 more stars to go');
  });

  it('offers a reward that costs exactly the balance', async () => {
    const { compiled } = await setup({
      getMyProgress: vi.fn(async () => summary({ spendableStars: 8 })),
    });

    expect(buttonIn(rewardCard(compiled, 'dinner'), 'Ask for this')).toBeDefined();
  });

  it('shows an empty state when no rewards are set up', async () => {
    const { compiled } = await setup({
      getMyProgress: vi.fn(async () => summary({ rewards: [] })),
    });

    expect(compiled.textContent).toContain('There are no rewards yet.');
  });

  it('shows an error when progress fails to load', async () => {
    const { compiled } = await setup({
      getMyProgress: vi.fn(async () => {
        throw new Error('boom');
      }),
    });

    expect(compiled.textContent).toContain('Couldn’t load your rewards.');
    expect(compiled.querySelector('[data-testid="reward-balance"]')).toBeNull();
  });

  it('asks for a reward and shows the request as waiting', async () => {
    const { fixture, compiled, progress } = await setup({
      requestReward: vi.fn(async () =>
        summary({ spendableStars: 2, rewardRequests: [request('r1')] }),
      ),
    });

    buttonIn(rewardCard(compiled, 'screen'), 'Ask for this')!.click();
    await settle(fixture);

    expect(progress.requestReward).toHaveBeenCalledWith('screen');
    expect(compiled.textContent).toContain('2 stars to spend');
    const waiting = compiled.querySelector<HTMLElement>('[data-request-id="r1"]')!;
    expect(compiled.textContent).toContain('Waiting for a grown-up');
    expect(waiting.textContent).toContain('Screen time');
  });

  it('explains a refused request and reloads the balance when the stars ran out', async () => {
    const { fixture, compiled, progress } = await setup({
      requestReward: vi.fn(async () => {
        throw conflict('insufficient_stars');
      }),
    });

    buttonIn(rewardCard(compiled, 'screen'), 'Ask for this')!.click();
    await settle(fixture);

    expect(rewardCard(compiled, 'screen').textContent).toContain(
      'You don’t have enough stars for that yet.',
    );
    expect(progress.getMyProgress).toHaveBeenCalledTimes(2);
  });

  it('explains when too many requests are already waiting, without reloading', async () => {
    const { fixture, compiled, progress } = await setup({
      requestReward: vi.fn(async () => {
        throw conflict('too_many_pending_requests');
      }),
    });

    buttonIn(rewardCard(compiled, 'screen'), 'Ask for this')!.click();
    await settle(fixture);

    expect(rewardCard(compiled, 'screen').textContent).toContain(
      'You’re already waiting on lots of rewards.',
    );
    expect(progress.getMyProgress).toHaveBeenCalledTimes(1);
  });

  it('shows a generic error for any other failure', async () => {
    const { fixture, compiled } = await setup({
      requestReward: vi.fn(async () => {
        throw new HttpErrorResponse({ status: 500 });
      }),
    });

    buttonIn(rewardCard(compiled, 'screen'), 'Ask for this')!.click();
    await settle(fixture);

    expect(rewardCard(compiled, 'screen').textContent).toContain('That didn’t work. Try again.');
  });

  it('withdraws a waiting request', async () => {
    const { fixture, compiled, progress } = await setup({
      getMyProgress: vi.fn(async () => summary({ rewardRequests: [request('r1')] })),
      cancelRewardRequest: vi.fn(async () =>
        summary({ rewardRequests: [request('r1', { status: 'Cancelled' })] }),
      ),
    });
    const waiting = compiled.querySelector<HTMLElement>('[data-request-id="r1"]')!;

    buttonIn(waiting, 'Never mind')!.click();
    await settle(fixture);

    expect(progress.cancelRewardRequest).toHaveBeenCalledWith('r1');
    expect(compiled.querySelector('[data-request-id="r1"]')).toBeNull();
    expect(compiled.textContent).toContain('You took it back');
  });

  it('reloads and still shows the error when a request was answered before it was withdrawn', async () => {
    const { fixture, compiled, progress } = await setup({
      // The reload shows what really happened: a grown-up approved it first.
      getMyProgress: vi
        .fn()
        .mockResolvedValueOnce(summary({ rewardRequests: [request('r1')] }))
        .mockResolvedValue(summary({ rewardRequests: [request('r1', { status: 'Approved' })] })),
      cancelRewardRequest: vi.fn(async () => {
        throw conflict('reward_request_resolved');
      }),
    });

    buttonIn(compiled.querySelector<HTMLElement>('[data-request-id="r1"]')!, 'Never mind')!.click();
    await settle(fixture);

    expect(progress.getMyProgress).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('[data-request-id="r1"]')).toBeNull();
    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'Couldn’t take that back.',
    );
    expect(compiled.textContent).toContain('Yes!');
  });

  it('shows how earlier requests went', async () => {
    const { compiled } = await setup({
      getMyProgress: vi.fn(async () =>
        summary({
          rewardRequests: [
            request('a', { status: 'Approved', name: 'Ice cream' }),
            request('d', { status: 'Declined', name: 'New game' }),
          ],
        }),
      ),
    });

    expect(compiled.textContent).toContain('Earlier');
    expect(compiled.textContent).toContain('Ice cream');
    expect(compiled.textContent).toContain('Yes!');
    expect(compiled.textContent).toContain('Not this time');
    expect(compiled.textContent).not.toContain('Waiting for a grown-up');
  });
});
