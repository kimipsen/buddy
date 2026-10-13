import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RuntimeConfigService } from './runtime-config.service';
import { MyTransitionWarnings } from './transition-warnings';
import { ChildTransitionWarnings, TransitionWarningsService } from './transition-warnings.service';

describe('TransitionWarningsService', () => {
  let service: TransitionWarningsService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const base = `${apiBaseUrl}/transition-warnings`;

  const mine: MyTransitionWarnings = {
    enabled: false,
    leadMinutes: [15, 5, 1],
    kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
    quietHours: null,
    overrides: [],
  };
  const forChild: ChildTransitionWarnings = { ...mine, childId: 'c1', overrides: [] };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: { apiBaseUrl } },
      ],
    });
    service = TestBed.inject(TransitionWarningsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it("reads the signed-in child's own settings", async () => {
    const promise = service.getMine();
    const req = httpMock.expectOne(`${base}/me`);
    expect(req.request.method).toBe('GET');
    req.flush(mine);
    await expect(promise).resolves.toEqual(mine);
  });

  it("reads a child's settings for a guardian", async () => {
    const promise = service.getForChild('c1');
    const req = httpMock.expectOne(`${base}/children/c1`);
    expect(req.request.method).toBe('GET');
    req.flush(forChild);
    await expect(promise).resolves.toEqual(forChild);
  });

  it('saves the whole settings form with a PUT', async () => {
    const body = {
      enabled: true,
      leadMinutes: [10, 2],
      kinds: ['Event' as const],
      quietHours: { from: '21:00', to: '07:00' },
    };
    const promise = service.configure('c1', body);
    const req = httpMock.expectOne(`${base}/children/c1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    expect(req.request.headers.has('Idempotency-Key')).toBe(false);
    req.flush({ ...forChild, enabled: true });
    await expect(promise).resolves.toMatchObject({ enabled: true });
  });

  it('sets an item override, an empty list meaning "never"', async () => {
    const promise = service.setOverride('c1', 'item-1', []);
    const req = httpMock.expectOne(`${base}/children/c1/items/item-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ leadMinutes: [] });
    req.flush(forChild);
    await promise;
  });

  it('clears an item override with a DELETE', async () => {
    const promise = service.clearOverride('c1', 'item-1');
    const req = httpMock.expectOne(`${base}/children/c1/items/item-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await expect(promise).resolves.toBeUndefined();
  });
});
