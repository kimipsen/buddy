import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { Babysitter, BabysittersService, ChildBabysitter } from './babysitters.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('BabysittersService', () => {
  let service: BabysittersService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const me = `${apiBaseUrl}/babysitters/me`;

  const anna: Babysitter = { id: 'b-1', name: 'Anna', contactInfo: '123', isArchived: false };

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

    service = TestBed.inject(BabysittersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the caller’s own babysitters', async () => {
    const promise = service.listMine();

    const req = httpMock.expectOne(me);
    expect(req.request.method).toBe('GET');
    req.flush([anna]);

    await expect(promise).resolves.toEqual([anna]);
  });

  it('GETs the babysitters a child’s guardians can pick from', async () => {
    const entry: ChildBabysitter = { guardianId: 'g-1', id: 'b-1', name: 'Anna', contactInfo: '' };
    const promise = service.listForChild('c-1');

    const req = httpMock.expectOne(`${apiBaseUrl}/babysitters/children/c-1`);
    expect(req.request.method).toBe('GET');
    req.flush([entry]);

    await expect(promise).resolves.toEqual([entry]);
  });

  it('POSTs a new babysitter with an idempotency key', async () => {
    const promise = service.add({ name: 'Anna', contactInfo: '123' });

    const req = httpMock.expectOne(me);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Anna', contactInfo: '123' });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(anna);

    await expect(promise).resolves.toEqual(anna);
  });

  it('PATCHes a babysitter’s details', async () => {
    const promise = service.update('b-1', { name: 'Anne', contactInfo: '' });

    const req = httpMock.expectOne(`${me}/b-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ name: 'Anne', contactInfo: '' });
    req.flush({ ...anna, name: 'Anne', contactInfo: '' });

    await expect(promise).resolves.toEqual({ ...anna, name: 'Anne', contactInfo: '' });
  });

  it('DELETEs (archives) a babysitter', async () => {
    const promise = service.archive('b-1');

    const req = httpMock.expectOne(`${me}/b-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    await promise;
  });
});
