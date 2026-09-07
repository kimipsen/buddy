import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import { MealSlot } from './mealplans.service';
import { RuntimeConfigService } from './runtime-config.service';

// AiProvider values match the backend's AiProvider enum ordinals (no string enum converter is
// registered server-side): 0 = Anthropic, 1 = OpenAi, 2 = Gemini.
export type AiProvider = 0 | 1 | 2;

// AiSessionStatus values match the backend's AiSessionStatus enum ordinals: 0 = Drafting,
// 1 = Applied, 2 = Discarded.
export type AiSessionStatus = 0 | 1 | 2;

// AiChatMessageRole values match the backend's AiChatMessageRole enum ordinals: 0 = User,
// 1 = Assistant.
export type AiChatMessageRole = 0 | 1;

export interface AiProviderSettingsEntry {
  provider: AiProvider;
  last4: string;
  addedAt: string;
}

export interface AiProviderSettings {
  providers: AiProviderSettingsEntry[];
  activeProvider: AiProvider | null;
}

export interface TestProviderConnectionResult {
  isSuccessful: boolean;
  errorMessage: string | null;
}

export interface AiSessionTranscriptEntry {
  role: AiChatMessageRole;
  text: string;
  occurredAt: string;
}

export interface AiSessionDraftEntry {
  date: string;
  slot: MealSlot;
  mealId: string;
  mealName: string;
}

export interface AiSessionView {
  id: string;
  from: string;
  to: string;
  requestedSlots: MealSlot[];
  status: AiSessionStatus;
  transcript: AiSessionTranscriptEntry[];
  draft: AiSessionDraftEntry[];
}

export interface StartAiSessionRequest {
  from: string;
  to: string;
  slots: MealSlot[];
  mustIncludeMealIds: string[];
  notes: string | null;
}

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

  setProviderApiKey(childId: string, provider: AiProvider, apiKey: string): Promise<AiProviderSettings> {
    return firstValueFrom(this.http.put<AiProviderSettings>(`${this.base(childId)}/ai/providers/${provider}/key`, { apiKey }));
  }

  removeProviderApiKey(childId: string, provider: AiProvider): Promise<AiProviderSettings> {
    return firstValueFrom(this.http.delete<AiProviderSettings>(`${this.base(childId)}/ai/providers/${provider}/key`));
  }

  setActiveProvider(childId: string, provider: AiProvider): Promise<AiProviderSettings> {
    return firstValueFrom(this.http.put<AiProviderSettings>(`${this.base(childId)}/ai/active-provider/${provider}`, {}));
  }

  // A probe, not a state change -- still POST (per this feature's backend design) and so still
  // wrapped in postIdempotent per http-idempotency.ts's "only POST" rule, even though retrying it
  // twice has no side effect to duplicate.
  testProviderConnection(childId: string, provider: AiProvider, apiKey?: string | null): Promise<TestProviderConnectionResult> {
    return firstValueFrom(
      postIdempotent<TestProviderConnectionResult>(this.http, `${this.base(childId)}/ai/providers/${provider}/test-connection`, {
        apiKey: apiKey ?? null
      })
    );
  }

  // 404 when the family has no current session -- callers should treat that as "nothing to
  // restore" rather than an error.
  getCurrentSession(childId: string): Promise<AiSessionView> {
    return firstValueFrom(this.http.get<AiSessionView>(`${this.base(childId)}/ai/sessions/current`));
  }

  startSession(childId: string, request: StartAiSessionRequest): Promise<AiSessionView> {
    return firstValueFrom(postIdempotent<AiSessionView>(this.http, `${this.base(childId)}/ai/sessions`, request));
  }

  sendMessage(childId: string, text: string): Promise<AiSessionView> {
    return firstValueFrom(postIdempotent<AiSessionView>(this.http, `${this.base(childId)}/ai/sessions/current/messages`, { text }));
  }

  applyDraft(childId: string): Promise<AiSessionView> {
    return firstValueFrom(postIdempotent<AiSessionView>(this.http, `${this.base(childId)}/ai/sessions/current/apply`, {}));
  }

  discardSession(childId: string): Promise<AiSessionView> {
    return firstValueFrom(postIdempotent<AiSessionView>(this.http, `${this.base(childId)}/ai/sessions/current/discard`, {}));
  }
}
