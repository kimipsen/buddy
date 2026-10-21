import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  MealieConnectionView,
  MealieService,
  isMealieConnected,
  mealieErrorKey,
} from './mealie.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('MealieService', () => {
  let service: MealieService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const base = `${apiBaseUrl}/mealplans/children/child-1`;

  const connected: MealieConnectionView = {
    kind: 1,
    baseUrl: 'https://mealie.holm.example',
    householdName: 'Holm',
    mealieUserName: 'buddy-sync',
    mealieVersion: 'v3.29.0',
    tokenLast4: 'b7f2',
    connectedBy: 'guardian-1',
    connectedAt: '2026-10-19T10:00:00Z',
  };

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
    service = TestBed.inject(MealieService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs the family connection', async () => {
    const promise = service.getConnection('child-1');

    const req = httpMock.expectOne(`${base}/mealie`);
    expect(req.request.method).toBe('GET');
    req.flush(connected);

    await expect(promise).resolves.toEqual(connected);
  });

  describe('connection cache', () => {
    const notConnected: MealieConnectionView = { kind: 0 };
    const FIVE_MINUTES = 5 * 60 * 1000;
    let now = 1_000_000;

    beforeEach(() => {
      now = 1_000_000;
      vi.spyOn(Date, 'now').mockImplementation(() => now);
    });

    afterEach(() => vi.restoreAllMocks());

    async function load(childId = 'child-1', view: MealieConnectionView = connected) {
      const promise = service.getConnection(childId);
      httpMock.expectOne(`${apiBaseUrl}/mealplans/children/${childId}/mealie`).flush(view);
      return promise;
    }

    it('reuses a connection loaded in the last five minutes, per child', async () => {
      await load();
      now += FIVE_MINUTES - 1;

      await expect(service.getConnection('child-1')).resolves.toEqual(connected);
      httpMock.expectNone(`${base}/mealie`);
      await load('child-2', notConnected);
    });

    it('asks again once five minutes have passed', async () => {
      await load();
      now += FIVE_MINUTES;

      await expect(load('child-1', notConnected)).resolves.toEqual(notConnected);
    });

    it('shares one request between two pages asking at once', async () => {
      const first = service.getConnection('child-1');
      const second = service.getConnection('child-1');

      httpMock.expectOne(`${base}/mealie`).flush(connected);

      await expect(first).resolves.toEqual(connected);
      await expect(second).resolves.toEqual(connected);
    });

    it('asks again after a failed load', async () => {
      const failed = service.getConnection('child-1');
      httpMock.expectOne(`${base}/mealie`).flush(null, { status: 500, statusText: 'Error' });
      await expect(failed).rejects.toBeInstanceOf(HttpErrorResponse);

      await expect(load()).resolves.toEqual(connected);
    });

    it('keeps a newer load when an older one fails later', async () => {
      const old = service.getConnection('child-1');
      const oldRequest = httpMock.expectOne(`${base}/mealie`);
      now += FIVE_MINUTES;
      const newer = service.getConnection('child-1');
      httpMock.expectOne(`${base}/mealie`).flush(connected);
      await newer;

      oldRequest.flush(null, { status: 500, statusText: 'Error' });
      await expect(old).rejects.toBeInstanceOf(HttpErrorResponse);

      await expect(service.getConnection('child-1')).resolves.toEqual(connected);
      httpMock.expectNone(`${base}/mealie`);
    });

    it('forgets every child’s connection after a connect or a disconnect, even a failed one', async () => {
      for (const change of [
        () => {
          const promise = service.connect('child-1', 'https://m.example', 'token');
          httpMock.expectOne({ method: 'PUT', url: `${base}/mealie` }).flush(connected);
          return promise;
        },
        () => {
          const promise = service.disconnect('child-1');
          httpMock
            .expectOne({ method: 'DELETE', url: `${base}/mealie` })
            .flush(null, { status: 500, statusText: 'Error' });
          return promise.catch(() => undefined);
        },
      ]) {
        now += FIVE_MINUTES;
        await load('child-1', notConnected);
        await load('child-2', notConnected);

        await change();

        await load('child-1');
        await load('child-2');
      }
    });
  });

  it('PUTs the address and token to connect', async () => {
    const promise = service.connect('child-1', 'https://mealie.holm.example', 'token-1234');

    const req = httpMock.expectOne(`${base}/mealie`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      baseUrl: 'https://mealie.holm.example',
      apiToken: 'token-1234',
    });
    req.flush(connected);

    await expect(promise).resolves.toEqual(connected);
  });

  it('DELETEs the connection to disconnect', async () => {
    const promise = service.disconnect('child-1');

    const req = httpMock.expectOne(`${base}/mealie`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    await promise;
  });

  it('POSTs an empty body to test the connection', async () => {
    const result = {
      kind: 0 as const,
      mealieVersion: 'v3.29.0',
      recipeCount: 214,
      shoppingListCount: 2,
    };
    const promise = service.testConnection('child-1');

    const req = httpMock.expectOne(`${base}/mealie/test-connection`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(result);

    await expect(promise).resolves.toEqual(result);
  });

  it('GETs recipes with the search and page as query params', async () => {
    const page = { items: [], page: 2, totalPages: 3, total: 41 };
    const promise = service.searchRecipes('child-1', 'lasagne', 2);

    const req = httpMock.expectOne((r) => r.url === `${base}/mealie/recipes`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('search')).toBe('lasagne');
    expect(req.request.params.get('page')).toBe('2');
    req.flush(page);

    await expect(promise).resolves.toEqual(page);
  });

  it('GETs one recipe by id', async () => {
    const promise = service.getRecipe('child-1', 'recipe-1');

    const req = httpMock.expectOne(`${base}/mealie/recipes/recipe-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'recipe-1' });

    await expect(promise).resolves.toEqual({ id: 'recipe-1' });
  });

  it('GETs the recipe photo as a blob', async () => {
    const blob = new Blob(['RIFF'], { type: 'image/webp' });
    const promise = service.getRecipeImage('child-1', 'recipe-1');

    const req = httpMock.expectOne(`${base}/mealie/recipes/recipe-1/image`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(blob);

    await expect(promise).resolves.toEqual(blob);
  });

  it('POSTs the range to preview a Mealie import, without an idempotency key', async () => {
    const preview = { preview: { format: 'mealie' }, recipes: [] };
    const promise = service.previewImport('child-1', '2026-10-26', '2026-11-01');

    const req = httpMock.expectOne(`${base}/imports/mealie/preview`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ from: '2026-10-26', to: '2026-11-01' });
    expect(req.request.headers.has('Idempotency-Key')).toBe(false);
    req.flush(preview);

    await expect(promise).resolves.toEqual(preview);
  });

  it('GETs the shopping lists', async () => {
    const lists = [{ id: 'list-1', name: 'Weekly shop' }];
    const promise = service.listShoppingLists('child-1');

    const req = httpMock.expectOne(`${base}/mealie/shopping-lists`);
    expect(req.request.method).toBe('GET');
    req.flush(lists);

    await expect(promise).resolves.toEqual(lists);
  });

  it('POSTs the week and slots to a shopping list with an idempotency key', async () => {
    const added = { added: [], skippedUnlinked: [], skippedEatingOut: 0 };
    const promise = service.addPlanToShoppingList('child-1', 'list-1', '2026-10-19', '2026-10-25', [
      'Dinner',
    ]);

    const req = httpMock.expectOne(`${base}/mealie/shopping-lists/list-1/recipes`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ from: '2026-10-19', to: '2026-10-25', slots: ['Dinner'] });
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush(added);

    await expect(promise).resolves.toEqual(added);
  });

  describe('mealieErrorKey', () => {
    const httpError = (status: number, code?: string) =>
      new HttpErrorResponse({ status, error: code ? { code } : null });

    it('maps each Mealie error code to its message', () => {
      expect(mealieErrorKey(httpError(409, 'mealie_not_connected'), 'x')).toBe(
        'mealie.errors.notConnected',
      );
      expect(mealieErrorKey(httpError(502, 'mealie_unreachable'), 'x')).toBe(
        'mealie.errors.unreachable',
      );
      expect(mealieErrorKey(httpError(502, 'mealie_token_rejected'), 'x')).toBe(
        'mealie.errors.tokenRejected',
      );
      expect(mealieErrorKey(httpError(502, 'mealie_error'), 'x')).toBe('mealie.errors.error');
    });

    it('falls back for any other error', () => {
      expect(mealieErrorKey(httpError(500, 'internal_error'), 'fallback')).toBe('fallback');
      expect(mealieErrorKey(httpError(0), 'fallback')).toBe('fallback');
      expect(mealieErrorKey(new Error('boom'), 'fallback')).toBe('fallback');
    });
  });

  it('isMealieConnected tells a connection from none', () => {
    expect(isMealieConnected(connected)).toBe(true);
    expect(isMealieConnected({ kind: 0 })).toBe(false);
    expect(isMealieConnected(null)).toBe(false);
  });
});
