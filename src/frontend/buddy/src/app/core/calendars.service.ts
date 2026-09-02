import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { todayIsoDate } from './date-utils';
import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// CalendarRole/CalendarItemKind values match the backend's enum ordinals (no string enum
// converter is registered server-side): CalendarRole 0 = Owner, 1 = Contributor, 2 = Viewer.
export type CalendarRole = 0 | 1 | 2;

// CalendarItemKind 0 = Event, 1 = Task.
export type CalendarItemKind = 0 | 1;

// RecurrenceFrequency 0 = Daily, 1 = Weekly, 2 = Monthly, 3 = Yearly.
export type RecurrenceFrequency = 0 | 1 | 2 | 3;

export interface CalendarSummary {
  id: string;
  name: string;
  icon: string;
  role: CalendarRole;
}

export interface IcalTokenSummary {
  tokenId: string;
  issuedAt: string;
}

// Returned exactly once, at creation -- the plaintext token is never retrievable again after this.
export interface IssuedIcalToken {
  tokenId: string;
  token: string;
  subscriptionPath: string;
}

export interface CreateCalendarRequest {
  name: string;
  timeZoneId: string;
  // Required -- a calendar is always group-owned now, there's no personal-calendar option.
  groupId: string;
  // Omitted/null falls back to the backend's default icon.
  icon?: string | null;
}

export interface UpdateCalendarIconRequest {
  icon: string;
}

export interface DatePart {
  date: string;
  time: string;
}

export interface RecurrenceRuleRequest {
  frequency: RecurrenceFrequency;
  intervalCount: number;
  until: string | null;
}

export interface CreateItemRequest {
  kind: CalendarItemKind;
  title: string;
  // null means "inherit the owning calendar's icon" -- the item stores no override.
  icon: string | null;
  color: string;
  // Event requires startsAt+endsAt; task requires dueDate -- the other pair stays null.
  startsAt: DatePart | null;
  endsAt: DatePart | null;
  dueDate: DatePart | null;
  // When true, the time-of-day in startsAt/endsAt/dueDate is a sentinel and should be ignored.
  isAllDay: boolean;
  recurrence: RecurrenceRuleRequest | null;
  // Only meaningful for a Task -- ignored for an Event. Null means unassigned.
  assignedTo: string | null;
}

// Someone who could be assigned a task on a calendar: an explicit per-calendar grant, or -- for a
// group-owned calendar -- any member of that group.
export interface AssignableMember {
  userId: string;
  givenName: string;
  familyName: string;
}

export interface UpdateItemDetailsRequest {
  title: string;
  // null clears any override, reverting to the owning calendar's icon.
  icon: string | null;
  color: string;
}

export interface RescheduleItemRequest {
  // Same Event-vs-Task invariant as CreateItemRequest -- exactly one pair is set.
  startsAt: DatePart | null;
  endsAt: DatePart | null;
  dueDate: DatePart | null;
  isAllDay: boolean;
}

// Matches ScheduleTaskFromTemplateRequest exactly. startDate/startTime are flat DateOnly/TimeOnly
// fields (not a nested DatePart like CreateItemRequest) -- System.Text.Json's built-in converters
// serialize DateOnly as "yyyy-MM-dd" and TimeOnly as "HH:mm:ss", matching todayIsoDate() and the
// seconds-appended convention ManageMedicines/TimeSelect already use for TimeOnly-backed fields.
export interface ScheduleTaskFromTemplateRequest {
  taskTemplateId: string;
  startDate: string;
  startTime: string;
  recurrence: RecurrenceRuleRequest | null;
  assignedTo: string | null;
  title: string;
  icon: string | null;
  color: string;
}

export interface CalendarItemResponse {
  id: string;
  calendarId: string;
  kind: CalendarItemKind;
  title: string;
  // Raw override -- null if the item inherits the owning calendar's icon.
  icon: string | null;
  color: string;
  createdBy: string;
  lastModifiedBy: string;
  assignedTo: string | null;
  // Set when this item was scheduled from a TaskLibrary template (see ScheduleTaskFromTemplate);
  // null for a freeform item. Optional (rather than a plain `string | null`) for two reasons: (1)
  // as of this writing the backend's CalendarItemResponse (CreateItem.Endpoint.cs) does not
  // actually serialize CalendarItem.TaskTemplateId onto this DTO yet, even though the domain type
  // carries it -- this field is added here for the shape the next step (agenda.ts integration)
  // will need, but will read as undefined against the real API until that backend gap is closed;
  // (2) making it optional keeps every existing CalendarItemResponse object literal (e.g. in
  // calendars.service.spec.ts and any consumer outside this step's scope) compiling unchanged.
  taskTemplateId?: string | null;
}

export interface CalendarItemOccurrence {
  itemId: string;
  kind: CalendarItemKind;
  title: string;
  // Always resolved: the item's own override, or the owning calendar's icon when it has none.
  icon: string;
  // Raw override -- null if this occurrence's icon came from the calendar's default.
  iconOverride: string | null;
  color: string;
  startsAt: string | null;
  endsAt: string | null;
  dueAt: string | null;
  isAllDay: boolean;
  isCompleted: boolean;
  createdBy: string;
  lastModifiedBy: string;
  assignedTo: string | null;
  // The parent item's own Title, set only when this occurrence is one subtask of a
  // template-scheduled task (title above is the subtask's own title in that case). Null for every
  // other occurrence -- lets the frontend group a routine's subtask occurrences under their
  // shared parent. Optional (matching the backend's own "additive trailing field" comment on
  // CalendarItemOccurrence) so every existing CalendarItemOccurrence/CalendarOccurrence object
  // literal outside this step's scope (agenda/tasks-today/events-today/child-calendar/home specs)
  // keeps compiling unchanged.
  parentTitle?: string | null;
  // The subtask's own id, set only for a template-scheduled task's per-subtask occurrence --
  // required by setTaskCompletion to target the right subtask. Null otherwise. Optional for the
  // same back-compat reason as parentTitle.
  subtaskId?: string | null;
  // The parent item's own effective icon, set alongside parentTitle -- icon above is the
  // *subtask's* own icon (which can legitimately differ between sibling subtasks), so it's the
  // wrong value for a grouped run's header. Null for every other occurrence. Optional for the
  // same back-compat reason as parentTitle.
  parentIcon?: string | null;
}

