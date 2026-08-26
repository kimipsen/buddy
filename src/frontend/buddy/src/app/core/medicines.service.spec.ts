import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { MedicineDoseOccurrence, MedicineSchedule, MedicinesService } from './medicines.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('MedicinesService', () => {
  let service: MedicinesService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const childId = 'child-1';

  function base(): string {
    return `${apiBaseUrl}/medicines/children/${childId}`;
  }

  function schedule(overrides: Partial<MedicineSchedule> = {}): MedicineSchedule {
    return {
      id: 'med-1',
      childId,
      name: 'Amoxicillin',
      dosage: '5ml',
      icon: '💊',
      color: '#f00',
      times: ['08:00', '20:00'],
      startDate: '2026-08-01',
      endDate: null,
      isStopped: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      ...overrides
    };
  }

  function dose(overrides: Partial<MedicineDoseOccurrence> = {}): MedicineDoseOccurrence {
    return {
      medicineId: 'med-1',
      name: 'Amoxicillin',
      dosage: '5ml',
      icon: '💊',
      color: '#f00',
      date: '2026-08-26',
      time: '08:00',
      status: 0,
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

    service = TestBed.inject(MedicinesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listSchedules', () => {
    it('GETs schedules for a child', async () => {
      const schedules = [schedule()];

      const promise = service.listSchedules(childId);

      const req = httpMock.expectOne(`${base()}/schedules`);
      expect(req.request.method).toBe('GET');
      req.flush(schedules);

      await expect(promise).resolves.toEqual(schedules);
    });

    it('returns an empty array when the child has no schedules', async () => {
      const promise = service.listSchedules(childId);

      const req = httpMock.expectOne(`${base()}/schedules`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('rejects on an error response', async () => {
      const promise = service.listSchedules(childId);

      const req = httpMock.expectOne(`${base()}/schedules`);
      req.flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('createSchedule', () => {
    it('POSTs the create request and returns the created schedule', async () => {
      const request = { name: 'Amoxicillin', dosage: '5ml', icon: '💊', color: '#f00', times: ['08:00', '20:00'], startDate: '2026-08-01' };
      const created = schedule();

      const promise = service.createSchedule(childId, request);

      const req = httpMock.expectOne(`${base()}/schedules`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('serializes an explicit endDate in the request body', async () => {
      const request = {
        name: 'Amoxicillin',
        dosage: '5ml',
        icon: '💊',
        color: '#f00',
        times: ['08:00'],
        startDate: '2026-08-01',
        endDate: '2026-08-15'
      };

      const promise = service.createSchedule(childId, request);

      const req = httpMock.expectOne(`${base()}/schedules`);
      expect(req.request.body.endDate).toBe('2026-08-15');
      req.flush(schedule({ endDate: '2026-08-15' }));

      await promise;
    });
  });

  describe('updateScheduleDetails', () => {
    it('PATCHes the details endpoint and returns the updated schedule', async () => {
      const request = { name: 'Amoxicillin XR', dosage: '10ml', icon: '💊', color: '#0f0' };
      const updated = schedule({ name: 'Amoxicillin XR', dosage: '10ml', color: '#0f0' });

      const promise = service.updateScheduleDetails(childId, 'med-1', request);

      const req = httpMock.expectOne(`${base()}/schedules/med-1/details`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual(request);
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
    });
  });

  describe('rescheduleSchedule', () => {
    it('PATCHes the schedule endpoint with times/startDate/endDate', async () => {
      const request = { times: ['09:00'], startDate: '2026-09-01', endDate: '2026-09-30' };
      const updated = schedule({ times: ['09:00'], startDate: '2026-09-01', endDate: '2026-09-30' });

      const promise = service.rescheduleSchedule(childId, 'med-1', request);

      const req = httpMock.expectOne(`${base()}/schedules/med-1/schedule`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual(request);
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
    });

    it('allows endDate to be omitted (open-ended schedule)', async () => {
      const request = { times: ['09:00'], startDate: '2026-09-01' };

      const promise = service.rescheduleSchedule(childId, 'med-1', request);

      const req = httpMock.expectOne(`${base()}/schedules/med-1/schedule`);
      expect(req.request.body.endDate).toBeUndefined();
      req.flush(schedule({ endDate: null }));

      await promise;
    });
  });

  describe('stopSchedule', () => {
    it('DELETEs the schedule', async () => {
      const promise = service.stopSchedule(childId, 'med-1');

      const req = httpMock.expectOne(`${base()}/schedules/med-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
    });

    it('rejects when the backend refuses to stop the schedule', async () => {
      const promise = service.stopSchedule(childId, 'med-1');

      const req = httpMock.expectOne(`${base()}/schedules/med-1`);
      req.flush('nope', { status: 403, statusText: 'Forbidden' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('listDoses', () => {
    it('GETs doses with from/to params', async () => {
      const doses = [dose()];

      const promise = service.listDoses(childId, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/doses` && r.params.get('from') === '2026-08-01' && r.params.get('to') === '2026-08-31'
      );
      expect(req.request.method).toBe('GET');
      req.flush(doses);

      await expect(promise).resolves.toEqual(doses);
    });

    it('returns an empty array when there are no doses in range', async () => {
      const promise = service.listDoses(childId, '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne((r) => r.url === `${base()}/doses`);
      req.flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('setDoseStatus', () => {
    it('PUTs status with date/time params and returns the updated occurrence', async () => {
      const updated = dose({ status: 1 });

      const promise = service.setDoseStatus(childId, 'med-1', '2026-08-26', '08:00', 1);

      const req = httpMock.expectOne(
        (r) => r.url === `${base()}/doses/med-1` && r.params.get('date') === '2026-08-26' && r.params.get('time') === '08:00'
      );
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ status: 1 });
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
    });

    it('serializes DoseStatus 0 (Pending) distinctly from Skipped (2)', async () => {
      const pendingPromise = service.setDoseStatus(childId, 'med-1', '2026-08-26', '08:00', 0);
      const pendingReq = httpMock.expectOne((r) => r.url === `${base()}/doses/med-1`);
      expect(pendingReq.request.body).toEqual({ status: 0 });
      pendingReq.flush(dose({ status: 0 }));
      await pendingPromise;

      const skippedPromise = service.setDoseStatus(childId, 'med-1', '2026-08-26', '20:00', 2);
      const skippedReq = httpMock.expectOne((r) => r.url === `${base()}/doses/med-1`);
      expect(skippedReq.request.body).toEqual({ status: 2 });
      skippedReq.flush(dose({ status: 2, time: '20:00' }));
      await skippedPromise;
    });
  });

  describe('shareWithGroup', () => {
    it('PUTs an empty body to the group-share endpoint', async () => {
      const promise = service.shareWithGroup(childId, 'group-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/medicines/children/${childId}/group-share/group-1`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({});
      req.flush(null);

      await promise;
    });
  });

  describe('unshareFromGroup', () => {
    it('DELETEs the group-share relationship', async () => {
      const promise = service.unshareFromGroup(childId, 'group-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/medicines/children/${childId}/group-share/group-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await promise;
    });
  });

  describe('getSharedGroup', () => {
    it('returns the group when schedules are shared', async () => {
      const promise = service.getSharedGroup(childId);

      const req = httpMock.expectOne(`${apiBaseUrl}/medicines/children/${childId}/group-share`);
      expect(req.request.method).toBe('GET');
      req.flush({ groupId: 'group-1', groupName: 'The Fam' });

      await expect(promise).resolves.toEqual({ groupId: 'group-1', groupName: 'The Fam' });
    });

    it('returns null when not shared', async () => {
      const promise = service.getSharedGroup(childId);

      const req = httpMock.expectOne(`${apiBaseUrl}/medicines/children/${childId}/group-share`);
      req.flush({ groupId: null, groupName: null });

      await expect(promise).resolves.toBeNull();
    });
  });
});
