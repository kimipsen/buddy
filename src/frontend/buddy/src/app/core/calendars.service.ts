import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// CalendarRole/CalendarItemKind values match the backend's enum ordinals (no string enum
// converter is registered server-side): CalendarRole 0 = Owner, 1 = Contributor, 2 = Viewer.
export type CalendarRole = 0 | 1 | 2;

// CalendarItemKind 0 = Event, 1 = Task.
export type CalendarItemKind = 0 | 1;

export interface CalendarSummary {
  id: string;
  name: string;
  role: CalendarRole;
}

export interface CreateCalendarRequest {
  name: string;
  timeZoneId: string;
  groupId?: string;
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
  createdBy: string;
  lastModifiedBy: string;
}

export type CalendarOccurrence = CalendarItemOccurrence & { calendarId: string };

function todayIsoDate(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

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

  listOccurrences(calendarId: string, from: string, to: string): Promise<CalendarItemOccurrence[]> {
    return firstValueFrom(
      this.http.get<CalendarItemOccurrence[]>(`${this.runtimeConfig.apiBaseUrl}/calendars/${calendarId}/occurrences`, {
        params: { from, to }
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
      const promise = this.fetchTodayOccurrences(today).catch((error: unknown) => {
        if (this.todayCache?.promise === promise) {
          this.todayCache = null;
        }
        throw error;
      });
      this.todayCache = { date: today, promise };
    }

    return this.todayCache.promise;
  }

  private async fetchTodayOccurrences(today: string): Promise<CalendarOccurrence[]> {
    const calendars = await this.listMyCalendars();

    const perCalendar = await Promise.all(
      calendars.map(async (calendar) => {
        const occurrences = await this.listOccurrences(calendar.id, today, today);
        return occurrences.map((occurrence) => ({ ...occurrence, calendarId: calendar.id }));
      })
    );

    return perCalendar.flat();
  }
}
