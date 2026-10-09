import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type AiProvider = Schemas['AiProvider'];

export type AiSessionStatus = Schemas['AiSessionStatus'];

export type AiChatMessageRole = Schemas['AiChatMessageRole'];

export type AiProviderSettingsEntry = Schemas['AiProviderSettingsEntry'];

export type AiProviderSettings = Schemas['AiProviderSettings'];

// kind 0 = the provider answered; 1 = it rejected the key (bad key, no quota, ...) with a message.
export type TestProviderConnectionResult = Schemas['TestProviderConnectionResult'];

export type AiSessionTranscriptEntry = Schemas['AiSessionTranscriptEntry'];

export type AiSessionDraftEntry = Schemas['AiSessionDraftEntry'];

// How far back a meal must have been served to be suggested; Any means no served-in filter.
export type AiServedWindow = Schemas['AiServedWindow'];

// Each window's day count, for display.
export const SERVED_WINDOW_DAYS: Record<AiServedWindow, number> = {
  Any: 0,
  Last30Days: 30,
  Last60Days: 60,
  Last90Days: 90,
};

export type AiSessionView = Schemas['AiSessionView'];

export type StartAiSessionRequest = Schemas['StartAiSessionRequest'];

@Injectable({ providedIn: 'root' })
export class AiAssistantService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private base(childId: string): string {
    return `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}`;
  }

  listProviders(childId: string): Promise<AiProviderSettings> {
    return firstValueFrom(this.http.get<AiProviderSettings>(`${this.base(childId)}/ai/providers`));
  }

  setProviderApiKey(
    childId: string,
    provider: AiProvider,
    apiKey: string,
  ): Promise<AiProviderSettings> {
    return firstValueFrom(
      this.http.put<AiProviderSettings>(`${this.base(childId)}/ai/providers/${provider}/key`, {
        apiKey,
      }),
    );
  }

  removeProviderApiKey(childId: string, provider: AiProvider): Promise<AiProviderSettings> {
    return firstValueFrom(
      this.http.delete<AiProviderSettings>(`${this.base(childId)}/ai/providers/${provider}/key`),
    );
  }

  setActiveProvider(childId: string, provider: AiProvider): Promise<AiProviderSettings> {
    return firstValueFrom(
      this.http.put<AiProviderSettings>(`${this.base(childId)}/ai/active-provider/${provider}`, {}),
    );
  }

  acknowledgeDataSharing(childId: string): Promise<AiProviderSettings> {
    return firstValueFrom(
      this.http.put<AiProviderSettings>(
        `${this.base(childId)}/ai/data-sharing-acknowledgement`,
        {},
      ),
    );
  }

  // A probe, not a state change -- still POST (per this feature's backend design) and so still
  // wrapped in postIdempotent per http-idempotency.ts's "only POST" rule, even though retrying it
  // twice has no side effect to duplicate.
  testProviderConnection(
    childId: string,
    provider: AiProvider,
    apiKey?: string | null,
  ): Promise<TestProviderConnectionResult> {
    return firstValueFrom(
      postIdempotent<TestProviderConnectionResult>(
        this.http,
        `${this.base(childId)}/ai/providers/${provider}/test-connection`,
        {
          apiKey: apiKey ?? null,
        },
      ),
    );
  }

  // 404 when the family has no current session -- callers should treat that as "nothing to
  // restore" rather than an error.
  getCurrentSession(childId: string): Promise<AiSessionView> {
    return firstValueFrom(
      this.http.get<AiSessionView>(`${this.base(childId)}/ai/sessions/current`),
    );
  }

  startSession(childId: string, request: StartAiSessionRequest): Promise<AiSessionView> {
    return firstValueFrom(
      postIdempotent<AiSessionView>(this.http, `${this.base(childId)}/ai/sessions`, request),
    );
  }

  sendMessage(childId: string, text: string): Promise<AiSessionView> {
    return firstValueFrom(
      postIdempotent<AiSessionView>(
        this.http,
        `${this.base(childId)}/ai/sessions/current/messages`,
        { text },
      ),
    );
  }

  applyDraft(childId: string): Promise<AiSessionView> {
    return firstValueFrom(
      postIdempotent<AiSessionView>(
        this.http,
        `${this.base(childId)}/ai/sessions/current/apply`,
        {},
      ),
    );
  }

  discardSession(childId: string): Promise<AiSessionView> {
    return firstValueFrom(
      postIdempotent<AiSessionView>(
        this.http,
        `${this.base(childId)}/ai/sessions/current/discard`,
        {},
      ),
    );
  }
}
