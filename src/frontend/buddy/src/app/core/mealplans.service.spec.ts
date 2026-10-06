import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  GroupMealplanStatus,
  IssuedMealplanIcalToken,
  Meal,
  MealPlanEntry,
  MealplanIcalTokenSummary,
  MealplanScope,
  MealplansService,
  MealRating,
} from './mealplans.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('MealplansService', () => {
  let service: MealplansService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const familyScope: MealplanScope = { kind: 'family', childId: 'child-1' };
  const groupScope: MealplanScope = {
    kind: 'group',
    groupId: 'group-1',
    groupName: 'The Fam',
    accessTier: 2,
  };

  function familyBase(): string {
    return `${apiBaseUrl}/mealplans/children/child-1`;
  }

  function groupBase(): string {
    return `${apiBaseUrl}/mealplans/groups/group-1`;
  }

  function entry(overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date: '2026-08-26',
      slot: 0,
      mealId: 'meal-1',
      mealName: 'Pancakes',
      icon: '🥞',
      color: '#fff',
      rating: null,
      notes: '',
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides,
    };
  }

  function meal(overrides: Partial<Meal> = {}): Meal {
    return {
      id: 'meal-1',
      name: 'Pancakes',
      description: '',
      icon: '🥞',
      color: '#fff',
      isArchived: false,
      ratings: [],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
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

    service = TestBed.inject(MealplansService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listMealPlan', () => {
    it('requests the family plan with from/to params and returns the entries', async () => {
      const entries = [entry()];

      const promise = service.listMealPlan(familyScope, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne(
        (r) =>
          r.url === `${familyBase()}/plan` &&
          r.params.get('from') === '2026-08-01' &&
          r.params.get('to') === '2026-08-31',
      );
      expect(req.request.method).toBe('GET');
      req.flush(entries);

      await expect(promise).resolves.toEqual(entries);
    });

    it('requests the group plan using the group base URL', async () => {
      const promise = service.listMealPlan(groupScope, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne(
        (r) =>
          r.url === `${groupBase()}/plan` &&
          r.params.get('from') === '2026-08-01' &&
          r.params.get('to') === '2026-08-31',
      );
      expect(req.request.method).toBe('GET');
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('returns an empty list when there are no entries', async () => {
      const promise = service.listMealPlan(familyScope, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne((r) => r.url === `${familyBase()}/plan`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('rejects when the backend returns an error status', async () => {
      const promise = service.listMealPlan(familyScope, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne((r) => r.url === `${familyBase()}/plan`);
      req.flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('rateMeal', () => {
    it('PUTs stars and comment to the child-scoped rating endpoint', async () => {
      const rating: MealRating = { stars: 5, comment: 'Yum', ratedAt: '2026-08-26T12:00:00Z' };
      const ratedMeal = meal({
        ratings: [{ childId: 'child-1', stars: 5, comment: 'Yum', ratedAt: rating.ratedAt }],
      });

      const promise = service.rateMeal('child-1', 'meal-1', 5, 'Yum');

      const req = httpMock.expectOne(
        `${apiBaseUrl}/mealplans/children/child-1/meals/meal-1/rating`,
      );
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ stars: 5, comment: 'Yum' });
      req.flush(ratedMeal);

      await expect(promise).resolves.toEqual(ratedMeal);
    });
  });

  describe('listMeals', () => {
    it('fetches meals for a scope and updates the meals signal', async () => {
      const meals = [meal(), meal({ id: 'meal-2', name: 'Toast' })];

      const promise = service.listMeals(familyScope);

      const req = httpMock.expectOne(`${familyBase()}/meals`);
      expect(req.request.method).toBe('GET');
      req.flush(meals);

      await expect(promise).resolves.toEqual(meals);
      expect(service.meals()).toEqual(meals);
    });

    it('fetches meals for a group scope from the group base URL', async () => {
      const promise = service.listMeals(groupScope);

      const req = httpMock.expectOne(`${groupBase()}/meals`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
      expect(service.meals()).toEqual([]);
    });

    it('rejects and leaves state as-is when the request fails', async () => {
      const promise = service.listMeals(familyScope);

      const req = httpMock.expectOne(`${familyBase()}/meals`);
      req.flush('nope', { status: 403, statusText: 'Forbidden' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('createMeal', () => {
    it('POSTs the meal details and appends the created meal to state', async () => {
      const created = meal({ id: 'meal-new', name: 'Waffles' });

      const promise = service.createMeal(familyScope, {
        name: 'Waffles',
        icon: '🧇',
        color: '#eee',
        description: '',
      });

      const req = httpMock.expectOne(`${familyBase()}/meals`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        name: 'Waffles',
        icon: '🧇',
        color: '#eee',
        description: '',
      });
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
      expect(service.meals()).toEqual([created]);
    });

    it('appends to existing meals state rather than replacing it', async () => {
      const existing = meal({ id: 'meal-1' });

      const listPromise = service.listMeals(familyScope);
      httpMock.expectOne(`${familyBase()}/meals`).flush([existing]);
      await listPromise;

      const created = meal({ id: 'meal-2', name: 'Toast' });
      const createPromise = service.createMeal(familyScope, {
        name: 'Toast',
        icon: '🍞',
        color: '#111',
        description: '',
      });
      httpMock
        .expectOne((r) => r.url === `${familyBase()}/meals` && r.method === 'POST')
        .flush(created);
      await createPromise;

      expect(service.meals()).toEqual([existing, created]);
    });
  });

  describe('updateMealDetails', () => {
    it('PATCHes meal details and replaces the matching meal in state', async () => {
      const original = meal({ id: 'meal-1', name: 'Pancakes' });
      const listPromise = service.listMeals(familyScope);
      httpMock.expectOne(`${familyBase()}/meals`).flush([original]);
      await listPromise;

      const updated = meal({ id: 'meal-1', name: 'Fluffy Pancakes' });
      const promise = service.updateMealDetails(familyScope, 'meal-1', {
        name: 'Fluffy Pancakes',
        icon: '🥞',
        color: '#fff',
        description: '',
      });

      const req = httpMock.expectOne(`${familyBase()}/meals/meal-1/details`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({
        name: 'Fluffy Pancakes',
        icon: '🥞',
        color: '#fff',
        description: '',
      });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
      expect(service.meals()).toEqual([updated]);
    });

    it('leaves unrelated meals untouched in state', async () => {
      const other = meal({ id: 'meal-other', name: 'Soup' });
      const target = meal({ id: 'meal-1', name: 'Pancakes' });
      const listPromise = service.listMeals(familyScope);
      httpMock.expectOne(`${familyBase()}/meals`).flush([other, target]);
      await listPromise;

      const updated = meal({ id: 'meal-1', name: 'Fluffy Pancakes' });
      const promise = service.updateMealDetails(familyScope, 'meal-1', {
        name: 'Fluffy Pancakes',
        icon: '🥞',
        color: '#fff',
        description: '',
      });
      httpMock.expectOne(`${familyBase()}/meals/meal-1/details`).flush(updated);
      await promise;

      expect(service.meals()).toEqual([other, updated]);
    });
  });

  describe('archiveMeal', () => {
    it('DELETEs the meal and removes it from state', async () => {
      const target = meal({ id: 'meal-1' });
      const other = meal({ id: 'meal-2' });
      const listPromise = service.listMeals(familyScope);
      httpMock.expectOne(`${familyBase()}/meals`).flush([target, other]);
      await listPromise;

      const promise = service.archiveMeal(familyScope, 'meal-1');

      const req = httpMock.expectOne(`${familyBase()}/meals/meal-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
      expect(service.meals()).toEqual([other]);
    });
  });

  describe('assignMealToSlot', () => {
    it('PUTs mealId/notes with date/slot params and returns the new entry', async () => {
      const created = entry({
        date: '2026-08-26',
        slot: 1,
        mealId: 'meal-1',
        notes: 'extra syrup',
      });

      const promise = service.assignMealToSlot(
        familyScope,
        '2026-08-26',
        1,
        'meal-1',
        'extra syrup',
      );

      const req = httpMock.expectOne(
        (r) =>
          r.url === `${familyBase()}/plan` &&
          r.params.get('date') === '2026-08-26' &&
          r.params.get('slot') === '1',
      );
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ mealId: 'meal-1', notes: 'extra syrup' });
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('serializes slot 0 (Breakfast) as the string "0" in query params', async () => {
      const promise = service.assignMealToSlot(familyScope, '2026-08-26', 0, 'meal-1', '');

      const req = httpMock.expectOne(
        (r) => r.url === `${familyBase()}/plan` && r.params.get('slot') === '0',
      );
      expect(req.request.body).toEqual({ mealId: 'meal-1', notes: '' });
      req.flush(entry({ slot: 0 }));

      await promise;
    });
  });

  describe('clearMealSlot', () => {
    it('DELETEs the plan entry using date/slot params', async () => {
      const promise = service.clearMealSlot(familyScope, '2026-08-26', 2);

      const req = httpMock.expectOne(
        (r) =>
          r.url === `${familyBase()}/plan` &&
          r.params.get('date') === '2026-08-26' &&
          r.params.get('slot') === '2',
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
    });
  });

  describe('shareWithGroup', () => {
    it('PUTs an empty body to share a child plan with a group', async () => {
      const promise = service.shareWithGroup('child-1', 'group-1');

      const req = httpMock.expectOne(
        `${apiBaseUrl}/mealplans/children/child-1/plan/groups/group-1`,
      );
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({});
      req.flush(null);

      await promise;
    });
  });

  describe('unshareFromGroup', () => {
    it('DELETEs the share relationship', async () => {
      const promise = service.unshareFromGroup('child-1', 'group-1');

      const req = httpMock.expectOne(
        `${apiBaseUrl}/mealplans/children/child-1/plan/groups/group-1`,
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
    });
  });

  describe('getSharedGroup', () => {
    it('returns the group when the plan is shared', async () => {
      const promise = service.getSharedGroup('child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/mealplans/children/child-1/plan/groups`);
      expect(req.request.method).toBe('GET');
      req.flush({ groupId: 'group-1', groupName: 'The Fam' });

      await expect(promise).resolves.toEqual({ groupId: 'group-1', groupName: 'The Fam' });
    });

    it('returns null for a 204 (not shared)', async () => {
      const promise = service.getSharedGroup('child-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/mealplans/children/child-1/plan/groups`);
      req.flush(null, { status: 204, statusText: 'No Content' });

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('getGroupMealplanStatus', () => {
    it('GETs the status for the group and resolves with it', async () => {
      const status: GroupMealplanStatus = { hasSharedPlan: true };

      const promise = service.getGroupMealplanStatus('group-1');

      const req = httpMock.expectOne(`${groupBase()}/status`);
      expect(req.request.method).toBe('GET');
      req.flush(status);

      await expect(promise).resolves.toEqual(status);
    });
  });

  describe('listIcalTokens', () => {
    it('GETs the ical-tokens list for the child', async () => {
      const tokens: MealplanIcalTokenSummary[] = [
        { tokenId: 'token-1', issuedAt: '2026-08-01T00:00:00Z' },
      ];

      const promise = service.listIcalTokens('child-1');

      const req = httpMock.expectOne(`${familyBase()}/ical-tokens`);
      expect(req.request.method).toBe('GET');
      req.flush(tokens);

      await expect(promise).resolves.toEqual(tokens);
    });
  });

  describe('createIcalToken', () => {
    it('POSTs an empty body and resolves with the issued token', async () => {
      const issued: IssuedMealplanIcalToken = {
        tokenId: 'token-1',
        token: 'plaintext-secret',
        subscriptionPath: '/mealplans/plan-1/ical/token-1',
      };

      const promise = service.createIcalToken('child-1');

      const req = httpMock.expectOne(`${familyBase()}/ical-tokens`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush(issued);

      await expect(promise).resolves.toEqual(issued);
    });
  });

  describe('revokeIcalToken', () => {
    it('DELETEs the specific token under the child', async () => {
      const promise = service.revokeIcalToken('child-1', 'token-1');

      const req = httpMock.expectOne(`${familyBase()}/ical-tokens/token-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('icalFeedUrl', () => {
    it('prefixes the relative subscription path with the configured API base URL, issuing no HTTP request', () => {
      // httpMock.verify() in afterEach would fail if this triggered a request.
      expect(service.icalFeedUrl('/mealplans/plan-1/ical/token-1')).toBe(
        `${apiBaseUrl}/mealplans/plan-1/ical/token-1`,
      );
    });
  });

  describe('imports', () => {
    it('POSTs the text and options to the preview endpoint without an idempotency key', async () => {
      const preview = { format: 'weekly-note', lines: [], groups: [], warnings: [], emptyDays: 0 };

      const promise = service.previewImport(familyScope, {
        text: 'Madplan 2026',
        format: 'auto',
        weekStart: 0,
        slot: 2,
      });

      const req = httpMock.expectOne(`${familyBase()}/imports/preview`);
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.has('Idempotency-Key')).toBe(false);
      expect(req.request.body).toEqual({
        text: 'Madplan 2026',
        format: 'auto',
        weekStart: 0,
        slot: 2,
      });
      req.flush(preview);

      await expect(promise).resolves.toEqual(preview);
    });

    it('POSTs the reviewed entries with an idempotency key on commit', async () => {
      const result = {
        importId: 'import-1',
        imported: 1,
        createdMeals: 1,
        archivedMeals: 0,
        skipped: [],
      };
      const request = {
        format: 'csv' as const,
        archiveSingleUse: true,
        entries: [{ date: '2024-01-14', slot: 2 as const, newMealName: 'Lasagne', notes: '' }],
      };

      const promise = service.commitImport(groupScope, request);

      const req = httpMock.expectOne(`${groupBase()}/imports`);
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
      expect(req.request.body).toEqual(request);
      req.flush(result);

      await expect(promise).resolves.toEqual(result);
    });

    it('lists imports and reverts one by id', async () => {
      const listPromise = service.listImports(familyScope);
      const listReq = httpMock.expectOne(`${familyBase()}/imports`);
      expect(listReq.request.method).toBe('GET');
      listReq.flush([]);
      await expect(listPromise).resolves.toEqual([]);

      const revertPromise = service.revertImport(familyScope, 'import-1');
      const revertReq = httpMock.expectOne(`${familyBase()}/imports/import-1`);
      expect(revertReq.request.method).toBe('DELETE');
      revertReq.flush(null);
      await expect(revertPromise).resolves.toBeNull();
    });
  });
});
