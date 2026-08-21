import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

export interface UserEventItem {
  type: string;
  data: Record<string, unknown>;
}

interface UserEventsPageResponse {
  items: UserEventItem[];
  previousCursor: string | null;
  nextCursor: string | null;
}

@Injectable({ providedIn: 'root' })
export class UserEventsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  async listCurrentUserEvents(): Promise<UserEventItem[]> {
    const response = await firstValueFrom(
      this.http.get<UserEventsPageResponse>(`${this.runtimeConfig.apiBaseUrl}/users/me/events`)
    );

    return response.items;
  }
}
