import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// Who is on duty at night for a child (docs/backend/analysis/night-duty.md).

// responsible.kind: 0 guardian, 1 babysitter (name resolved, output only), 2 sleepover,
// 3 unassigned (a note on its own).
export type NightResponsible = Schemas['NightResponsibleDto'];
export type NightResponsibleKind = NightResponsible['kind'];

export type NightEntry = Schemas['NightEntryDto'];

// kind 0 = unplanned, 1 = cleared by an override, 2 = planned (from the schedule or an override).
export type NightStatus = Schemas['NightStatus'];
export type NightSource = Schemas['NightSource'];
export type Night = Schemas['Night'];

export type NightPatternDay = Schemas['NightPatternDayDto'];
export type NightPattern = Schemas['NightPatternResponse'];
export type NightPatternStatus = Schemas['NightPatternStatus'];
export type NightSchedule = Schemas['NightScheduleResponse'];

export type ReplaceNightPatternRequest = Schemas['ReplaceNightPatternRequest'];

// entry null = "nothing planned this night" (a cleared night). To follow the fixed schedule
// again, clear the override instead.
export type NightOverride = Schemas['NightOverrideRequest'];

// The caller's own iCal subscription links to one child's nights
// (docs/backend/analysis/night-duty-ical-feed.md). The plaintext token is only in the create response.
export type NightIcalTokenSummary = Schemas['NightIcalTokenSummary'];
export type IssuedNightIcalToken = Schemas['NightIcalTokenResponse'];

export const NIGHT_RESPONSIBLE = {
  guardian: 0,
  babysitter: 1,
  sleepover: 2,
  unassigned: 3,
} as const satisfies Record<string, NightResponsibleKind>;

export const NIGHT_STATUS = {
  unplanned: 0,
  cleared: 1,
  planned: 2,
} as const satisfies Record<string, NightStatus['kind']>;

// The entry a night resolves to, or null when nothing is planned.
export function nightEntry(night: Night): NightEntry | null {
  return night.status.kind === NIGHT_STATUS.planned ? night.status.entry : null;
}

// What the fixed schedule says for an overridden night, or null.
export function scheduledEntry(night: Night): NightEntry | null {
  return night.status.kind === NIGHT_STATUS.unplanned ? null : night.status.scheduled;
}

// Whether the night is a per-date change: cleared, or planned by an override.
export function isNightOverride(night: Night): boolean {
  return (
    night.status.kind === NIGHT_STATUS.cleared ||
    (night.status.kind === NIGHT_STATUS.planned && night.status.source === 'Override')
  );
}

@Injectable({ providedIn: 'root' })
export class NightDutyService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private url(childId: string, path = ''): string {
    return `${this.runtimeConfig.apiBaseUrl}/night-duty/children/${childId}${path}`;
  }

  getSchedule(childId: string): Promise<NightSchedule> {
    return firstValueFrom(this.http.get<NightSchedule>(this.url(childId)));
  }

  listNights(childId: string, from: string, to: string): Promise<Night[]> {
    return firstValueFrom(
      this.http.get<Night[]>(this.url(childId, '/nights'), { params: { from, to } }),
    );
  }

  replacePattern(childId: string, request: ReplaceNightPatternRequest): Promise<NightSchedule> {
    return firstValueFrom(this.http.put<NightSchedule>(this.url(childId, '/pattern'), request));
  }

  // One request for several nights -- a switch is two -- saved in one append.
  setOverrides(childId: string, nights: NightOverride[]): Promise<Night[]> {
    return firstValueFrom(this.http.put<Night[]>(this.url(childId, '/overrides'), { nights }));
  }

  clearOverrides(childId: string, from: string, to: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(this.url(childId, '/overrides'), { params: { from, to } }),
    );
  }

  listIcalTokens(childId: string): Promise<NightIcalTokenSummary[]> {
    return firstValueFrom(
      this.http.get<NightIcalTokenSummary[]>(this.url(childId, '/ical-tokens')),
    );
  }

  createIcalToken(childId: string): Promise<IssuedNightIcalToken> {
    return firstValueFrom(
      postIdempotent<IssuedNightIcalToken>(this.http, this.url(childId, '/ical-tokens'), {}),
    );
  }

  revokeIcalToken(childId: string, tokenId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(this.url(childId, `/ical-tokens/${tokenId}`)));
  }

  icalFeedUrl(subscriptionPath: string): string {
    return `${this.runtimeConfig.apiBaseUrl}${subscriptionPath}`;
  }
}
