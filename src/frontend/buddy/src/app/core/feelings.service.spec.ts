import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { FeelingCheckIn, FeelingsService } from './feelings.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('FeelingsService', () => {
  let service: FeelingsService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const base = `${apiBaseUrl}/feelings`;
  const child = `${base}/children/child-1`;

  // As the API sends it: the time with seconds.
  function apiCheckIn(overrides: Partial<FeelingCheckIn> = {}): FeelingCheckIn {
    return {
      id: 'c1',
      zone: 'Yellow',
      tags: ['Worried'],
      note: '',
      date: '2026-10-13',
      time: '15:30:00',
      loggedBy: 'child-1',
      isSelfReport: true,
      recordedAt: '2026-10-13T13:30:00Z',
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

    service = TestBed.inject(FeelingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it("loads the child's today and trims times to HH:mm", async () => {
    const promise = service.getMyToday();

    const req = httpMock.expectOne(`${base}/me/today`);
    expect(req.request.method).toBe('GET');
    req.flush({
      date: '2026-10-13',
      promptTimes: ['07:30:00', '15:00:00'],
      displayMode: 'FacesOnly',
      checkIns: [apiCheckIn()],
    });

    const today = await promise;
    expect(today.promptTimes).toEqual(['07:30', '15:00']);
    expect(today.displayMode).toBe('FacesOnly');
    expect(today.checkIns[0].time).toBe('15:30');
  });

  it('checks in with an idempotency key', async () => {
    const promise = service.checkIn({ zone: 'Green', tags: ['Happy'], note: 'Football' });

    const req = httpMock.expectOne(`${base}/me/check-ins`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    expect(req.request.body).toEqual({ zone: 'Green', tags: ['Happy'], note: 'Football' });
    req.flush(apiCheckIn({ zone: 'Green' }));

    expect((await promise).time).toBe('15:30');
  });

  it('undoes a check-in', async () => {
    const promise = service.undoCheckIn('c1');

    const req = httpMock.expectOne(`${base}/me/check-ins/c1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    await promise;
  });

  it("lists a child's check-ins in a range", async () => {
    const promise = service.listCheckIns('child-1', '2026-09-30', '2026-10-13');

    const req = httpMock.expectOne(
      (r) =>
        r.url === `${child}/check-ins` &&
        r.params.get('from') === '2026-09-30' &&
        r.params.get('to') === '2026-10-13',
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      from: '2026-09-30',
      to: '2026-10-13',
      promptTimes: ['19:30:00'],
      displayMode: 'FacesAndWords',
      checkIns: [apiCheckIn()],
    });

    const list = await promise;
    expect(list.promptTimes).toEqual(['19:30']);
    expect(list.checkIns[0].time).toBe('15:30');
  });

  it('logs a check-in for a child, with or without a moment', async () => {
    const promise = service.logCheckIn(
      'child-1',
      { zone: 'Red', tags: [], note: 'Pickup' },
      '2026-10-12',
      '15:10',
    );

    const req = httpMock.expectOne(`${child}/check-ins`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    expect(req.request.body).toEqual({
      zone: 'Red',
      tags: [],
      note: 'Pickup',
      date: '2026-10-12',
      time: '15:10',
    });
    req.flush(apiCheckIn({ zone: 'Red', time: '15:10:00' }));

    expect((await promise).time).toBe('15:10');

    const now = service.logCheckIn('child-1', { zone: 'Blue', tags: [], note: '' }, null, null);
    const nowReq = httpMock.expectOne(`${child}/check-ins`);
    expect(nowReq.request.body).toEqual({
      zone: 'Blue',
      tags: [],
      note: '',
      date: null,
      time: null,
    });
    nowReq.flush(apiCheckIn());
    await now;
  });

  it('edits and removes a check-in', async () => {
    const edit = service.editCheckIn('child-1', 'c1', {
      zone: 'Yellow',
      tags: ['Frustrated'],
      note: '',
      date: '2026-10-12',
      time: '15:30',
    });

    const editReq = httpMock.expectOne(`${child}/check-ins/c1`);
    expect(editReq.request.method).toBe('PUT');
    expect(editReq.request.body).toEqual({
      zone: 'Yellow',
      tags: ['Frustrated'],
      note: '',
      date: '2026-10-12',
      time: '15:30',
    });
    editReq.flush(apiCheckIn());
    expect((await edit).time).toBe('15:30');

    const remove = service.removeCheckIn('child-1', 'c1');
    const removeReq = httpMock.expectOne(`${child}/check-ins/c1`);
    expect(removeReq.request.method).toBe('DELETE');
    removeReq.flush(null);
    await remove;
  });

  it('sets the prompt times and the display mode', async () => {
    const times = service.setPromptTimes('child-1', ['07:30', '19:30']);

    const timesReq = httpMock.expectOne(`${child}/prompt-times`);
    expect(timesReq.request.method).toBe('PUT');
    expect(timesReq.request.body).toEqual({ times: ['07:30', '19:30'] });
    timesReq.flush({ times: ['07:30:00', '19:30:00'] });
    expect(await times).toEqual(['07:30', '19:30']);

    const mode = service.setDisplayMode('child-1', 'FacesOnly');
    const modeReq = httpMock.expectOne(`${child}/display-mode`);
    expect(modeReq.request.method).toBe('PUT');
    expect(modeReq.request.body).toEqual({ displayMode: 'FacesOnly' });
    modeReq.flush({ displayMode: 'FacesOnly' });
    expect(await mode).toBe('FacesOnly');
  });

  it('creates, lists and revokes share links', async () => {
    const created = service.createShareLink('child-1', '2026-11-12T00:00:00.000Z');
    const createReq = httpMock.expectOne(`${child}/share-links`);
    expect(createReq.request.method).toBe('POST');
    expect(createReq.request.headers.has('Idempotency-Key')).toBe(true);
    expect(createReq.request.body).toEqual({ expiresAt: '2026-11-12T00:00:00.000Z' });
    createReq.flush({ id: 'l1', token: 'tok', createdAt: '2026-10-13T00:00:00Z', expiresAt: null });
    expect((await created).token).toBe('tok');

    const listed = service.listShareLinks('child-1');
    const listReq = httpMock.expectOne(`${child}/share-links`);
    expect(listReq.request.method).toBe('GET');
    listReq.flush([]);
    expect(await listed).toEqual([]);

    const revoked = service.revokeShareLink('child-1', 'l1');
    const revokeReq = httpMock.expectOne(`${child}/share-links/l1`);
    expect(revokeReq.request.method).toBe('DELETE');
    revokeReq.flush(null);
    await revoked;
  });

  it('reads a shared view anonymously, with an optional range', async () => {
    const promise = service.getShared('a/b', '2026-09-30', '2026-10-13');

    const req = httpMock.expectOne(
      (r) =>
        r.url === `${base}/shared/a%2Fb` &&
        r.params.get('from') === '2026-09-30' &&
        r.params.get('to') === '2026-10-13',
    );
    req.flush({
      childGivenName: 'Emil',
      childFamilyName: 'Hansen',
      from: '2026-09-30',
      to: '2026-10-13',
      expiresAt: null,
      checkIns: [apiCheckIn()],
    });

    expect((await promise).checkIns[0].time).toBe('15:30');

    const noRange = service.getShared('tok');
    const noRangeReq = httpMock.expectOne((r) => r.url === `${base}/shared/tok`);
    expect(noRangeReq.request.params.keys()).toEqual([]);
    noRangeReq.flush({
      childGivenName: 'Emil',
      childFamilyName: 'Hansen',
      from: '2026-09-30',
      to: '2026-10-13',
      expiresAt: null,
      checkIns: [],
    });
    await noRange;
  });
});
