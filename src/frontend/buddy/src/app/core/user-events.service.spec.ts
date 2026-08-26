import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RuntimeConfigService } from './runtime-config.service';
import { UserEventsPage, UserEventsService } from './user-events.service';

describe('UserEventsService', () => {
  let service: UserEventsService;
  let httpMock: HttpTestingController;

  const runtimeConfigStub: Partial<RuntimeConfigService> = { apiBaseUrl: 'https://api.buddy.test' };

  const page: UserEventsPage = {
    items: [{ type: 'UserCreated', data: { userId: 'user-1' } }],
    previousCursor: null,
    nextCursor: 'cursor-2'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: runtimeConfigStub }
      ]
    });

    service = TestBed.inject(UserEventsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('requests the current user events page from the runtime-config-provided API base URL', async () => {
    const resultPromise = service.listCurrentUserEvents(null, 20);

    const req = httpMock.expectOne((request) => request.url === 'https://api.buddy.test/users/me/events');
    expect(req.request.method).toBe('GET');
    req.flush(page);

    await expect(resultPromise).resolves.toEqual(page);
  });

  it('sends pageSize as a string query param and omits cursor when none is given', async () => {
    const resultPromise = service.listCurrentUserEvents(null, 20);

    const req = httpMock.expectOne(
      (request) => request.url === 'https://api.buddy.test/users/me/events'
    );
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.has('cursor')).toBe(false);
    req.flush(page);

    await resultPromise;
  });

  it('includes the cursor query param when a cursor is given', async () => {
    const resultPromise = service.listCurrentUserEvents('cursor-1', 10);

    const req = httpMock.expectOne((request) => request.url === 'https://api.buddy.test/users/me/events');
    expect(req.request.params.get('pageSize')).toBe('10');
    expect(req.request.params.get('cursor')).toBe('cursor-1');
    req.flush(page);

    await resultPromise;
  });

  it('resolves with the exact page returned by the backend', async () => {
    const resultPromise = service.listCurrentUserEvents(null, 5);

    const req = httpMock.expectOne((request) => request.url === 'https://api.buddy.test/users/me/events');
    req.flush(page);

    const result = await resultPromise;
    expect(result.items).toEqual(page.items);
    expect(result.previousCursor).toBeNull();
    expect(result.nextCursor).toBe('cursor-2');
  });

  it('propagates an HTTP error to the caller', async () => {
    const resultPromise = service.listCurrentUserEvents(null, 20);

    const req = httpMock.expectOne((request) => request.url === 'https://api.buddy.test/users/me/events');
    req.flush({ message: 'boom' }, { status: 500, statusText: 'Internal Server Error' });

    await expect(resultPromise).rejects.toMatchObject({ status: 500 });
  });
});
