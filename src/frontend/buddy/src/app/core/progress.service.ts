import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

export interface GoalPost {
  threshold: number;
  icon: string;
  label: string | null;
}

export interface ProgressSummary {
  totalStars: number;
  unlockedMilestones: number[];
  // Resolved server-side (see GoalPostResolver) from the child's guardian-configured goal posts
  // -- or the default scale, if none are configured -- including extrapolated posts past the
  // configured list, so the frontend never re-derives this logic.
  currentIcon: string | null;
  nextGoalThreshold: number;
  nextGoalIcon: string;
  goalPosts: GoalPost[];
}

@Injectable({ providedIn: 'root' })
export class ProgressService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  getMyProgress(): Promise<ProgressSummary> {
    return firstValueFrom(this.http.get<ProgressSummary>(`${this.runtimeConfig.apiBaseUrl}/progress/me`));
  }

  getChildProgress(childId: string): Promise<ProgressSummary> {
    return firstValueFrom(this.http.get<ProgressSummary>(`${this.runtimeConfig.apiBaseUrl}/progress/children/${childId}`));
  }

  // Guardian-only (see ProgressAuthorization.CheckManage) -- full-replace, mirrors the backend's
  // GoalPostsConfigured event semantics.
  configureGoalPosts(childId: string, goalPosts: GoalPost[]): Promise<ProgressSummary> {
    return firstValueFrom(
      this.http.put<ProgressSummary>(`${this.runtimeConfig.apiBaseUrl}/progress/children/${childId}/goals`, { goalPosts })
    );
  }
}
