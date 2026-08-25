import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { todayIsoDate } from './date-utils';
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
  role: CalendarRole;
}

export interface CreateCalendarRequest {
  name: string;
  timeZoneId: string;
  // Required -- a calendar is always group-owned now, there's no personal-calendar option.
  groupId: string;
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
  icon: string;
  color: string;
  // Event requires startsAt+endsAt; task requires dueDate -- the other pair stays null.
  startsAt: DatePart | null;
  endsAt: DatePart | null;
  dueDate: DatePart | null;
  recurrence: RecurrenceRuleRequest | null;
}

export interface CalendarItemResponse {
  id: string;
  calendarId: string;
  kind: CalendarItemKind;
  title: string;
  icon: string;
  color: string;
  createdBy: string;
  lastModifiedBy: string;
}

export interface CalendarItemOccurrence {
  itemId: string;
  kind: CalendarItemKind;
  title: string;
  icon: string;
  color: string;
  startsAt: string | null;
  endsAt: string | null;
  dueAt: string | null;
  isCompleted: boolean;
  createdBy: string;
  lastModifiedBy: string;
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
    return firstValueFrom(this.http.post<CalendarSummary>(`${this.runtimeConfig.apiBaseUrl}/calendars`, request));
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

  listOccurrences(calendarId: string, from: string, to: string): Promise<CalendarItemOccurrence[]> {
    return firstValueFrom(
      this.http.get<CalendarItemOccurrence[]>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/occurrences`, {
        params: { from, to }
      })
    );
  }

  createItem(calendarId: string, request: CreateItemRequest): Promise<CalendarItemResponse> {
    return firstValueFrom(
      this.http.post<CalendarItemResponse>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items`, request)
    );
  }

  setTaskCompletion(calendarId: string, itemId: string, date: string, isCompleted: boolean): Promise<TaskCompletion> {
    return firstValueFrom(
      this.http.patch<TaskCompletion>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/items/${itemId}/completion`, {
        date,
        isCompleted
      })
    );
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
