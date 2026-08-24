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

export interface InviteToGroupRequest {
  email: string;
  role: GroupRole;
}

export interface GroupInvite {
  id: string;
  email: string;
  role: GroupRole;
  invitedAt: string;
  expiresAt: string;
}

export interface GroupInvitePreview {
  groupName: string;
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

  listInvites(groupId: string): Promise<GroupInvite[]> {
    return firstValueFrom(this.http.get<GroupInvite[]>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites`));
  }

  inviteToGroup(groupId: string, request: InviteToGroupRequest): Promise<GroupInvite> {
    return firstValueFrom(this.http.post<GroupInvite>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites`, request));
  }

  revokeInvite(groupId: string, inviteId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites/${inviteId}`));
  }

  previewInvite(token: string): Promise<GroupInvitePreview> {
    return firstValueFrom(this.http.get<GroupInvitePreview>(`${this.runtimeConfig.apiBaseUrl}/invites/${token}/preview`));
  }

  acceptInvite(token: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.runtimeConfig.apiBaseUrl}/invites/${token}/accept`, {}));
  }
}
