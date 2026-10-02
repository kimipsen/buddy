import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// Backend DayOfWeek ordinals (System.DayOfWeek): 0 = Sunday ... 6 = Saturday.
export type Weekday = 0 | 1 | 2 | 3 | 4 | 5 | 6;

// WorkDaySource ordinals: 0 = Pattern, 1 = Override.
export type WorkDaySource = 0 | 1;

export interface WorkLocation {
  id: string;
  name: string;
  icon: string;
  color: string;
  isArchived: boolean;
}

export interface WorkPatternDay {
  week: number;
  day: Weekday;
  locationId: string;
}

// A cycle of cycleWeeks weeks counted from anchorMonday (cycle week 0) -- see
// docs/backend/analysis/work-locations.md, Question 4.
export interface WorkPattern {
  cycleWeeks: number;
  anchorMonday: string;
  days: WorkPatternDay[];
}

export interface WorkLocationSchedule {
  guardianId: string;
  locations: WorkLocation[];
  pattern: WorkPattern;
}

// kind 0 = unplanned (no override, no pattern entry), 1 = off (an override to "not at any
// location"), 2 = at a location, from the pattern or an override.
export type WorkDayStatus =
  { kind: 0 } | { kind: 1 } | { kind: 2; location: WorkLocation; source: WorkDaySource };

export interface WorkDay {
  date: string;
  status: WorkDayStatus;
}

export function workDayLocation(day: WorkDay): WorkLocation | null {
  return day.status.kind === 2 ? day.status.location : null;
}

// Whether the day is a per-date exception: an override to off or to a location.
export function isWorkDayOverride(day: WorkDay): boolean {
  return day.status.kind === 1 || (day.status.kind === 2 && day.status.source === 1);
}

export interface WorkLocationDetails {
  name: string;
  icon: string;
  color: string;
}

@Injectable({ providedIn: 'root' })
export class WorkLocationsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  getSchedule(guardianId: string): Promise<WorkLocationSchedule> {
    return firstValueFrom(
      this.http.get<WorkLocationSchedule>(
        `${this.runtimeConfig.apiBaseUrl}/work-locations/guardians/${guardianId}`,
      ),
    );
  }

  listWorkDays(guardianId: string, from: string, to: string): Promise<WorkDay[]> {
    return firstValueFrom(
      this.http.get<WorkDay[]>(
        `${this.runtimeConfig.apiBaseUrl}/work-locations/guardians/${guardianId}/days`,
        { params: { from, to } },
      ),
    );
  }

  addLocation(details: WorkLocationDetails): Promise<WorkLocation> {
    return firstValueFrom(
      postIdempotent<WorkLocation>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/work-locations/me/locations`,
        details,
      ),
    );
  }

  updateLocation(locationId: string, details: WorkLocationDetails): Promise<WorkLocation> {
    return firstValueFrom(
      this.http.patch<WorkLocation>(
        `${this.runtimeConfig.apiBaseUrl}/work-locations/me/locations/${locationId}`,
        details,
      ),
    );
  }

  archiveLocation(locationId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/work-locations/me/locations/${locationId}`,
      ),
    );
  }

  replacePattern(pattern: WorkPattern): Promise<WorkPattern> {
    return firstValueFrom(
      this.http.put<WorkPattern>(
        `${this.runtimeConfig.apiBaseUrl}/work-locations/me/pattern`,
        pattern,
      ),
    );
  }

  // locationId null marks every date in [from, to] as "not at any location" (a day off).
  setOverrides(from: string, to: string, locationId: string | null): Promise<WorkDay[]> {
    return firstValueFrom(
      this.http.put<WorkDay[]>(`${this.runtimeConfig.apiBaseUrl}/work-locations/me/overrides`, {
        from,
        to,
        locationId,
      }),
    );
  }

  clearOverrides(from: string, to: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/work-locations/me/overrides`, {
        params: { from, to },
      }),
    );
  }
}
