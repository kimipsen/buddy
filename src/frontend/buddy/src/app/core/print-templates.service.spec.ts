import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  PRINT_ROW_KIND,
  PrintTemplate,
  PrintTemplatesService,
  emptyRow,
} from './print-templates.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('PrintTemplatesService', () => {
  let service: PrintTemplatesService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const base = `${apiBaseUrl}/print-templates`;

  const template: PrintTemplate = {
    id: 't-1',
    ownerUserId: 'me',
    ownerGroupId: null,
    name: 'Ugeplan',
    paperSize: 'A4',
    defaultStartWeekday: 'Monday',
    showWeekNumber: true,
    rows: [],
    guardianColors: [],
    babysitterColors: [],
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

    service = TestBed.inject(PrintTemplatesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the template list', async () => {
    const promise = service.list();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 't-1', ownerUserId: 'me', ownerGroupId: null, name: 'Ugeplan' }]);

    await expect(promise).resolves.toHaveLength(1);
  });

  it('resolves the template list sorted by name', async () => {
    const weekend = { id: 't-2', ownerUserId: 'me', ownerGroupId: null, name: 'Weekend' };
    const school = { id: 't-1', ownerUserId: 'me', ownerGroupId: null, name: 'School week' };
    const promise = service.list();

    httpMock.expectOne(base).flush([weekend, school]);

    await expect(promise).resolves.toEqual([school, weekend]);
  });

  it('GETs one template', async () => {
    const promise = service.get('t-1');

    const req = httpMock.expectOne(`${base}/t-1`);
    expect(req.request.method).toBe('GET');
    req.flush(template);

    await expect(promise).resolves.toEqual(template);
  });

  it('POSTs a personal template with an idempotency key', async () => {
    const promise = service.create('Ugeplan');

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Ugeplan', groupId: null });
    expect(req.request.headers.get('Idempotency-Key')).toBeTruthy();
    req.flush(template);

    await expect(promise).resolves.toEqual(template);
  });

  it('POSTs a group template', async () => {
    const promise = service.create('Fælles', 'g-1');

    const req = httpMock.expectOne(base);
    expect(req.request.body).toEqual({ name: 'Fælles', groupId: 'g-1' });
    req.flush({ ...template, ownerUserId: null, ownerGroupId: 'g-1' });

    await promise;
  });

  it('PATCHes the name and the layout', async () => {
    const rename = service.rename('t-1', 'Skoleuge');
    const renameReq = httpMock.expectOne(`${base}/t-1/name`);
    expect(renameReq.request.method).toBe('PATCH');
    expect(renameReq.request.body).toEqual({ name: 'Skoleuge' });
    renameReq.flush(template);
    await rename;

    const layout = service.updateLayout('t-1', {
      paperSize: 'A3',
      defaultStartWeekday: 'Sunday',
      showWeekNumber: false,
    });
    const layoutReq = httpMock.expectOne(`${base}/t-1/layout`);
    expect(layoutReq.request.method).toBe('PATCH');
    expect(layoutReq.request.body).toEqual({
      paperSize: 'A3',
      defaultStartWeekday: 'Sunday',
      showWeekNumber: false,
    });
    layoutReq.flush(template);
    await layout;
  });

  it('PUTs the whole row list and the colors', async () => {
    const rows = [emptyRow(PRINT_ROW_KIND.blank, 'Noter')];
    const replaceRows = service.replaceRows('t-1', rows);
    const rowsReq = httpMock.expectOne(`${base}/t-1/rows`);
    expect(rowsReq.request.method).toBe('PUT');
    expect(rowsReq.request.body).toEqual({ rows });
    rowsReq.flush(template);
    await replaceRows;

    const colors = [{ guardianId: 'me', color: '#0ea5e9' }];
    const replaceColors = service.replaceColors('t-1', colors);
    const colorsReq = httpMock.expectOne(`${base}/t-1/colors`);
    expect(colorsReq.request.method).toBe('PUT');
    expect(colorsReq.request.body).toEqual({ colors });
    colorsReq.flush(template);
    await replaceColors;

    const babysitterColors = [{ guardianId: 'me', babysitterId: 'b-1', color: '#a855f7' }];
    const replaceBabysitterColors = service.replaceBabysitterColors('t-1', babysitterColors);
    const babysitterReq = httpMock.expectOne(`${base}/t-1/babysitter-colors`);
    expect(babysitterReq.request.method).toBe('PUT');
    expect(babysitterReq.request.body).toEqual({ colors: babysitterColors });
    babysitterReq.flush(template);
    await replaceBabysitterColors;
  });

  it('DELETEs a template', async () => {
    const promise = service.delete('t-1');

    const req = httpMock.expectOne(`${base}/t-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    await expect(promise).resolves.toBeNull();
  });

  it('builds an empty row with every kind-specific field cleared', () => {
    expect(emptyRow(PRINT_ROW_KIND.pickup, 'Hente')).toEqual({
      kind: 'Pickup',
      label: 'Hente',
      heightWeight: 1,
      childId: null,
      mealGroupId: null,
      mealSlot: null,
      guardianId: null,
      workLocationId: null,
      calendarIds: null,
      assignedToId: null,
      titleFilter: null,
      maxItems: null,
      showTime: false,
      showAssignee: false,
    });
  });
});
