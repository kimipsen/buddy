import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  Night,
  NightDutyService,
  NightSchedule,
  isNightOverride,
  nightEntry,
  scheduledEntry,
} from './night-duty.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('NightDutyService', () => {
  let service: NightDutyService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const base = `${apiBaseUrl}/night-duty/children/c-1`;
  const mom = { responsible: { kind: 0 as const, guardianId: 'g-1' }, note: '' };

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

    service = TestBed.inject(NightDutyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs a child schedule', async () => {
    const schedule: NightSchedule = { childId: 'c-1', today: '2026-11-09', patterns: [] };
    const promise = service.getSchedule('c-1');

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush(schedule);

    expect(await promise).toEqual(schedule);
  });

  it('GETs the resolved nights of a range', async () => {
    const nights: Night[] = [{ date: '2026-11-09', status: { kind: 0 } }];
    const promise = service.listNights('c-1', '2026-11-09', '2026-11-15');

    const req = httpMock.expectOne(`${base}/nights?from=2026-11-09&to=2026-11-15`);
    expect(req.request.method).toBe('GET');
    req.flush(nights);

    expect(await promise).toEqual(nights);
  });

  it('PUTs a fixed-schedule version', async () => {
    const request = {
      effectiveFrom: '2026-11-16',
      cycleWeeks: 2,
      anchorMonday: '2026-11-16',
      days: [{ week: 0, day: 'Monday' as const, entry: mom }],
    };
    const promise = service.replacePattern('c-1', request);

    const req = httpMock.expectOne(`${base}/pattern`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({ childId: 'c-1', today: '2026-11-09', patterns: [] });

    expect((await promise).childId).toBe('c-1');
  });

  it('PUTs several overrides in one request', async () => {
    const promise = service.setOverrides('c-1', [
      { date: '2026-11-10', entry: mom },
      { date: '2026-11-12', entry: null },
    ]);

    const req = httpMock.expectOne(`${base}/overrides`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      nights: [
        { date: '2026-11-10', entry: mom },
        { date: '2026-11-12', entry: null },
      ],
    });
    req.flush([]);

    expect(await promise).toEqual([]);
  });

  it('DELETEs the overrides of a range', async () => {
    const promise = service.clearOverrides('c-1', '2026-11-10', '2026-11-10');

    const req = httpMock.expectOne(`${base}/overrides?from=2026-11-10&to=2026-11-10`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    await promise;
  });

  describe('night helpers', () => {
    const planned = (source: 'Pattern' | 'Override'): Night => ({
      date: '2026-11-10',
      status: { kind: 2, entry: mom, source, scheduled: source === 'Override' ? mom : null },
    });

    it('reads the entry of a planned night only', () => {
      expect(nightEntry(planned('Pattern'))).toEqual(mom);
      expect(nightEntry({ date: 'x', status: { kind: 1, scheduled: mom } })).toBeNull();
      expect(nightEntry({ date: 'x', status: { kind: 0 } })).toBeNull();
    });

    it('reads what the schedule said for cleared and planned nights', () => {
      expect(scheduledEntry(planned('Override'))).toEqual(mom);
      expect(scheduledEntry({ date: 'x', status: { kind: 1, scheduled: mom } })).toEqual(mom);
      expect(scheduledEntry({ date: 'x', status: { kind: 0 } })).toBeNull();
    });

    it('marks cleared nights and override entries as changes', () => {
      expect(isNightOverride(planned('Override'))).toBe(true);
      expect(isNightOverride({ date: 'x', status: { kind: 1, scheduled: null } })).toBe(true);
      expect(isNightOverride(planned('Pattern'))).toBe(false);
      expect(isNightOverride({ date: 'x', status: { kind: 0 } })).toBe(false);
    });
  });
});
