import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  AssignPickupRequest,
  IssuedPickupIcalToken,
  PickupIcalTokenSummary,
  PickupOccurrence,
  PickupsService,
  babysitterName,
  playdateHostName,
} from './pickups.service';
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
      assignee: { kind: 0, guardianId: 'guardian-1' },
      date: '2026-08-26',
      slot: 'DropOff',
      time: null,
      notes: '',
      assignedBy: 'guardian-1',
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
        (r) =>
          r.url === `${base()}/schedule` &&
          r.params.get('from') === '2026-08-01' &&
          r.params.get('to') === '2026-08-31',
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
      const request: AssignPickupRequest = {
        assignee: { kind: 0, guardianId: 'guardian-2' },
        time: null,
        notes: '',
      };
      const created = occurrence({ assignee: { kind: 0, guardianId: 'guardian-2' } });

      const promise = service.assignPickup(childId, '2026-08-26', 'PickUp', request);

      const req = httpMock.expectOne(
        (r) =>
          r.url === `${base()}/assignments` &&
          r.params.get('date') === '2026-08-26' &&
          r.params.get('slot') === 'PickUp',
      );
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('serializes the DropOff slot by name', async () => {
      const request: AssignPickupRequest = { assignee: { kind: 1 }, time: null, notes: '' };

      const promise = service.assignPickup(childId, '2026-08-26', 'DropOff', request);

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/assignments` && r.params.get('slot') === 'DropOff',
      );
      expect(req.request.params.get('slot')).toBe('DropOff');
      req.flush(occurrence({ assignee: { kind: 1 }, slot: 'DropOff' }));

      await promise;
    });

    it('sends a playdate assignment with its full detail fields', async () => {
      const request: AssignPickupRequest = {
        assignee: { kind: 3, hostName: 'Alex', location: 'Park', contactInfo: '555-1234' },
        time: '15:30',
        notes: 'Bring cleats',
      };
      const created = occurrence({
        assignee: { kind: 3, hostName: 'Alex', location: 'Park', contactInfo: '555-1234' },
        time: '15:30',
        notes: 'Bring cleats',
      });

      const promise = service.assignPickup(childId, '2026-08-26', 'PickUp', request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments`);
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('sends a sibling assignment', async () => {
      const request: AssignPickupRequest = {
        assignee: { kind: 2, siblingChildId: 'child-2' },
        time: null,
        notes: '',
      };
      const created = occurrence({ assignee: { kind: 2, siblingChildId: 'child-2' } });

      const promise = service.assignPickup(childId, '2026-08-26', 'PickUp', request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments`);
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('rejects when the assignment is refused', async () => {
      const request: AssignPickupRequest = {
        assignee: { kind: 0, guardianId: 'guardian-2' },
        time: null,
        notes: '',
      };

      const promise = service.assignPickup(childId, '2026-08-26', 'PickUp', request);

      const req = httpMock.expectOne((r) => r.url === `${base()}/assignments`);
      req.flush('conflict', { status: 409, statusText: 'Conflict' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('clearPickup', () => {
    it('DELETEs the assignment using date/slot params', async () => {
      const promise = service.clearPickup(childId, '2026-08-26', 'PickUp');

      const req = httpMock.expectOne(
        (r) =>
          r.url === `${base()}/assignments` &&
          r.params.get('date') === '2026-08-26' &&
          r.params.get('slot') === 'PickUp',
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
    });

    it('serializes the DropOff slot distinctly from PickUp', async () => {
      const promise = service.clearPickup(childId, '2026-08-26', 'DropOff');

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/assignments` && r.params.get('slot') === 'DropOff',
      );
      expect(req.request.params.get('slot')).toBe('DropOff');
      req.flush(null);

      await promise;
    });
  });

  describe('iCal tokens', () => {
    const tokensUrl = `${apiBaseUrl}/pickups/me/ical-tokens`;

    it('GETs the caller’s own token list', async () => {
      const tokens: PickupIcalTokenSummary[] = [
        { tokenId: 'token-1', issuedAt: '2026-10-01T00:00:00Z' },
      ];

      const promise = service.listIcalTokens();

      const req = httpMock.expectOne(tokensUrl);
      expect(req.request.method).toBe('GET');
      req.flush(tokens);

      await expect(promise).resolves.toEqual(tokens);
    });

    it('POSTs an empty body with an idempotency key and resolves with the issued token', async () => {
      const issued: IssuedPickupIcalToken = {
        tokenId: 'token-1',
        token: 'plaintext-secret',
        subscriptionPath: '/pickups/feeds/guardian-1/ical/plaintext-secret',
      };

      const promise = service.createIcalToken();

      const req = httpMock.expectOne(tokensUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
      req.flush(issued);

      await expect(promise).resolves.toEqual(issued);
    });

    it('DELETEs one token by id', async () => {
      const promise = service.revokeIcalToken('token-1');

      const req = httpMock.expectOne(`${tokensUrl}/token-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });

    it('prefixes a subscription path with the API base URL, issuing no request', () => {
      expect(service.icalFeedUrl('/pickups/feeds/guardian-1/ical/secret')).toBe(
        `${apiBaseUrl}/pickups/feeds/guardian-1/ical/secret`,
      );
    });
  });
});

describe('pickup assignee names', () => {
  it('reads the playdate host’s name, and nothing for any other kind', () => {
    expect(playdateHostName({ kind: 3, hostName: 'Alex', location: 'Park', contactInfo: '' })).toBe(
      'Alex',
    );
    expect(playdateHostName({ kind: 0, guardianId: 'guardian-1' })).toBe('');
  });

  it('reads the babysitter’s resolved name, and nothing for any other kind', () => {
    expect(babysitterName({ kind: 4, babysitterId: 'b1', guardianId: 'g1', name: 'Maja' })).toBe(
      'Maja',
    );
    expect(babysitterName({ kind: 4, babysitterId: 'b1', guardianId: 'g1', name: null })).toBe('');
    expect(babysitterName({ kind: 1 })).toBe('');
  });
});
