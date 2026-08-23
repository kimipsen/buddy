import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// GroupRole values match the backend's GroupRole enum ordinals (no string enum converter is
// registered server-side): 0 = Owner, 1 = Admin, 2 = Member.
export type GroupRole = 0 | 1 | 2;

export interface GroupSummary {
  id: string;
  name: string;
  role: GroupRole;
}

export interface CreateGroupRequest {
  name: string;
}

@Injectable({ providedIn: 'root' })
export class GroupsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listMyGroups(): Promise<GroupSummary[]> {
    return firstValueFrom(this.http.get<GroupSummary[]>(`${this.runtimeConfig.apiBaseUrl}/groups`));
  }

  createGroup(request: CreateGroupRequest): Promise<GroupSummary> {
    return firstValueFrom(this.http.post<GroupSummary>(`${this.runtimeConfig.apiBaseUrl}/groups`, request));
  }
}
