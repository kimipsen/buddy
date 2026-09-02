import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { AssignPickupRequest, PickupOccurrence, PickupsService } from './pickups.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('PickupsService', () => {
  let service: PickupsService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const childId = 'child-1';

  function base(): string {
    return `${apiBaseUrl}/pickups/children/${childId}`;
  }

  function occurrence(overrides: Partial<PickupOccurrence> = {}): PickupOccurrence {
    return {
      date: '2026-08-26',
      slot: 0,
      kind: 0,
      guardianId: 'guardian-1',
      siblingChildId: null,
      playdateHostName: null,
      playdateLocation: null,
      playdateContactInfo: null,
      time: null,
      notes: null,
      assignedBy: 'guardian-1',
      ...overrides
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: { apiBaseUrl } as Partial<RuntimeConfigService> }
      ]
    });

    service = TestBed.inject(PickupsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listSchedule', () => {
    it('GETs the schedule with from/to params', async () => {
      const occurrences = [occurrence()];

      const promise = service.listSchedule(childId, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/schedule` && r.params.get('from') === '2026-08-01' && r.params.get('to') === '2026-08-31'
      );
      expect(req.request.method).toBe('GET');
      req.flush(occurrences);

      await expect(promise).resolves.toEqual(occurrences);
    });

    it('returns an empty list when nothing is scheduled', async () => {
      const promise = service.listSchedule(childId, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne((r) => r.url === `${base()}/schedule`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('rejects on an error response', async () => {
      const promise = service.listSchedule(childId, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne((r) => r.url === `${base()}/schedule`);
      req.flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('assignPickup', () => {
    it('PUTs a guardian assignment with date/slot params', async () => {
      const request: AssignPickupRequest = { kind: 0, guardianId: 'guardian-2' };
      const created = occurrence({ kind: 0, guardianId: 'guardian-2' });

      const promise = service.assignPickup(childId, '2026-08-26', 1, request);

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/assignments` && r.params.get('date') === '2026-08-26' && r.params.get('slot') === '1'
      );
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('serializes slot 0 (DropOff) as the string "0"', async () => {
      const request: AssignPickupRequest = { kind: 1 };

      const promise = service.assignPickup(childId, '2026-08-26', 0, request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments` && r.params.get('slot') === '0');
      expect(req.request.params.get('slot')).toBe('0');
      req.flush(occurrence({ slot: 0, kind: 1, guardianId: null }));

      await promise;
    });

    it('sends a playdate assignment with its full detail fields', async () => {
      const request: AssignPickupRequest = {
        kind: 3,
        playdateHostName: 'Alex',
        playdateLocation: 'Park',
        playdateContactInfo: '555-1234',
        time: '15:30',
        notes: 'Bring cleats'
      };
      const created = occurrence({
        kind: 3,
        guardianId: null,
        playdateHostName: 'Alex',
        playdateLocation: 'Park',
        playdateContactInfo: '555-1234',
        time: '15:30',
        notes: 'Bring cleats'
      });

      const promise = service.assignPickup(childId, '2026-08-26', 1, request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments`);
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('sends a sibling assignment', async () => {
      const request: AssignPickupRequest = { kind: 2, siblingChildId: 'child-2' };
      const created = occurrence({ kind: 2, guardianId: null, siblingChildId: 'child-2' });

      const promise = service.assignPickup(childId, '2026-08-26', 1, request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments`);
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('rejects when the assignment is refused', async () => {
      const request: AssignPickupRequest = { kind: 0, guardianId: 'guardian-2' };

      const promise = service.assignPickup(childId, '2026-08-26', 1, request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('clearPickup', () => {
    it('DELETEs the assignment using date/slot params', async () => {
      const promise = service.clearPickup(childId, '2026-08-26', 1);

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/assignments` && r.params.get('date') === '2026-08-26' && r.params.get('slot') === '1'
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
    });

    it('serializes slot 0 (DropOff) distinctly from slot 1 (PickUp)', async () => {
      const promise = service.clearPickup(childId, '2026-08-26', 0);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments` && r.params.get('slot') === '0');
      expect(req.request.params.get('slot')).toBe('0');
      req.flush(null);

      await promise;
    });
  });
});
