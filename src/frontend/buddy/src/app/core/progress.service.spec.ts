import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  GoalPost,
  ProgressService,
  ProgressSummary,
  RewardDraft,
  errorCode,
} from './progress.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('ProgressService', () => {
  let service: ProgressService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const childId = 'child-1';

  function summary(overrides: Partial<ProgressSummary> = {}): ProgressSummary {
    return {
      totalStars: 12,
      unlockedMilestones: [5, 10],
      displayIcon: 'star',
      nextGoalThreshold: 20,
      nextGoalIcon: 'trophy',
      goalPosts: [{ threshold: 20, icon: 'trophy', label: 'Big prize' }],
      spendableStars: 12,
      spentStars: 0,
      rewards: [],
      rewardRequests: [],
      ...overrides,
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: RuntimeConfigService,
          useValue: { apiBaseUrl } as Partial<RuntimeConfigService>,
        },
      ],
    });

    service = TestBed.inject(ProgressService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('getMyProgress', () => {
    it("GETs the current user's progress", async () => {
      const body = summary();

      const promise = service.getMyProgress();

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/me`);
      expect(req.request.method).toBe('GET');
      req.flush(body);

      await expect(promise).resolves.toEqual(body);
    });

    it('rejects on an error response', async () => {
      const promise = service.getMyProgress();

      httpMock
        .expectOne(`${apiBaseUrl}/progress/me`)
        .flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toMatchObject({ status: 500 });
    });
  });

  describe('getChildProgress', () => {
    it("GETs the given child's progress", async () => {
      const body = summary({ totalStars: 3 });

      const promise = service.getChildProgress(childId);

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/children/${childId}`);
      expect(req.request.method).toBe('GET');
      req.flush(body);

      await expect(promise).resolves.toEqual(body);
    });

    it('rejects on an error response', async () => {
      const promise = service.getChildProgress(childId);

      httpMock
        .expectOne(`${apiBaseUrl}/progress/children/${childId}`)
        .flush('nope', { status: 403, statusText: 'Forbidden' });

      await expect(promise).rejects.toMatchObject({ status: 403 });
    });
  });

  describe('configureGoalPosts', () => {
    it('PUTs the full goal post list wrapped in { goalPosts } and returns the updated summary', async () => {
      const goalPosts: GoalPost[] = [
        { threshold: 10, icon: 'balloon', label: '' },
        { threshold: 25, icon: 'bike', label: 'New bike' },
      ];
      const body = summary({ goalPosts });

      const promise = service.configureGoalPosts(childId, goalPosts);

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/children/${childId}/goals`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ goalPosts });
      req.flush(body);

      await expect(promise).resolves.toEqual(body);
    });

    it('sends an empty list to clear the configured goal posts', async () => {
      const promise = service.configureGoalPosts(childId, []);

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/children/${childId}/goals`);
      expect(req.request.body).toEqual({ goalPosts: [] });
      req.flush(summary({ goalPosts: [] }));

      await promise;
    });
  });

  describe('configureRewards', () => {
    it('PUTs the full catalog wrapped in { rewards }, keeping ids and omitting them for new rows', async () => {
      const rewards: RewardDraft[] = [
        { id: 'reward-1', name: 'Screen time', icon: '📱', cost: 5 },
        { name: 'Choose dinner', icon: '🍕', cost: 12 },
      ];
      const body = summary();

      const promise = service.configureRewards(childId, rewards);

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/children/${childId}/rewards`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ rewards });
      req.flush(body);

      await expect(promise).resolves.toEqual(body);
    });
  });

  describe('requestReward', () => {
    it('POSTs the reward id with an Idempotency-Key', async () => {
      const body = summary({ spendableStars: 7 });

      const promise = service.requestReward('reward-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/me/reward-requests`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ rewardId: 'reward-1' });
      expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
      req.flush(body);

      await expect(promise).resolves.toEqual(body);
    });

    it('rejects with the API error code on a 409', async () => {
      const promise = service.requestReward('reward-1');

      httpMock
        .expectOne(`${apiBaseUrl}/progress/me/reward-requests`)
        .flush({ code: 'insufficient_stars' }, { status: 409, statusText: 'Conflict' });

      const error = await promise.catch((e: unknown) => e);
      expect(errorCode(error)).toBe('insufficient_stars');
    });
  });

  describe('cancelRewardRequest', () => {
    it("POSTs to the child's own request's cancel route", async () => {
      const promise = service.cancelRewardRequest('request-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/progress/me/reward-requests/request-1/cancel`);
      expect(req.request.method).toBe('POST');
      req.flush(summary());

      await promise;
    });
  });

  describe('approveRewardRequest / declineRewardRequest', () => {
    it("POSTs to the child's request's approve route", async () => {
      const promise = service.approveRewardRequest(childId, 'request-1');

      const req = httpMock.expectOne(
        `${apiBaseUrl}/progress/children/${childId}/reward-requests/request-1/approve`,
      );
      expect(req.request.method).toBe('POST');
      req.flush(summary({ spentStars: 5 }));

      await expect(promise).resolves.toMatchObject({ spentStars: 5 });
    });

    it("POSTs to the child's request's decline route", async () => {
      const promise = service.declineRewardRequest(childId, 'request-1');

      const req = httpMock.expectOne(
        `${apiBaseUrl}/progress/children/${childId}/reward-requests/request-1/decline`,
      );
      expect(req.request.method).toBe('POST');
      req.flush(summary());

      await promise;
    });
  });
});

describe('errorCode', () => {
  it('reads the code from an API error envelope', () => {
    expect(errorCode({ error: { code: 'reward_request_resolved' } })).toBe(
      'reward_request_resolved',
    );
  });

  it('is undefined for an error without one', () => {
    expect(errorCode(new Error('boom'))).toBeUndefined();
    expect(errorCode({ error: 'text body' })).toBeUndefined();
    expect(errorCode(null)).toBeUndefined();
  });
});
