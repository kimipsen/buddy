import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

export interface UserEventItem {
  type: string;
  data: Record<string, unknown>;
}

export interface UserEventsPage {
  items: UserEventItem[];
  previousCursor: string | null;
  nextCursor: string | null;
}

@Injectable({ providedIn: 'root' })
export class UserEventsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  async listCurrentUserEvents(cursor: string | null, pageSize: number): Promise<UserEventsPage> {
    const params: Record<string, string> = { pageSize: pageSize.toString() };

    if (cursor) {
      params['cursor'] = cursor;
    }

    return firstValueFrom(
      this.http.get<UserEventsPage>(`${this.runtimeConfig.apiBaseUrl}/users/me/events`, { params })
    );
  }
}

