import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RuntimeConfigService } from './runtime-config.service';
import { SleepDiaryService, SleepEntry, SleepEntryRequest } from './sleep-diary.service';

describe('SleepDiaryService', () => {
  let service: SleepDiaryService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const childId = 'child-1';
  const base = `${apiBaseUrl}/sleep-diary/children/${childId}`;

  const request: SleepEntryRequest = {
    routineStartTime: '19:00',
    ritualStartTime: '19:30',
    ritualEndTime: '20:10',
    bedTime: '20:15',
    fellAsleepTime: '20:45',
    nightWakeUps: [{ startTime: '03:30', durationMinutes: 30 }],
    morningWakeTime: '06:30',
    isTired: true,
    naps: [],
    totalSleepMinutes: 555,
    remarks: 'Restless',
  };

  // As the API sends it: times with seconds.
  function apiEntry(overrides: Partial<SleepEntry> = {}): SleepEntry {
    return {
      ...request,
      routineStartTime: '19:00:00',
      ritualStartTime: '19:30:00',
      ritualEndTime: '20:10:00',
      bedTime: '20:15:00',
      fellAsleepTime: '20:45:00',
      nightWakeUps: [{ startTime: '03:30:00', durationMinutes: 30 }],
      morningWakeTime: '06:30:00',
      naps: [{ startTime: '13:00:00', durationMinutes: 45 }],
      date: '2026-03-02',
      isWeekend: false,
      loggedBy: 'guardian-1',
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

    service = TestBed.inject(SleepDiaryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists entries in a range and trims times to HH:mm', async () => {
    const promise = service.listEntries(childId, '2026-03-02', '2026-03-15');

    const req = httpMock.expectOne(
      (r) =>
        r.url === `${base}/entries` &&
        r.params.get('from') === '2026-03-02' &&
        r.params.get('to') === '2026-03-15',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ sleepHygieneNotes: 'Curtains', entries: [apiEntry({ ritualEndTime: null })] });

    const diary = await promise;
    expect(diary.sleepHygieneNotes).toBe('Curtains');
    expect(diary.entries[0]).toMatchObject({
      routineStartTime: '19:00',
      ritualStartTime: '19:30',
      ritualEndTime: null,
      bedTime: '20:15',
      fellAsleepTime: '20:45',
      morningWakeTime: '06:30',
      nightWakeUps: [{ startTime: '03:30', durationMinutes: 30 }],
      naps: [{ startTime: '13:00', durationMinutes: 45 }],
    });
  });

  it('PUTs a whole day to its date', async () => {
    const promise = service.logEntry(childId, '2026-03-02', request);

    const req = httpMock.expectOne(`${base}/entries/2026-03-02`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(apiEntry());

    expect((await promise).bedTime).toBe('20:15');
  });

  it('DELETEs a day', async () => {
    const promise = service.clearEntry(childId, '2026-03-02');

    const req = httpMock.expectOne(`${base}/entries/2026-03-02`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    await promise;
  });

  it('PUTs the hygiene notes', async () => {
    const promise = service.updateHygieneNotes(childId, 'No screens');

    const req = httpMock.expectOne(`${base}/hygiene-notes`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ notes: 'No screens' });
    req.flush(null, { status: 204, statusText: 'No Content' });

    await promise;
  });

  it('POSTs a share link with an Idempotency-Key', async () => {
    const promise = service.createShareLink(childId, '2026-11-05T00:00:00.000Z');

    const req = httpMock.expectOne(`${base}/share-links`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ expiresAt: '2026-11-05T00:00:00.000Z' });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    const created = { id: 'link-1', token: 'secret', createdAt: 'x', expiresAt: null };
    req.flush(created);

    expect(await promise).toEqual(created);
  });

  it('lists and revokes share links', async () => {
    const list = service.listShareLinks(childId);
    const listReq = httpMock.expectOne(`${base}/share-links`);
    expect(listReq.request.method).toBe('GET');
    listReq.flush([{ id: 'link-1', createdAt: 'x', expiresAt: null }]);
    expect(await list).toHaveLength(1);

    const revoke = service.revokeShareLink(childId, 'link-1');
    const revokeReq = httpMock.expectOne(`${base}/share-links/link-1`);
    expect(revokeReq.request.method).toBe('DELETE');
    revokeReq.flush(null, { status: 204, statusText: 'No Content' });
    await revoke;
  });

  it('GETs the shared diary by token, with the range only when given', async () => {
    const shared = {
      childGivenName: 'Alex',
      childFamilyName: 'Anderson',
      from: '2026-03-02',
      to: '2026-03-15',
      expiresAt: null,
      sleepHygieneNotes: '',
      entries: [apiEntry()],
    };

    const withoutRange = service.getShared('a/b');
    const first = httpMock.expectOne(`${apiBaseUrl}/sleep-diary/shared/a%2Fb`);
    expect(first.request.params.keys()).toEqual([]);
    first.flush(shared);
    expect((await withoutRange).entries[0].bedTime).toBe('20:15');

    const withRange = service.getShared('tok', '2026-03-02', '2026-03-15');
    const second = httpMock.expectOne(
      (r) =>
        r.url === `${apiBaseUrl}/sleep-diary/shared/tok` &&
        r.params.get('from') === '2026-03-02' &&
        r.params.get('to') === '2026-03-15',
    );
    second.flush(shared);
    await withRange;
  });
});
