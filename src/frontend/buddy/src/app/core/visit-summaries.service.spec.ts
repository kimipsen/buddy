import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RuntimeConfigService } from './runtime-config.service';
import {
  VisitSummariesService,
  VisitSummary,
  VisitSummaryRequest,
} from './visit-summaries.service';

describe('VisitSummariesService', () => {
  let service: VisitSummariesService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const base = `${apiBaseUrl}/visit-summaries/children/child-1`;

  const request: VisitSummaryRequest = {
    title: 'Follow-up',
    visitDate: '2026-04-02',
    from: '2026-03-02',
    to: '2026-03-29',
    sections: ['Medicine', 'Sleep'],
    note: 'Ask about lunch',
  };

  const summary: VisitSummary = {
    id: 'summary-1',
    childId: 'child-1',
    title: 'Follow-up',
    visitDate: '2026-04-02',
    from: '2026-03-02',
    to: '2026-03-29',
    sections: ['Medicine', 'Sleep'],
    note: 'Ask about lunch',
    createdBy: 'guardian-1',
    lastModifiedAt: '2026-03-30T10:00:00Z',
    shareLinks: [],
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

    service = TestBed.inject(VisitSummariesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists a child’s summaries', async () => {
    const promise = service.list('child-1');

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([summary]);

    expect(await promise).toEqual([summary]);
  });

  it('gets one summary with its report', async () => {
    const promise = service.get('child-1', 'summary-1');

    const req = httpMock.expectOne(`${base}/summary-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ summary, report: { title: 'Follow-up' } });

    expect((await promise).summary).toEqual(summary);
  });

  it('creates a summary with an idempotency key', async () => {
    const promise = service.create('child-1', request);

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(summary);

    expect(await promise).toEqual(summary);
  });

  it('updates and deletes a summary', async () => {
    const updated = service.update('child-1', 'summary-1', request);
    const put = httpMock.expectOne(`${base}/summary-1`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual(request);
    put.flush(summary);
    expect(await updated).toEqual(summary);

    const deleted = service.delete('child-1', 'summary-1');
    const del = httpMock.expectOne(`${base}/summary-1`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    await deleted;
  });

  it('creates and revokes a share link', async () => {
    const created = service.createShareLink('child-1', 'summary-1', '2026-05-01T00:00:00Z');
    const post = httpMock.expectOne(`${base}/summary-1/share-links`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ expiresAt: '2026-05-01T00:00:00Z' });
    expect(post.request.headers.get('Idempotency-Key')).toBeTruthy();
    post.flush({ id: 'link-1', token: 'secret', createdAt: '', expiresAt: null });
    expect((await created).token).toBe('secret');

    const revoked = service.revokeShareLink('child-1', 'summary-1', 'link-1');
    const del = httpMock.expectOne(`${base}/summary-1/share-links/link-1`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    await revoked;
  });

  it('reads a shared report anonymously with the token encoded', async () => {
    const promise = service.getShared('a/b');

    const req = httpMock.expectOne(`${apiBaseUrl}/visit-summaries/shared/a%2Fb`);
    expect(req.request.method).toBe('GET');
    req.flush({ report: { title: 'Follow-up' }, expiresAt: null });

    expect((await promise).expiresAt).toBeNull();
  });
});