export interface TaskCompletion {
  itemId: string;
  occurrenceDate: string;
  isCompleted: boolean;
}

export type CalendarOccurrence = CalendarItemOccurrence & { calendarId: string; calendarName: string };

@Injectable({ providedIn: 'root' })
export class CalendarsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private todayCache: { date: string; promise: Promise<CalendarOccurrence[]> } | null = null;

  listMyCalendars(): Promise<CalendarSummary[]> {
    return firstValueFrom(this.http.get<CalendarSummary[]>(`${this.runtimeConfig.apiBaseUrl}/calendars`));
  }

  createCalendar(request: CreateCalendarRequest): Promise<CalendarSummary> {
    return firstValueFrom(postIdempotent<CalendarSummary>(this.http, `${this.runtimeConfig.apiBaseUrl}/calendars`, request));
  }

  // Owner-only -- the calendar's icon is the one detail that can change after creation today.
  updateCalendarIcon(calendarId: string, icon: string): Promise<void> {
    return firstValueFrom(
      this.http.patch<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/icon`, { icon } satisfies UpdateCalendarIconRequest)
    );
  }

  // Moves an already-existing calendar to a different group -- the one exception to ownership
  // otherwise being fixed at creation. Requires the caller to own the calendar and manage the
  // destination group (two-sided consent, gated server-side).
  transferToGroup(calendarId: string, groupId: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/group/${groupId}`, {}));
  }

  deleteCalendar(calendarId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}`));
  }

  listIcalTokens(calendarId: string): Promise<IcalTokenSummary[]> {
    return firstValueFrom(this.http.get<IcalTokenSummary[]>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/ical-tokens`));
  }

  createIcalToken(calendarId: string): Promise<IssuedIcalToken> {
    return firstValueFrom(
      postIdempotent<IssuedIcalToken>(this.http, `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/ical-tokens`, {})
    );
  }

  revokeIcalToken(calendarId: string, tokenId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/ical-tokens/${tokenId}`));
  }

  // subscriptionPath is relative, in the same style as every other endpoint path on this
  // service -- prefix with apiBaseUrl to get the URL a calendar app can subscribe to.
  icalFeedUrl(subscriptionPath: string): string {
    return `${this.runtimeConfig.apiBaseUrl}${subscriptionPath}`;
  }

  listOccurrences(calendarId: string, from: string, to: string): Promise<CalendarItemOccurrence[]> {
    return firstValueFrom(
      this.http.get<CalendarItemOccurrence[]>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/occurrences`, {
        params: { from, to }
      })
    );
  }

  listAssignableMembers(calendarId: string): Promise<AssignableMember[]> {
    return firstValueFrom(this.http.get<AssignableMember[]>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/assignable-members`));
  }

  async createItem(calendarId: string, request: CreateItemRequest): Promise<CalendarItemResponse> {
    const created = await firstValueFrom(
      postIdempotent<CalendarItemResponse>(this.http, `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items`, request)
    );
    this.todayCache = null;
    return created;
  }

  async updateItemDetails(calendarId: string, itemId: string, request: UpdateItemDetailsRequest): Promise<CalendarItemResponse> {
    const updated = await firstValueFrom(
      this.http.patch<CalendarItemResponse>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}/details`, request)
    );
    this.todayCache = null;
    return updated;
  }

  async rescheduleItem(calendarId: string, itemId: string, request: RescheduleItemRequest): Promise<CalendarItemResponse> {
    const updated = await firstValueFrom(
      this.http.patch<CalendarItemResponse>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}/schedule`, request)
    );
    this.todayCache = null;
    return updated;
  }

  async deleteItem(calendarId: string, itemId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}`));
    this.todayCache = null;
  }

  // subtaskId is required to complete one subtask of a template-scheduled task, and must be
  // omitted (null) for a plain task -- matches SetTaskCompletionRequest.SubtaskId. Defaults to
  // null so every existing (pre-TaskLibrary) call site keeps working unchanged.
  async setTaskCompletion(calendarId: string, itemId: string, date: string, isCompleted: boolean, subtaskId: string | null = null): Promise<TaskCompletion> {
    const completion = await firstValueFrom(
      this.http.patch<TaskCompletion>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}/completion`, {
        date,
        isCompleted,
        subtaskId
      })
    );
    this.todayCache = null;
    return completion;
  }

  // The calendar-item analog of createItem for a Task whose subtasks come from a TaskLibrary
  // template instead of being entered by hand -- see ScheduleTaskFromTemplate.Command.cs.
  async scheduleTaskFromTemplate(calendarId: string, request: ScheduleTaskFromTemplateRequest): Promise<CalendarItemResponse> {
    const created = await firstValueFrom(
      postIdempotent<CalendarItemResponse>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/from-template`,
        request
      )
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

    const perCalendar = await Promise.all(
      calendars.map(async (calendar) => {
        const occurrences = await this.listOccurrences(calendar.id, from, to);
        return occurrences.map((occurrence) => ({ ...occurrence, calendarId: calendar.id, calendarName: calendar.name }));
      })
    );

    return perCalendar.flat();
  }
}
