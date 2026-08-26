import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

export interface ProgressSummary {
  totalStars: number;
  unlockedMilestones: number[];
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
}
