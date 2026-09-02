import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { CalendarRole } from './calendars.service';
import { postIdempotent } from './http-idempotency';
import { MealplanAccessTier } from './mealplans.service';
import { RuntimeConfigService } from './runtime-config.service';

// GroupRole values match the backend's GroupRole enum ordinals (no string enum converter is
// registered server-side): 0 = Owner, 1 = Admin, 2 = Member.
export type GroupRole = 0 | 1 | 2;

export interface GroupSummary {
  id: string;
  name: string;
  role: GroupRole;
}

export interface GroupMember {
  userId: string;
  givenName: string;
  familyName: string;
  role: GroupRole;
  isChild: boolean;
}

// Unlike GroupRole itself, dictionary KEYS of type GroupRole serialize as the enum's member name
// (System.Text.Json's built-in behavior for enum dictionary keys), not its numeric ordinal --
// so this policy's keys are string role names, while CalendarRole values stay numeric.
export type GroupRoleName = 'Owner' | 'Admin' | 'Member';

export const GROUP_ROLE_NAMES: readonly GroupRoleName[] = ['Owner', 'Admin', 'Member'];

export type CalendarPermissionPolicy = Record<GroupRoleName, CalendarRole>;

export type MealplanPermissionPolicy = Record<GroupRoleName, MealplanAccessTier>;

export interface GroupDetail {
  id: string;
  name: string;
  members: GroupMember[];
  calendarPermissionPolicy: CalendarPermissionPolicy;
  mealplanPermissionPolicy: MealplanPermissionPolicy;
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
    return firstValueFrom(postIdempotent<GroupSummary>(this.http, `${this.runtimeConfig.apiBaseUrl}/groups`, request));
  }

  listInvites(groupId: string): Promise<GroupInvite[]> {
    return firstValueFrom(this.http.get<GroupInvite[]>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites`));
  }

  inviteToGroup(groupId: string, request: InviteToGroupRequest): Promise<GroupInvite> {
    return firstValueFrom(
      postIdempotent<GroupInvite>(this.http, `${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites`, request)
    );
  }

  revokeInvite(groupId: string, inviteId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites/${inviteId}`));
  }

  // Adds a child the caller guards directly as a Member -- no invite/accept step, since a
  // guardian already has authority over their own child (mirrors CreateChild's direct-provision
  // pattern rather than InviteToGroup's email-based flow).
  addChildToGroup(groupId: string, childId: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/children/${childId}`, {}));
  }

  getGroup(groupId: string): Promise<GroupDetail> {
    return firstValueFrom(this.http.get<GroupDetail>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}`));
  }

  updateCalendarPermissionPolicy(groupId: string, policy: CalendarPermissionPolicy): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/calendar-permission-policy`, { policy })
    );
  }

  updateMealplanPermissionPolicy(groupId: string, policy: MealplanPermissionPolicy): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/mealplan-permission-policy`, { policy })
    );
  }

  previewInvite(token: string): Promise<GroupInvitePreview> {
    return firstValueFrom(this.http.get<GroupInvitePreview>(`${this.runtimeConfig.apiBaseUrl}/invites/${token}/preview`));
  }

  acceptInvite(token: string): Promise<void> {
    return firstValueFrom(postIdempotent<void>(this.http, `${this.runtimeConfig.apiBaseUrl}/invites/${token}/accept`, {}));
  }
}
