import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

export type GoalPost = Schemas['GoalPostResponse'];

export type ProgressSummary = Schemas['ProgressSummary'];

export type Reward = Schemas['RewardResponse'];

// A catalog row to save: id omitted for a new reward (see ConfigureRewards).
export type RewardDraft = Schemas['RewardBody'];

export type RewardRequest = Schemas['RewardRequestResponse'];

export type RewardRequestStatus = Schemas['RewardRequestStatus'];

// The 409 codes the reward request routes answer with (RewardRequestOutcome on the backend).
export const INSUFFICIENT_STARS = 'insufficient_stars';
export const TOO_MANY_PENDING_REQUESTS = 'too_many_pending_requests';
export const REWARD_REQUEST_RESOLVED = 'reward_request_resolved';

@Injectable({ providedIn: 'root' })
export class ProgressService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  getMyProgress(): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.get<ProgressSummary>(`${this.runtimeConfig.apiBaseUrl}/progress/me`),
    );
  }

  getChildProgress(childId: string): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.get<ProgressSummary>(
        `${this.runtimeConfig.apiBaseUrl}/progress/children/${childId}`,
      ),
    );
  }

  // Guardian-only (see ProgressAuthorization.CheckManage) -- full-replace, mirrors the backend's
  // GoalPostsConfigured event semantics.
  configureGoalPosts(childId: string, goalPosts: GoalPost[]): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.put<ProgressSummary>(
        `${this.runtimeConfig.apiBaseUrl}/progress/children/${childId}/goals`,
        { goalPosts },
      ),
    );
  }

  // Guardian-only, full replace of the child's reward catalog (RewardsConfigured).
  configureRewards(childId: string, rewards: RewardDraft[]): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.put<ProgressSummary>(
        `${this.runtimeConfig.apiBaseUrl}/progress/children/${childId}/rewards`,
        { rewards },
      ),
    );
  }

  // The signed-in child asks for one of their rewards; it waits for a guardian.
  requestReward(rewardId: string): Promise<ProgressSummary> {
    return firstValueFrom(
      postIdempotent<ProgressSummary>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/progress/me/reward-requests`,
        { rewardId },
      ),
    );
  }

  // The signed-in child withdraws their own pending request. Repeating it is a no-op server-side.
  cancelRewardRequest(requestId: string): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.post<ProgressSummary>(
        `${this.runtimeConfig.apiBaseUrl}/progress/me/reward-requests/${requestId}/cancel`,
        null,
      ),
    );
  }

  approveRewardRequest(childId: string, requestId: string): Promise<ProgressSummary> {
    return this.resolveRewardRequest(childId, requestId, 'approve');
  }

  declineRewardRequest(childId: string, requestId: string): Promise<ProgressSummary> {
    return this.resolveRewardRequest(childId, requestId, 'decline');
  }

  // Guardian-only. Repeating the same resolution is a no-op server-side, so no Idempotency-Key.
  private resolveRewardRequest(
    childId: string,
    requestId: string,
    action: 'approve' | 'decline',
  ): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.post<ProgressSummary>(
        `${this.runtimeConfig.apiBaseUrl}/progress/children/${childId}/reward-requests/${requestId}/${action}`,
        null,
      ),
    );
  }
}

// The API error code of a failed request, if it has one.
export function errorCode(error: unknown): string | undefined {
  const body = (error as { error?: { code?: unknown } } | null)?.error;
  return typeof body?.code === 'string' ? body.code : undefined;
}
