import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { FirstThenBoard, FirstThenService, GuardianFirstThen } from './first-then.service';
import { RuntimeConfigService } from './runtime-config.service';

const apiBaseUrl = 'https://api.example.test';
const childId = 'child-1';

const board: FirstThenBoard = {
  revision: 1,
  state: 'Waiting',
  first: { title: 'Homework', icon: '📚' },
  then: { title: 'iPad', icon: '📱' },
  thenMinutes: 20,
  unlockedAt: null,
  lastModifiedAt: '2026-10-14T15:00:00+00:00',
  lastModifiedByName: 'Sara',
};

describe('FirstThenService', () => {
  let service: FirstThenService;
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
    service = TestBed.inject(FirstThenService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs the child’s own board', async () => {
    const promise = service.getMyBoard();
    const req = httpMock.expectOne(`${apiBaseUrl}/first-then/me`);
    expect(req.request.method).toBe('GET');
    req.flush(board);
    await expect(promise).resolves.toEqual(board);
  });

  it.each([
    ['first/complete' as const, `${apiBaseUrl}/first-then/me/first/complete`],
    ['first/reopen' as const, `${apiBaseUrl}/first-then/me/first/reopen`],
    ['end' as const, `${apiBaseUrl}/first-then/me/end`],
  ])('POSTs the child’s %s with the revision they were shown', async (action, url) => {
    const promise = service.actOnMyBoard(action, 3);
    const req = httpMock.expectOne(url);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ revision: 3 });
    // Idempotent by design, so no Idempotency-Key (first-then-board.md, Routes).
    expect(req.request.headers.has('Idempotency-Key')).toBe(false);
    req.flush(board);
    await expect(promise).resolves.toEqual(board);
  });

  it('GETs a child’s board with the recent cards', async () => {
    const view: GuardianFirstThen = { board, recentFirsts: [board.first!], recentThens: [] };
    const promise = service.getBoard(childId);
    const req = httpMock.expectOne(`${apiBaseUrl}/first-then/children/${childId}`);
    expect(req.request.method).toBe('GET');
    req.flush(view);
    await expect(promise).resolves.toEqual(view);
  });

  it('PUTs a new board', async () => {
    const body = {
      revision: 0,
      first: { title: 'Homework', icon: '📚' },
      then: { title: 'iPad', icon: '📱' },
      thenMinutes: null,
    };
    const promise = service.setBoard(childId, body);
    const req = httpMock.expectOne(`${apiBaseUrl}/first-then/children/${childId}`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush(board);
    await expect(promise).resolves.toEqual(board);
  });

  it.each([
    ['first/complete' as const, `${apiBaseUrl}/first-then/children/${childId}/first/complete`],
    ['first/reopen' as const, `${apiBaseUrl}/first-then/children/${childId}/first/reopen`],
    ['end' as const, `${apiBaseUrl}/first-then/children/${childId}/end`],
  ])('POSTs a guardian’s %s for the child', async (action, url) => {
    const promise = service.act(childId, action, 2);
    const req = httpMock.expectOne(url);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ revision: 2 });
    req.flush(board);
    await expect(promise).resolves.toEqual(board);
  });
});
