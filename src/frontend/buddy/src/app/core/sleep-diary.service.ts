import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// A night wake-up or a daytime nap. Times are "HH:mm" here; the API answers "HH:mm:ss", which
// normalizeEntry trims so they bind straight to app-time-select.
export interface SleepInterval {
  startTime: string;
  durationMinutes: number;
}

// What a guardian saves for one date -- every field optional, matching the backend's
// LogSleepEntryRequest. Saving always overwrites the whole day.
export interface SleepEntryRequest {
  routineStartTime: string | null;
  ritualStartTime: string | null;
  ritualEndTime: string | null;
  bedTime: string | null;
  fellAsleepTime: string | null;
  nightWakeUps: SleepInterval[];
  morningWakeTime: string | null;
  isTired: boolean;
  naps: SleepInterval[];
  // Guardian-entered (the app only suggests it), so it can disagree with the times above.
  totalSleepMinutes: number | null;
  remarks: string;
}

export interface SleepEntry extends SleepEntryRequest {
  date: string;
  // Derived server-side from the date (the paper form's "weekend" column).
  isWeekend: boolean;
  loggedBy: string;
}

export interface SleepDiaryEntries {
  sleepHygieneNotes: string;
  entries: SleepEntry[];
}

// The plaintext token is only ever returned here, once.
export interface CreatedShareLink {
  id: string;
  token: string;
  createdAt: string;
  expiresAt: string | null;
}

export interface ShareLinkSummary {
  id: string;
  createdAt: string;
  expiresAt: string | null;
}

export interface SharedSleepDiary {
  childGivenName: string;
  childFamilyName: string;
  from: string;
  to: string;
  expiresAt: string | null;
  sleepHygieneNotes: string;
  entries: SleepEntry[];
}

function trimTime(time: string | null): string | null {
  return time === null ? null : time.slice(0, 5);
}

function normalizeIntervals(intervals: SleepInterval[]): SleepInterval[] {
  return intervals.map((interval) => ({ ...interval, startTime: interval.startTime.slice(0, 5) }));
}

function normalizeEntry(entry: SleepEntry): SleepEntry {
  return {
    ...entry,
    routineStartTime: trimTime(entry.routineStartTime),
    ritualStartTime: trimTime(entry.ritualStartTime),
    ritualEndTime: trimTime(entry.ritualEndTime),
    bedTime: trimTime(entry.bedTime),
    fellAsleepTime: trimTime(entry.fellAsleepTime),
    morningWakeTime: trimTime(entry.morningWakeTime),
    nightWakeUps: normalizeIntervals(entry.nightWakeUps),
    naps: normalizeIntervals(entry.naps),
  };
}

@Injectable({ providedIn: 'root' })
export class SleepDiaryService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private base(childId: string): string {
    return `${this.runtimeConfig.apiBaseUrl}/sleep-diary/children/${childId}`;
  }

  async listEntries(childId: string, from: string, to: string): Promise<SleepDiaryEntries> {
    const diary = await firstValueFrom(
      this.http.get<SleepDiaryEntries>(`${this.base(childId)}/entries`, { params: { from, to } }),
    );

    return { ...diary, entries: diary.entries.map(normalizeEntry) };
  }

  async logEntry(childId: string, date: string, request: SleepEntryRequest): Promise<SleepEntry> {
    const entry = await firstValueFrom(
      this.http.put<SleepEntry>(`${this.base(childId)}/entries/${date}`, request),
    );

    return normalizeEntry(entry);
  }

  clearEntry(childId: string, date: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base(childId)}/entries/${date}`));
  }

  updateHygieneNotes(childId: string, notes: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.base(childId)}/hygiene-notes`, { notes }));
  }

  createShareLink(childId: string, expiresAt: string | null): Promise<CreatedShareLink> {
    return firstValueFrom(
      postIdempotent<CreatedShareLink>(this.http, `${this.base(childId)}/share-links`, {
        expiresAt,
      }),
    );
  }

  listShareLinks(childId: string): Promise<ShareLinkSummary[]> {
    return firstValueFrom(this.http.get<ShareLinkSummary[]>(`${this.base(childId)}/share-links`));
  }

  revokeShareLink(childId: string, shareLinkId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base(childId)}/share-links/${shareLinkId}`),
    );
  }

  // Anonymous: the token is the credential. from/to default server-side to the last 14 days.
  async getShared(token: string, from?: string, to?: string): Promise<SharedSleepDiary> {
    const params: Record<string, string> = {};

    if (from) {
      params['from'] = from;
    }

    if (to) {
      params['to'] = to;
    }

    const shared = await firstValueFrom(
      this.http.get<SharedSleepDiary>(
        `${this.runtimeConfig.apiBaseUrl}/sleep-diary/shared/${encodeURIComponent(token)}`,
        { params },
      ),
    );

    return { ...shared, entries: shared.entries.map(normalizeEntry) };
  }
}
