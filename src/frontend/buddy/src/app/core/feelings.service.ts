import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

// docs/backend/analysis/feelings-check-in.md. Times are "HH:mm" here; the API answers "HH:mm:ss",
// which the service trims so they bind straight to app-time-select and compare as strings.
export type FeelingZone = Schemas['FeelingZone'];
export type FeelingTag = Schemas['FeelingTag'];
export type FeelingsDisplayMode = Schemas['FeelingsDisplayMode'];
export type FeelingCheckIn = Schemas['FeelingCheckInResponse'];
export type MyFeelingsToday = Schemas['MyFeelingsTodayResponse'];
export type FeelingCheckIns = Schemas['FeelingCheckInsResponse'];
export type CreatedFeelingsShareLink = Schemas['FeelingsShareLinkResponse'];
export type FeelingsShareLinkSummary = Schemas['FeelingsShareLinkSummary'];
export type SharedFeelings = Schemas['SharedFeelingsResponse'];

// What both the child and a guardian send for one check-in. A guardian may add date/time.
export interface CheckInContent {
  zone: FeelingZone;
  tags: FeelingTag[];
  note: string;
}

export interface GuardianCheckIn extends CheckInContent {
  date: string;
  time: string;
}

function trimTime(time: string): string {
  return time.slice(0, 5);
}

function normalizeCheckIn(checkIn: FeelingCheckIn): FeelingCheckIn {
  return { ...checkIn, time: trimTime(checkIn.time) };
}

@Injectable({ providedIn: 'root' })
export class FeelingsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private get base(): string {
    return `${this.runtimeConfig.apiBaseUrl}/feelings`;
  }

  private child(childId: string): string {
    return `${this.base}/children/${childId}`;
  }

  // --- The child's own routes ---

  async getMyToday(): Promise<MyFeelingsToday> {
    const today = await firstValueFrom(this.http.get<MyFeelingsToday>(`${this.base}/me/today`));

    return {
      ...today,
      promptTimes: today.promptTimes.map(trimTime),
      checkIns: today.checkIns.map(normalizeCheckIn),
    };
  }

  async checkIn(content: CheckInContent): Promise<FeelingCheckIn> {
    const checkIn = await firstValueFrom(
      postIdempotent<FeelingCheckIn>(this.http, `${this.base}/me/check-ins`, content),
    );

    return normalizeCheckIn(checkIn);
  }

  undoCheckIn(checkInId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/me/check-ins/${checkInId}`));
  }

  // --- A guardian's routes ---

  async listCheckIns(childId: string, from: string, to: string): Promise<FeelingCheckIns> {
    const list = await firstValueFrom(
      this.http.get<FeelingCheckIns>(`${this.child(childId)}/check-ins`, { params: { from, to } }),
    );

    return {
      ...list,
      promptTimes: list.promptTimes.map(trimTime),
      checkIns: list.checkIns.map(normalizeCheckIn),
    };
  }

  // date/time null means "now" on the child's clock.
  async logCheckIn(
    childId: string,
    content: CheckInContent,
    date: string | null,
    time: string | null,
  ): Promise<FeelingCheckIn> {
    const checkIn = await firstValueFrom(
      postIdempotent<FeelingCheckIn>(this.http, `${this.child(childId)}/check-ins`, {
        ...content,
        date,
        time,
      }),
    );

    return normalizeCheckIn(checkIn);
  }

  async editCheckIn(
    childId: string,
    checkInId: string,
    checkIn: GuardianCheckIn,
  ): Promise<FeelingCheckIn> {
    const edited = await firstValueFrom(
      this.http.put<FeelingCheckIn>(`${this.child(childId)}/check-ins/${checkInId}`, checkIn),
    );

    return normalizeCheckIn(edited);
  }

  removeCheckIn(childId: string, checkInId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.child(childId)}/check-ins/${checkInId}`));
  }

  async setPromptTimes(childId: string, times: string[]): Promise<string[]> {
    const saved = await firstValueFrom(
      this.http.put<{ times: string[] }>(`${this.child(childId)}/prompt-times`, { times }),
    );

    return saved.times.map(trimTime);
  }

  async setDisplayMode(
    childId: string,
    displayMode: FeelingsDisplayMode,
  ): Promise<FeelingsDisplayMode> {
    const saved = await firstValueFrom(
      this.http.put<{ displayMode: FeelingsDisplayMode }>(`${this.child(childId)}/display-mode`, {
        displayMode,
      }),
    );

    return saved.displayMode;
  }

  createShareLink(childId: string, expiresAt: string | null): Promise<CreatedFeelingsShareLink> {
    return firstValueFrom(
      postIdempotent<CreatedFeelingsShareLink>(this.http, `${this.child(childId)}/share-links`, {
        expiresAt,
      }),
    );
  }

  listShareLinks(childId: string): Promise<FeelingsShareLinkSummary[]> {
    return firstValueFrom(
      this.http.get<FeelingsShareLinkSummary[]>(`${this.child(childId)}/share-links`),
    );
  }

  revokeShareLink(childId: string, shareLinkId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.child(childId)}/share-links/${shareLinkId}`),
    );
  }

  // Anonymous: the token is the credential. from/to default server-side to the last 14 days.
  async getShared(token: string, from?: string, to?: string): Promise<SharedFeelings> {
    const params: Record<string, string> = {};

    if (from) {
      params['from'] = from;
    }

    if (to) {
      params['to'] = to;
    }

    const shared = await firstValueFrom(
      this.http.get<SharedFeelings>(`${this.base}/shared/${encodeURIComponent(token)}`, {
        params,
      }),
    );

    return { ...shared, checkIns: shared.checkIns.map(normalizeCheckIn) };
  }
}
