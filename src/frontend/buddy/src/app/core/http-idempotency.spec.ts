import { HttpClient, HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { postIdempotent } from './http-idempotency';

describe('postIdempotent', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('sends a fresh Idempotency-Key header on the request', async () => {
    const promise = firstValueFrom(postIdempotent<{ id: string }>(http, '/things', { name: 'A' }));

    const req = httpMock.expectOne('/things');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/i);
    req.flush({ id: '1' });

    await expect(promise).resolves.toEqual({ id: '1' });
  });

  it('uses a different key for each separate call', async () => {
    const first = firstValueFrom(postIdempotent<void>(http, '/things', {}));
    const firstReq = httpMock.expectOne('/things');
    const firstKey = firstReq.request.headers.get('Idempotency-Key');
    firstReq.flush(null);
    await first;

    const second = firstValueFrom(postIdempotent<void>(http, '/things', {}));
    const secondReq = httpMock.expectOne('/things');
    const secondKey = secondReq.request.headers.get('Idempotency-Key');
    secondReq.flush(null);
    await second;

    expect(firstKey).not.toBe(secondKey);
  });

  it('retries a transient network failure with the same key and resolves once it succeeds', async () => {
    const promise = firstValueFrom(postIdempotent<{ id: string }>(http, '/things', { name: 'A' }));

    const firstAttempt = httpMock.expectOne('/things');
    const key = firstAttempt.request.headers.get('Idempotency-Key');
    firstAttempt.flush('offline', { status: 0, statusText: 'Unknown Error' });

    // The retry is scheduled after a real delay (RETRY_BASE_DELAY_MS), not a fake-timer tick --
    // waiting past it here keeps this test decoupled from that implementation detail.
    await new Promise((resolve) => setTimeout(resolve, 400));

    const secondAttempt = httpMock.expectOne('/things');
    expect(secondAttempt.request.headers.get('Idempotency-Key')).toBe(key);
    secondAttempt.flush({ id: '1' });

    await expect(promise).resolves.toEqual({ id: '1' });
  });

  it('retries a 5xx response, not just a network failure', async () => {
    const promise = firstValueFrom(postIdempotent<{ id: string }>(http, '/things', { name: 'A' }));

    httpMock.expectOne('/things').flush('boom', { status: 503, statusText: 'Service Unavailable' });
    await new Promise((resolve) => setTimeout(resolve, 400));

    httpMock.expectOne('/things').flush({ id: '1' });

    await expect(promise).resolves.toEqual({ id: '1' });
  });

  it('does not retry a client error and rejects with it immediately', async () => {
    const promise = firstValueFrom(postIdempotent<unknown>(http, '/things', {}));

    httpMock.expectOne('/things').flush('invalid', { status: 400, statusText: 'Bad Request' });

    await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    // No second attempt: httpMock.verify() in afterEach fails if one snuck in.
  });

  it('gives up after exhausting its retries and rejects with the last error', async () => {
    const promise = firstValueFrom(postIdempotent<unknown>(http, '/things', {}));

    httpMock.expectOne('/things').flush('boom', { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve, 400));

    httpMock.expectOne('/things').flush('boom', { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve, 700));

    httpMock.expectOne('/things').flush('boom', { status: 500, statusText: 'Server Error' });

    await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
  });
});
