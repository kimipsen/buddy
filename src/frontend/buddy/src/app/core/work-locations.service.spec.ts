import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RuntimeConfigService } from './runtime-config.service';
import {
  WorkDay,
  WorkLocation,
  WorkLocationSchedule,
  WorkLocationsService,
  WorkPattern,
} from './work-locations.service';

describe('WorkLocationsService', () => {
  let service: WorkLocationsService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const me = `${apiBaseUrl}/work-locations/me`;

  const stil: WorkLocation = {
    id: 'loc-1',
    name: 'Stil',
    icon: '🏢',
    color: '#0ea5e9',
    isArchived: false,
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

    service = TestBed.inject(WorkLocationsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs a guardian schedule', async () => {
    const schedule: WorkLocationSchedule = {
      guardianId: 'g-1',
      locations: [stil],
      pattern: { cycleWeeks: 1, anchorMonday: '2026-09-28', days: [] },
    };
    const promise = service.getSchedule('g-1');

    const req = httpMock.expectOne(`${apiBaseUrl}/work-locations/guardians/g-1`);
    expect(req.request.method).toBe('GET');
    req.flush(schedule);

    await expect(promise).resolves.toEqual(schedule);
  });

  it('GETs resolved work days with from/to params', async () => {
    const days: WorkDay[] = [{ date: '2026-09-28', location: stil, source: 1 }];
    const promise = service.listWorkDays('g-1', '2026-09-28', '2026-10-04');

    const req = httpMock.expectOne(
      (r) =>
        r.url === `${apiBaseUrl}/work-locations/guardians/g-1/days` &&
        r.params.get('from') === '2026-09-28' &&
        r.params.get('to') === '2026-10-04',
    );
    expect(req.request.method).toBe('GET');
    req.flush(days);

    await expect(promise).resolves.toEqual(days);
  });

  it('POSTs a new location with an idempotency key', async () => {
    const details = { name: 'Stil', icon: '🏢', color: '#0ea5e9' };
    const promise = service.addLocation(details);

    const req = httpMock.expectOne(`${me}/locations`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(details);
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(stil);

    await expect(promise).resolves.toEqual(stil);
  });

  it('PATCHes a location', async () => {
    const details = { name: 'Kontoret', icon: '🏬', color: '#10b981' };
    const promise = service.updateLocation('loc-1', details);

    const req = httpMock.expectOne(`${me}/locations/loc-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual(details);
    req.flush({ ...stil, ...details });

    await expect(promise).resolves.toEqual({ ...stil, ...details });
  });

  it('DELETEs (archives) a location', async () => {
    const promise = service.archiveLocation('loc-1');

    const req = httpMock.expectOne(`${me}/locations/loc-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    await expect(promise).resolves.toBeNull();
  });

  it('PUTs the whole pattern', async () => {
    const pattern: WorkPattern = {
      cycleWeeks: 2,
      anchorMonday: '2026-09-28',
      days: [{ week: 1, day: 4, locationId: 'loc-1' }],
    };
    const promise = service.replacePattern(pattern);

    const req = httpMock.expectOne(`${me}/pattern`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(pattern);
    req.flush(pattern);

    await expect(promise).resolves.toEqual(pattern);
  });

  it('PUTs an override range, with null meaning off', async () => {
    const promise = service.setOverrides('2026-10-12', '2026-10-18', null);

    const req = httpMock.expectOne(`${me}/overrides`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ from: '2026-10-12', to: '2026-10-18', locationId: null });
    req.flush([]);

    await expect(promise).resolves.toEqual([]);
  });

  it('DELETEs overrides in a range', async () => {
    const promise = service.clearOverrides('2026-10-12', '2026-10-18');

    const req = httpMock.expectOne(
      (r) =>
        r.url === `${me}/overrides` &&
        r.params.get('from') === '2026-10-12' &&
        r.params.get('to') === '2026-10-18',
    );
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    await expect(promise).resolves.toBeNull();
  });

  it('rejects on an error response', async () => {
    const promise = service.getSchedule('g-1');

    httpMock
      .expectOne(`${apiBaseUrl}/work-locations/guardians/g-1`)
      .flush('nope', { status: 404, statusText: 'Not Found' });

    await expect(promise).rejects.toBeTruthy();
  });
});
