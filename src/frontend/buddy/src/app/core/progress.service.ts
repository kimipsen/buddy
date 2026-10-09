import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type GoalPost = Schemas['GoalPostResponse'];

export type ProgressSummary = Schemas['ProgressSummary'];

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
}
