import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  FinishFlashCardGameRequest,
  FinishFlashCardGameResponse,
  FlashCardSet,
  FlashCardsService,
  isConflict,
} from './flash-cards.service';
import { RuntimeConfigService } from './runtime-config.service';

const apiBaseUrl = 'https://api.example.test';
const base = `${apiBaseUrl}/flash-cards`;

const set: FlashCardSet = {
  id: 'set-1',
  ownerId: 'owner-1',
  name: 'Animals',
  kind: 'Text',
  math: null,
  cards: [{ id: 'card-1', question: 'Cat', answer: 'Kat' }],
  uniqueCardCount: 1,
  builtInKey: null,
  exampleCard: null,
};

const finishRequest: FinishFlashCardGameRequest = {
  gameId: 'game-1',
  setId: 'set-1',
  tierId: 'tier-1',
  results: [{ slot: 0, cardKey: 'card-1', knewFirstTime: true }],
};

const finishResponse: FinishFlashCardGameResponse = {
  stars: 2,
  totalStars: 10,
  game: {
    gameId: 'game-1',
    finishedOn: '2026-11-27',
    setId: 'set-1',
    setName: 'Animals',
    cardCount: 1,
    knownCount: 1,
    stars: 2,
  },
};

describe('FlashCardsService', () => {
  let service: FlashCardsService;
  let httpMock: HttpTestingController;

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
    service = TestBed.inject(FlashCardsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // Settles the pending promise after the request was answered.
  async function expectRoute<T>(
    promise: Promise<T>,
    method: string,
    url: string,
    body: unknown,
    flush: object | null,
  ): Promise<T> {
    const req = httpMock.expectOne(url);
    expect(req.request.method).toBe(method);
    if (body !== undefined) {
      expect(req.request.body).toEqual(body);
    }
    req.flush(flush);
    return promise;
  }

  it('lists the library', async () => {
    await expect(
      expectRoute(service.listSets(), 'GET', `${base}/sets`, undefined, [set]),
    ).resolves.toEqual([set]);
  });

  it('creates a set', async () => {
    const request = { name: 'Animals', kind: 'Text' as const };
    await expect(
      expectRoute(service.createSet(request), 'POST', `${base}/sets`, request, set),
    ).resolves.toEqual(set);
  });

  it('updates a set', async () => {
    const request = { name: 'Pets' };
    await expect(
      expectRoute(service.updateSet('set-1', request), 'PUT', `${base}/sets/set-1`, request, set),
    ).resolves.toEqual(set);
  });

  it('deletes a set', async () => {
    await expectRoute(service.deleteSet('set-1'), 'DELETE', `${base}/sets/set-1`, undefined, null);
  });

  it('adds, updates and removes a card', async () => {
    const card = { question: 'Dog', answer: 'Hund' };
    await expectRoute(
      service.addCard('set-1', card),
      'POST',
      `${base}/sets/set-1/cards`,
      card,
      set,
    );
    await expectRoute(
      service.updateCard('set-1', 'card-1', card),
      'PUT',
      `${base}/sets/set-1/cards/card-1`,
      card,
      set,
    );
    await expectRoute(
      service.removeCard('set-1', 'card-1'),
      'DELETE',
      `${base}/sets/set-1/cards/card-1`,
      undefined,
      null,
    );
  });

  it('reads, enables and disables a child’s sets', async () => {
    const tiers = [{ cardCount: 5, stars: 1 }];
    const enabled = { setId: 'set-1', setName: 'Animals', tiers: [] };

    await expectRoute(
      service.getChildFlashCards('child-1'),
      'GET',
      `${base}/children/child-1`,
      undefined,
      { childId: 'child-1', enabledSets: [], games: [] },
    );
    await expect(
      expectRoute(
        service.enableSet('child-1', 'set-1', tiers),
        'PUT',
        `${base}/children/child-1/sets/set-1`,
        { tiers },
        enabled,
      ),
    ).resolves.toEqual(enabled);
    await expectRoute(
      service.disableSet('child-1', 'set-1'),
      'DELETE',
      `${base}/children/child-1/sets/set-1`,
      undefined,
      null,
    );
  });

  it('lists the child’s own sets and fetches a deck for a tier', async () => {
    await expectRoute(service.listMySets(), 'GET', `${base}/me/sets`, undefined, []);

    const promise = service.getDeck('set-1', 'tier-1');
    const req = httpMock.expectOne(`${base}/me/sets/set-1/deck?tierId=tier-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ cards: [] });
    await expect(promise).resolves.toEqual({ cards: [] });
  });

  describe('finishGame', () => {
    const finishUrl = `${base}/me/games`;

    it('POSTs the game with an Idempotency-Key', async () => {
      const promise = service.finishGame(finishRequest);
      const req = httpMock.expectOne(finishUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(finishRequest);
      expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
      req.flush(finishResponse);

      await expect(promise).resolves.toEqual(finishResponse);
    });

    it('retries one concurrency_conflict as a new call: same GameId, a new Idempotency-Key', async () => {
      const promise = service.finishGame(finishRequest);

      const first = httpMock.expectOne(finishUrl);
      const firstKey = first.request.headers.get('Idempotency-Key');
      first.flush({ code: 'concurrency_conflict' }, { status: 409, statusText: 'Conflict' });

      await nextTick();

      const second = httpMock.expectOne(finishUrl);
      expect(second.request.body).toEqual(finishRequest);
      expect(second.request.headers.get('Idempotency-Key')).not.toBe(firstKey);
      second.flush(finishResponse);

      await expect(promise).resolves.toEqual(finishResponse);
    });

    it('gives up when the retry hits a concurrency_conflict too', async () => {
      const promise = service.finishGame(finishRequest);
      const outcome = promise.then(
        () => null,
        (error: unknown) => error,
      );

      httpMock
        .expectOne(finishUrl)
        .flush({ code: 'concurrency_conflict' }, { status: 409, statusText: 'Conflict' });
      await nextTick();
      httpMock
        .expectOne(finishUrl)
        .flush({ code: 'concurrency_conflict' }, { status: 409, statusText: 'Conflict' });

      expect(isConflict(await outcome, 'concurrency_conflict')).toBe(true);
      httpMock.expectNone(finishUrl);
    });

    it('does not retry idempotency_key_in_progress', async () => {
      const promise = service.finishGame(finishRequest);
      const outcome = promise.then(
        () => null,
        (error: unknown) => error,
      );

      httpMock
        .expectOne(finishUrl)
        .flush({ code: 'idempotency_key_in_progress' }, { status: 409, statusText: 'Conflict' });

      expect(isConflict(await outcome, 'idempotency_key_in_progress')).toBe(true);
      httpMock.expectNone(finishUrl);
    });

    it('does not retry a 422', async () => {
      const promise = service.finishGame(finishRequest);
      const outcome = promise.then(
        () => null,
        (error: unknown) => error,
      );

      httpMock
        .expectOne(finishUrl)
        .flush({ code: 'game_no_longer_available' }, { status: 422, statusText: 'Unprocessable' });

      const error = await outcome;
      expect(error).toBeInstanceOf(HttpErrorResponse);
      expect((error as HttpErrorResponse).status).toBe(422);
      httpMock.expectNone(finishUrl);
    });
  });

  describe('isConflict', () => {
    const conflict = (status: number, code: string | null) =>
      new HttpErrorResponse({ status, error: code === null ? null : { code } });

    it('matches a 409 with the code only', () => {
      expect(isConflict(conflict(409, 'x'), 'x')).toBe(true);
      expect(isConflict(conflict(409, 'y'), 'x')).toBe(false);
      expect(isConflict(conflict(422, 'x'), 'x')).toBe(false);
      expect(isConflict(conflict(409, null), 'x')).toBe(false);
      expect(isConflict(new Error('x'), 'x')).toBe(false);
    });
  });
});

// Lets the promise chain run so the service sends its next request.
function nextTick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}
