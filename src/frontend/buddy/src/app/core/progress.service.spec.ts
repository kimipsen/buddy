import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { GoalPost, ProgressService, ProgressSummary } from './progress.service';
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
});
