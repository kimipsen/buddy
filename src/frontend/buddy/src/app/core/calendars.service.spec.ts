import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  AssignableMember,
  CalendarItemOccurrence,
  CalendarItemResponse,
  CalendarOccurrence,
  CalendarsService,
  CalendarSummary,
  CreateCalendarRequest,
  CreateItemRequest,
  IcalTokenSummary,
  IssuedIcalToken,
  RescheduleItemRequest,
  ScheduleTaskFromTemplateRequest,
  TaskCompletion,
  UpdateItemDetailsRequest
} from './calendars.service';
import { todayIsoDate } from './date-utils';
import { RuntimeConfigService } from './runtime-config.service';

describe('CalendarsService', () => {
  const apiBaseUrl = 'https://api.buddy.test';

  let service: CalendarsService;
  let httpMock: HttpTestingController;

  function calendar(overrides: Partial<CalendarSummary> = {}): CalendarSummary {
    return { id: 'cal-1', name: 'Home', icon: '🏠', role: 0, ...overrides };
  }

  // listOccurrencesInRange/listTodayOccurrences chain an `await` on the calendars response before
  // issuing the per-calendar occurrences requests. Resuming an `await` always costs at least one
  // microtask turn, so flushing the calendars request alone isn't enough to make the follow-up
  // requests observable to httpMock yet -- a macrotask flush (per docs/testing.md) drains any
  // depth of chained awaits reliably.
  async function flushMicrotasks(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }

  function occurrence(overrides: Partial<CalendarItemOccurrence> = {}): CalendarItemOccurrence {
    return {
      itemId: 'task-1',
      kind: 1,
      title: 'Clean room',
      icon: '🧹',
      iconOverride: null,
      color: '#000',
      startsAt: null,
      endsAt: null,
      dueAt: '2026-08-26T00:00:00Z',
      isAllDay: true,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      parentTitle: null,
      subtaskId: null,
      ...overrides
    };
  }

  beforeEach(() => {
    const runtimeConfigStub: Partial<RuntimeConfigService> = { apiBaseUrl };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: runtimeConfigStub }
      ]
    });

    service = TestBed.inject(CalendarsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listMyCalendars', () => {
    it('GETs the calendars list and resolves with the response body', async () => {
      const calendars = [calendar(), calendar({ id: 'cal-2', name: 'Work', role: 1 })];

      const promise = service.listMyCalendars();

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars`);
      expect(req.request.method).toBe('GET');
      req.flush(calendars);

      await expect(promise).resolves.toEqual(calendars);
    });

    it('resolves with an empty array when the caller has no calendars', async () => {
      const promise = service.listMyCalendars();

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('rejects when the server responds with an error status', async () => {
      const promise = service.listMyCalendars();
      // Swallow the unhandled-rejection warning until the assertion below awaits it.
      promise.catch(() => undefined);

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('createCalendar', () => {
    it('POSTs the request body and resolves with the created calendar', async () => {
      const request: CreateCalendarRequest = { name: 'Home', timeZoneId: 'UTC', groupId: 'group-1', icon: '🏠' };
      const created = calendar();

      const promise = service.createCalendar(request);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });
  });

  describe('updateCalendarIcon', () => {
    it('PATCHes the icon field to the calendar-scoped icon endpoint', async () => {
      const promise = service.updateCalendarIcon('cal-1', '🎉');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/icon`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ icon: '🎉' });
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('transferToGroup', () => {
    it('PUTs to the calendar/group endpoint with an empty body', async () => {
      const promise = service.transferToGroup('cal-1', 'group-2');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/group/group-2`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({});
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('deleteCalendar', () => {
    it('DELETEs the calendar', async () => {
      const promise = service.deleteCalendar('cal-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('listIcalTokens', () => {
    it('GETs the ical-tokens list for the calendar', async () => {
      const tokens: IcalTokenSummary[] = [{ tokenId: 'token-1', issuedAt: '2026-08-01T00:00:00Z' }];

      const promise = service.listIcalTokens('cal-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/ical-tokens`);
      expect(req.request.method).toBe('GET');
      req.flush(tokens);

      await expect(promise).resolves.toEqual(tokens);
    });

    it('resolves with an empty array when there are no tokens', async () => {
      const promise = service.listIcalTokens('cal-1');

      httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/ical-tokens`).flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('createIcalToken', () => {
    it('POSTs an empty body and resolves with the issued token', async () => {
      const issued: IssuedIcalToken = { tokenId: 'token-1', token: 'plaintext-secret', subscriptionPath: '/ical/token-1.ics' };

      const promise = service.createIcalToken('cal-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/ical-tokens`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush(issued);

      await expect(promise).resolves.toEqual(issued);
    });
  });

  describe('revokeIcalToken', () => {
    it('DELETEs the specific token under the calendar', async () => {
      const promise = service.revokeIcalToken('cal-1', 'token-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/ical-tokens/token-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeNull();
    });
  });

  describe('icalFeedUrl', () => {
    it('prefixes the relative subscription path with the configured API base URL, issuing no HTTP request', () => {
      // httpMock.verify() in afterEach would fail if this triggered a request.
      expect(service.icalFeedUrl('/ical/token-1.ics')).toBe(`${apiBaseUrl}/ical/token-1.ics`);
    });
  });

  describe('listOccurrences', () => {
    it('GETs occurrences for the calendar with from/to query params', async () => {
      const occurrences = [occurrence()];

      const promise = service.listOccurrences('cal-1', '2026-08-01', '2026-08-31');

      const req = httpMock.expectOne(
        (r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences` && r.params.get('from') === '2026-08-01' && r.params.get('to') === '2026-08-31'
      );
      expect(req.request.method).toBe('GET');
      req.flush(occurrences);

      await expect(promise).resolves.toEqual(occurrences);
    });

    it('resolves with an empty array when there are no occurrences in range', async () => {
      const promise = service.listOccurrences('cal-1', '2026-08-01', '2026-08-31');

      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`).flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('sends distinct from/to values as separate query params, not swapped or merged', async () => {
      const promise = service.listOccurrences('cal-1', 'from-value', 'to-value');
      promise.catch(() => undefined);

      const req = httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`);
      expect(req.request.params.get('from')).toBe('from-value');
      expect(req.request.params.get('to')).toBe('to-value');
      expect(req.request.params.get('from')).not.toBe(req.request.params.get('to'));
      req.flush([]);
    });
  });

  describe('listAssignableMembers', () => {
    it('GETs the assignable-members list for the calendar', async () => {
      const members: AssignableMember[] = [{ userId: 'child-1', givenName: 'Sam', familyName: 'Kid' }];

      const promise = service.listAssignableMembers('cal-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/assignable-members`);
      expect(req.request.method).toBe('GET');
      req.flush(members);

      await expect(promise).resolves.toEqual(members);
    });

    it('resolves with an empty array when nobody is assignable', async () => {
      const promise = service.listAssignableMembers('cal-1');

      httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/assignable-members`).flush([]);

      await expect(promise).resolves.toEqual([]);
    });
  });

  describe('createItem', () => {
    const request: CreateItemRequest = {
      kind: 1,
      title: 'Clean room',
      icon: null,
      color: '#000',
      startsAt: null,
      endsAt: null,
      dueDate: { date: '2026-08-26', time: '00:00' },
      isAllDay: true,
      recurrence: null,
      assignedTo: null
    };

    it('POSTs the item request under the calendar and resolves with the created item', async () => {
      const created: CalendarItemResponse = {
        id: 'task-1',
        calendarId: 'cal-1',
        kind: 1,
        title: 'Clean room',
        icon: null,
        color: '#000',
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: null
      };

      const promise = service.createItem('cal-1', request);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('invalidates the today-occurrences cache so a subsequent listTodayOccurrences re-fetches', async () => {
      const today = todayIsoDate();

      // Prime the cache with one round-trip.
      const firstToday = service.listTodayOccurrences();
      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([]);
      await expect(firstToday).resolves.toEqual([]);

      // A second call before any mutation should be served from cache -- no new HTTP traffic.
      const cachedToday = service.listTodayOccurrences();
      httpMock.expectNone(`${apiBaseUrl}/calendars`);
      await expect(cachedToday).resolves.toEqual([]);

      // Creating an item clears the cache.
      const createPromise = service.createItem('cal-1', request);
      httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items`).flush({
        id: 'task-1',
        calendarId: 'cal-1',
        kind: 1,
        title: 'Clean room',
        icon: null,
        color: '#000',
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: null
      } satisfies CalendarItemResponse);
      await createPromise;

      // So the next listTodayOccurrences fetches again instead of reusing the stale promise.
      const afterCreate = service.listTodayOccurrences();
      const calendarsReq = httpMock.expectOne(`${apiBaseUrl}/calendars`);
      calendarsReq.flush([calendar()]);
      await flushMicrotasks();
      const occurrencesReq = httpMock.expectOne(
        (r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences` && r.params.get('from') === today && r.params.get('to') === today
      );
      occurrencesReq.flush([occurrence()]);

      await expect(afterCreate).resolves.toEqual([{ ...occurrence(), calendarId: 'cal-1', calendarName: 'Home' }]);
    });
  });

  describe('updateItemDetails', () => {
    const request: UpdateItemDetailsRequest = { title: 'Tidy room', icon: null, color: '#111' };

    it('PATCHes the item details endpoint and resolves with the updated item', async () => {
      const updated: CalendarItemResponse = {
        id: 'task-1',
        calendarId: 'cal-1',
        kind: 1,
        title: 'Tidy room',
        icon: null,
        color: '#111',
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: null
      };

      const promise = service.updateItemDetails('cal-1', 'task-1', request);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/task-1/details`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual(request);
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
    });
  });

  describe('rescheduleItem', () => {
    const request: RescheduleItemRequest = {
      startsAt: null,
      endsAt: null,
      dueDate: { date: '2026-08-27', time: '00:00' },
      isAllDay: true
    };

    it('PATCHes the item schedule endpoint and resolves with the updated item', async () => {
      const updated: CalendarItemResponse = {
        id: 'task-1',
        calendarId: 'cal-1',
        kind: 1,
        title: 'Clean room',
        icon: null,
        color: '#000',
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: null
      };

      const promise = service.rescheduleItem('cal-1', 'task-1', request);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/task-1/schedule`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual(request);
      req.flush(updated);

      await expect(promise).resolves.toEqual(updated);
    });
  });

  describe('deleteItem', () => {
    it('DELETEs the item under the calendar and resolves void', async () => {
      const promise = service.deleteItem('cal-1', 'task-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/task-1`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      await expect(promise).resolves.toBeUndefined();
    });
  });

  describe('setTaskCompletion', () => {
    it('PATCHes the completion endpoint with date, isCompleted, and a null subtaskId when none is given, and resolves with the result', async () => {
      const completion: TaskCompletion = { itemId: 'task-1', occurrenceDate: '2026-08-26', isCompleted: true };

      const promise = service.setTaskCompletion('cal-1', 'task-1', '2026-08-26', true);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/task-1/completion`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ date: '2026-08-26', isCompleted: true, subtaskId: null });
      req.flush(completion);

      await expect(promise).resolves.toEqual(completion);
    });

    it('sends isCompleted:false as-is, not coerced or dropped', async () => {
      const completion: TaskCompletion = { itemId: 'task-1', occurrenceDate: '2026-08-26', isCompleted: false };

      const promise = service.setTaskCompletion('cal-1', 'task-1', '2026-08-26', false);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/task-1/completion`);
      expect(req.request.body).toEqual({ date: '2026-08-26', isCompleted: false, subtaskId: null });
      req.flush(completion);

      await expect(promise).resolves.toEqual(completion);
    });

    it('threads a given subtaskId through to the request body, to complete one subtask of a template-scheduled task', async () => {
      const completion: TaskCompletion = { itemId: 'task-1', occurrenceDate: '2026-08-26', isCompleted: true };

      const promise = service.setTaskCompletion('cal-1', 'task-1', '2026-08-26', true, 'subtask-1');

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/task-1/completion`);
      expect(req.request.body).toEqual({ date: '2026-08-26', isCompleted: true, subtaskId: 'subtask-1' });
      req.flush(completion);

      await expect(promise).resolves.toEqual(completion);
    });
  });

  describe('scheduleTaskFromTemplate', () => {
    const request: ScheduleTaskFromTemplateRequest = {
      taskTemplateId: 'template-1',
      startDate: '2026-08-27',
      startTime: '08:00:00',
      recurrence: null,
      assignedTo: 'child-1',
      title: 'Morning routine',
      icon: '🌅',
      color: '#10b981'
    };

    it('POSTs the request under the calendar\'s from-template endpoint and resolves with the created item', async () => {
      const created: CalendarItemResponse = {
        id: 'task-1',
        calendarId: 'cal-1',
        kind: 1,
        title: 'Morning routine',
        icon: '🌅',
        color: '#10b981',
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: 'child-1'
      };

      const promise = service.scheduleTaskFromTemplate('cal-1', request);

      const req = httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/from-template`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(created);

      await expect(promise).resolves.toEqual(created);
    });

    it('invalidates the today-occurrences cache so a subsequent listTodayOccurrences re-fetches', async () => {
      const today = todayIsoDate();

      const firstToday = service.listTodayOccurrences();
      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([]);
      await expect(firstToday).resolves.toEqual([]);

      const cachedToday = service.listTodayOccurrences();
      httpMock.expectNone(`${apiBaseUrl}/calendars`);
      await expect(cachedToday).resolves.toEqual([]);

      const schedulePromise = service.scheduleTaskFromTemplate('cal-1', request);
      httpMock.expectOne(`${apiBaseUrl}/calendars/cal-1/items/from-template`).flush({
        id: 'task-1',
        calendarId: 'cal-1',
        kind: 1,
        title: 'Morning routine',
        icon: '🌅',
        color: '#10b981',
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: 'child-1'
      } satisfies CalendarItemResponse);
      await schedulePromise;

      const afterSchedule = service.listTodayOccurrences();
      const calendarsReq = httpMock.expectOne(`${apiBaseUrl}/calendars`);
      calendarsReq.flush([calendar()]);
      await flushMicrotasks();
      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences` && r.params.get('from') === today && r.params.get('to') === today).flush(
        []
      );

      await expect(afterSchedule).resolves.toEqual([]);
    });
  });

  describe('listOccurrencesInRange', () => {
    it('fans out to every calendar and tags each occurrence with its owning calendar id/name', async () => {
      const calendars = [calendar({ id: 'cal-1', name: 'Home' }), calendar({ id: 'cal-2', name: 'Work' })];
      const homeOccurrence = occurrence({ itemId: 'home-task' });
      const workOccurrence = occurrence({ itemId: 'work-task' });

      const promise = service.listOccurrencesInRange('2026-08-01', '2026-08-31');

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush(calendars);
      await flushMicrotasks();

      const homeReq = httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`);
      expect(homeReq.request.params.get('from')).toBe('2026-08-01');
      expect(homeReq.request.params.get('to')).toBe('2026-08-31');
      homeReq.flush([homeOccurrence]);

      const workReq = httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-2/occurrences`);
      workReq.flush([workOccurrence]);

      const result = await promise;
      expect(result).toEqual([
        { ...homeOccurrence, calendarId: 'cal-1', calendarName: 'Home' },
        { ...workOccurrence, calendarId: 'cal-2', calendarName: 'Work' }
      ] satisfies CalendarOccurrence[]);
    });

    it('resolves with an empty array when the caller has no calendars at all', async () => {
      const promise = service.listOccurrencesInRange('2026-08-01', '2026-08-31');

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('resolves with an empty array when calendars exist but none have occurrences in range', async () => {
      const promise = service.listOccurrencesInRange('2026-08-01', '2026-08-31');

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([calendar()]);
      await flushMicrotasks();
      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`).flush([]);

      await expect(promise).resolves.toEqual([]);
    });

    it('is not memoized -- each call re-fetches the calendar list and occurrences', async () => {
      const first = service.listOccurrencesInRange('2026-08-01', '2026-08-31');
      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([calendar()]);
      await flushMicrotasks();
      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`).flush([]);
      await first;

      const second = service.listOccurrencesInRange('2026-08-01', '2026-08-31');
      // If the result were memoized like listTodayOccurrences, this second round of requests
      // would never be observed and expectOne would fail with "no open request found".
      const secondCalendarsReq = httpMock.expectOne(`${apiBaseUrl}/calendars`);
      secondCalendarsReq.flush([calendar()]);
      await flushMicrotasks();
      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`).flush([]);

      expect(secondCalendarsReq.request.method).toBe('GET');
      await expect(second).resolves.toEqual([]);
    });

    it('rejects if any per-calendar occurrences request fails', async () => {
      const promise = service.listOccurrencesInRange('2026-08-01', '2026-08-31');
      promise.catch(() => undefined);

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([calendar()]);
      await flushMicrotasks();
      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`).flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('listTodayOccurrences', () => {
    it('fetches every calendar and today\'s occurrences, tagging each with its owning calendar', async () => {
      const today = todayIsoDate();
      const calendars = [calendar()];
      const todaysOccurrence = occurrence();

      const promise = service.listTodayOccurrences();

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush(calendars);
      await flushMicrotasks();

      const req = httpMock.expectOne(
        (r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences` && r.params.get('from') === today && r.params.get('to') === today
      );
      req.flush([todaysOccurrence]);

      await expect(promise).resolves.toEqual([{ ...todaysOccurrence, calendarId: 'cal-1', calendarName: 'Home' }]);
    });

    it('memoizes concurrent calls on the same day into a single fan-out', async () => {
      const first = service.listTodayOccurrences();
      const second = service.listTodayOccurrences();

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([calendar()]);
      await flushMicrotasks();
      httpMock.expectOne((r) => r.url === `${apiBaseUrl}/calendars/cal-1/occurrences`).flush([]);

      const [firstResult, secondResult] = await Promise.all([first, second]);
      expect(firstResult).toEqual([]);
      expect(secondResult).toEqual([]);
      expect(firstResult).toBe(secondResult);

      // A follow-up call the same day is served from the memoized promise -- no further HTTP calls.
      const third = service.listTodayOccurrences();
      httpMock.expectNone(`${apiBaseUrl}/calendars`);
      await expect(third).resolves.toEqual([]);
    });

    it('clears the memoized cache on failure so the next call retries against the network', async () => {
      const first = service.listTodayOccurrences();
      first.catch(() => undefined);

      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush('boom', { status: 500, statusText: 'Server Error' });

      await expect(first).rejects.toBeTruthy();

      const second = service.listTodayOccurrences();
      httpMock.expectOne(`${apiBaseUrl}/calendars`).flush([]);

      await expect(second).resolves.toEqual([]);
    });
  });
});
