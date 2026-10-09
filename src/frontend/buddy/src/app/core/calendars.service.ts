import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { todayIsoDate } from './date-utils';
import { sortByFullName, sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import { PER_ITEM_REQUEST_CONCURRENCY, mapWithConcurrency } from './map-with-concurrency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type CalendarRole = Schemas['CalendarRole'];

export type CalendarItemKind = Schemas['CalendarItemKind'];

export type RecurrenceFrequency = Schemas['RecurrenceFrequency'];

export type Weekday = Schemas['DayOfWeek'];

export type CalendarSummary = Schemas['CalendarSummaryResponse'];

// GET /calendars/{id}: one calendar with its owning group (CalendarResponse).
export type CalendarDetail = Schemas['CalendarResponse'];

export type IcalTokenSummary = Schemas['IcalTokenSummary'];

// Returned exactly once, at creation -- the plaintext token is never retrievable again after this.
export type IssuedIcalToken = Schemas['IcalTokenResponse'];

export type CreateCalendarRequest = Schemas['CreateCalendarRequest'];

export type UpdateCalendarIconRequest = Schemas['UpdateCalendarIconRequest'];

export type DatePart = Schemas['StartsAt'];

// weekdays: null or absent means no filter (daily: every day; weekly: the start date's weekday).
// Otherwise the days a daily rule is limited to (intervalCount must be 1), or the days a weekly
// rule repeats on every intervalCount weeks. Not allowed for monthly/yearly.
export type RecurrenceRuleRequest = Schemas['RecurrenceRuleRequest'];

// When an item happens, discriminated by `kind` (0 = event, 1 = task) like the backend's
// ItemTimingRequest. isAllDay=true makes the time-of-day in startsAt/endsAt/dueDate a sentinel.
export type ItemTiming = Schemas['ItemTimingRequest'];

// A new item's schedule: a task may also be assigned to someone (null means unassigned).
export type ItemSchedule = Schemas['ItemScheduleRequest'];

export type CreateItemRequest = Schemas['CreateItemRequest'];

// Someone who could be assigned a task on a calendar: an explicit per-calendar grant, or -- for a
// group-owned calendar -- any member of that group.
export type AssignableMember = Schemas['AssignableMemberResponse'];

export type UpdateItemDetailsRequest = Schemas['UpdateItemDetailsRequest'];

// The timing must match the item's own kind -- an event is rescheduled with an event timing.
export type RescheduleItemRequest = Schemas['RescheduleItemRequest'];

// Matches ScheduleTaskFromTemplateRequest exactly. startDate/startTime are flat DateOnly/TimeOnly
// fields (not a nested DatePart like CreateItemRequest) -- System.Text.Json's built-in converters
// serialize DateOnly as "yyyy-MM-dd" and TimeOnly as "HH:mm:ss", matching todayIsoDate() and the
// seconds-appended convention ManageMedicines/TimeSelect already use for TimeOnly-backed fields.
export type ScheduleTaskFromTemplateRequest = Schemas['ScheduleTaskFromTemplateRequest'];

export type CalendarItemResponse = Schemas['CalendarItemResponse'];

export type CalendarItemOccurrence = Schemas['CalendarItemOccurrence'];

export type OccurrenceTiming = Schemas['OccurrenceTiming'];

// title on the occurrence is the subtask's own; parentTitle and parentIcon (the parent's
// effective icon, which a grouped run's header uses instead of any one subtask's icon) let a
// client group a routine's subtask occurrences. subtaskId targets setTaskCompletion.
export type Routine = Schemas['Routine'];

export type TaskCompletion = Schemas['TaskCompletionResponse'];

export type CalendarOccurrence = CalendarItemOccurrence & {
  calendarId: string;
  calendarName: string;
};

@Injectable({ providedIn: 'root' })
export class CalendarsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private todayCache: { date: string; promise: Promise<CalendarOccurrence[]> } | null = null;

  listMyCalendars(): Promise<CalendarSummary[]> {
    return firstValueFrom(
      this.http.get<CalendarSummary[]>(`${this.runtimeConfig.apiBaseUrl}/calendars`),
    ).then(sortByName);
  }

  getCalendar(calendarId: string): Promise<CalendarDetail> {
    return firstValueFrom(
      this.http.get<CalendarDetail>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}`),
    );
  }

  createCalendar(request: CreateCalendarRequest): Promise<CalendarSummary> {
    return firstValueFrom(
      postIdempotent<CalendarSummary>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/calendars`,
        request,
      ),
    );
  }

  // Owner-only -- the calendar's icon is the one detail that can change after creation today.
  updateCalendarIcon(calendarId: string, icon: string): Promise<void> {
    return firstValueFrom(
      this.http.patch<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/icon`, {
        icon,
      } satisfies UpdateCalendarIconRequest),
    );
  }

  // Moves an already-existing calendar to a different group -- the one exception to ownership
  // otherwise being fixed at creation. Requires the caller to own the calendar and manage the
  // destination group (two-sided consent, gated server-side).
  transferToGroup(calendarId: string, groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/group/${groupId}`,
        {},
      ),
    );
  }

  deleteCalendar(calendarId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}`),
    );
  }

  listIcalTokens(calendarId: string): Promise<IcalTokenSummary[]> {
    return firstValueFrom(
      this.http.get<IcalTokenSummary[]>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/ical-tokens`,
      ),
    );
  }

  createIcalToken(calendarId: string): Promise<IssuedIcalToken> {
    return firstValueFrom(
      postIdempotent<IssuedIcalToken>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/ical-tokens`,
        {},
      ),
    );
  }

  revokeIcalToken(calendarId: string, tokenId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/ical-tokens/${tokenId}`,
      ),
    );
  }

  // subscriptionPath is relative, in the same style as every other endpoint path on this
  // service -- prefix with apiBaseUrl to get the URL a calendar app can subscribe to.
  icalFeedUrl(subscriptionPath: string): string {
    return `${this.runtimeConfig.apiBaseUrl}${subscriptionPath}`;
  }

  listOccurrences(calendarId: string, from: string, to: string): Promise<CalendarItemOccurrence[]> {
    return firstValueFrom(
      this.http.get<CalendarItemOccurrence[]>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/occurrences`,
        {
          params: { from, to },
        },
      ),
    );
  }

  listAssignableMembers(calendarId: string): Promise<AssignableMember[]> {
    return firstValueFrom(
      this.http.get<AssignableMember[]>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/assignable-members`,
      ),
    ).then(sortByFullName);
  }

  async createItem(calendarId: string, request: CreateItemRequest): Promise<CalendarItemResponse> {
    const created = await firstValueFrom(
      postIdempotent<CalendarItemResponse>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items`,
        request,
      ),
    );
    this.todayCache = null;
    return created;
  }

  async updateItemDetails(
    calendarId: string,
    itemId: string,
    request: UpdateItemDetailsRequest,
  ): Promise<CalendarItemResponse> {
    const updated = await firstValueFrom(
      this.http.patch<CalendarItemResponse>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}/details`,
        request,
      ),
    );
    this.todayCache = null;
    return updated;
  }

  async rescheduleItem(
    calendarId: string,
    itemId: string,
    request: RescheduleItemRequest,
  ): Promise<CalendarItemResponse> {
    const updated = await firstValueFrom(
      this.http.patch<CalendarItemResponse>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}/schedule`,
        request,
      ),
    );
    this.todayCache = null;
    return updated;
  }

  async deleteItem(calendarId: string, itemId: string): Promise<void> {
    await firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}`,
      ),
    );
    this.todayCache = null;
  }

  // subtaskId completes one subtask of a template-scheduled task (its own route); omitted (null),
  // the plain task is completed as a whole. The API rejects the wrong one for the task's kind.
  async setTaskCompletion(
    calendarId: string,
    itemId: string,
    date: string,
    isCompleted: boolean,
    subtaskId: string | null = null,
  ): Promise<TaskCompletion> {
    const itemUrl = `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}`;
    const url =
      subtaskId === null ? `${itemUrl}/completion` : `${itemUrl}/subtasks/${subtaskId}/completion`;
    const completion = await firstValueFrom(
      this.http.patch<TaskCompletion>(url, { date, isCompleted }),
    );
    this.todayCache = null;
    return completion;
  }

  // The calendar-item analog of createItem for a Task whose subtasks come from a TaskLibrary
  // template instead of being entered by hand -- see ScheduleTaskFromTemplate.Command.cs.
  async scheduleTaskFromTemplate(
    calendarId: string,
    request: ScheduleTaskFromTemplateRequest,
  ): Promise<CalendarItemResponse> {
    const created = await firstValueFrom(
      postIdempotent<CalendarItemResponse>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/from-template`,
        request,
      ),
    );
    this.todayCache = null;
    return created;
  }

  /**
   * Lists today's occurrences across every calendar the guardian belongs to. The in-flight
   * promise is memoized per day so concurrent callers on the same page (e.g. the tasks and
   * events dashboard widgets) collapse into a single fan-out instead of one each.
   */
  listTodayOccurrences(): Promise<CalendarOccurrence[]> {
    const today = todayIsoDate();

    if (this.todayCache?.date !== today) {
      const promise = this.listOccurrencesInRange(today, today).catch((error: unknown) => {
        if (this.todayCache?.promise === promise) {
          this.todayCache = null;
        }
        throw error;
      });
      this.todayCache = { date: today, promise };
    }

    return this.todayCache.promise;
  }

  /**
   * Lists occurrences across every calendar the caller belongs to for an arbitrary date range,
   * tagging each with the owning calendar's id and name. Not memoized -- unlike
   * `listTodayOccurrences`, this is called on demand for whatever range the caller is currently
   * viewing (e.g. an agenda's visible week), so a same-day cache doesn't apply here.
   */
  async listOccurrencesInRange(from: string, to: string): Promise<CalendarOccurrence[]> {
    const calendars = await this.listMyCalendars();

    // One request per calendar (no cross-calendar range endpoint), bounded so a guardian in many
    // calendars doesn't burst the API. Still rejects if any calendar fails, as before.
    const perCalendar = await mapWithConcurrency(
      calendars,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (calendar) => {
        const occurrences = await this.listOccurrences(calendar.id, from, to);
        return occurrences.map((occurrence) => ({
          ...occurrence,
          calendarId: calendar.id,
          calendarName: calendar.name,
        }));
      },
    );

    return perCalendar.flat();
  }
}
