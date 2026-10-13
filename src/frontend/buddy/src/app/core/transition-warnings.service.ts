import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';
import type { MyTransitionWarnings } from './transition-warnings';

// A guardian's view of a child's settings, with each exception's item title and icon.
export type ChildTransitionWarnings = Schemas['ChildTransitionWarningsResponse'];
export type ItemWarningOverrideDetail = Schemas['ItemWarningOverrideDetailResponse'];
export type ConfigureTransitionWarningsRequest = Schemas['ConfigureTransitionWarningsRequest'];

// The 409 code SetItemWarningOverride answers at 100 exceptions.
export const TOO_MANY_WARNING_OVERRIDES = 'too_many_warning_overrides';

// The /transition-warnings endpoints (docs/backend/analysis/transition-warnings.md). Settings are
// read and written whole; every write is a PUT or DELETE, so none needs an idempotency key.
@Injectable({ providedIn: 'root' })
export class TransitionWarningsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private url(path: string): string {
    return `${this.runtimeConfig.apiBaseUrl}/transition-warnings${path}`;
  }

  // The signed-in child's own settings: off with 15/5/1 pre-filled until a guardian saves.
  getMine(): Promise<MyTransitionWarnings> {
    return firstValueFrom(this.http.get<MyTransitionWarnings>(this.url('/me')));
  }

  getForChild(childId: string): Promise<ChildTransitionWarnings> {
    return firstValueFrom(this.http.get<ChildTransitionWarnings>(this.url(`/children/${childId}`)));
  }

  configure(
    childId: string,
    settings: ConfigureTransitionWarningsRequest,
  ): Promise<ChildTransitionWarnings> {
    return firstValueFrom(
      this.http.put<ChildTransitionWarnings>(this.url(`/children/${childId}`), settings),
    );
  }

  // An empty leadMinutes list means "never warn for this item".
  setOverride(
    childId: string,
    itemId: string,
    leadMinutes: number[],
  ): Promise<ChildTransitionWarnings> {
    return firstValueFrom(
      this.http.put<ChildTransitionWarnings>(this.url(`/children/${childId}/items/${itemId}`), {
        leadMinutes,
      }),
    );
  }

  async clearOverride(childId: string, itemId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(this.url(`/children/${childId}/items/${itemId}`)));
  }
}
