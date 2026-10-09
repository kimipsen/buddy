import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { CalendarRole } from './calendars.service';
import { sortByFullName, sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import { MealplanAccessTier } from './mealplans.service';
import { RuntimeConfigService } from './runtime-config.service';
import type { Schemas } from './api/schemas';

export type GroupRole = Schemas['GroupRole'];

export type GroupSummary = Schemas['GroupSummaryResponse'];

export type GroupMember = Schemas['GroupMemberResponse'];

// The permission policies are keyed by role name; the generated schema only says "string keys".
export type CalendarPermissionPolicy = Record<GroupRole, CalendarRole>;

export type MealplanPermissionPolicy = Record<GroupRole, MealplanAccessTier>;

export type GroupDetail = Omit<
  Schemas['GroupResponse'],
  'calendarPermissionPolicy' | 'mealplanPermissionPolicy'
> & {
  calendarPermissionPolicy: CalendarPermissionPolicy;
  mealplanPermissionPolicy: MealplanPermissionPolicy;
};

export type CreateGroupRequest = Schemas['CreateGroupRequest'];

export type InviteToGroupRequest = Schemas['InviteToGroupRequest'];

export type GroupInvite = Schemas['GroupInviteResponse'];

// The response to sending an invite: the invite plus the link the email carries, so the inviter
// can share it themself. Only available here -- the server keeps just the token's hash.
export type SentGroupInvite = Schemas['SentGroupInviteResponse'];

export type GroupInvitePreview = Schemas['GroupInvitePreviewResponse'];

@Injectable({ providedIn: 'root' })
export class GroupsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listMyGroups(): Promise<GroupSummary[]> {
    return firstValueFrom(
      this.http.get<GroupSummary[]>(`${this.runtimeConfig.apiBaseUrl}/groups`),
    ).then(sortByName);
  }

  createGroup(request: CreateGroupRequest): Promise<GroupSummary> {
    return firstValueFrom(
      postIdempotent<GroupSummary>(this.http, `${this.runtimeConfig.apiBaseUrl}/groups`, request),
    );
  }

  listInvites(groupId: string): Promise<GroupInvite[]> {
    return firstValueFrom(
      this.http.get<GroupInvite[]>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites`),
    );
  }

  inviteToGroup(groupId: string, request: InviteToGroupRequest): Promise<SentGroupInvite> {
    return firstValueFrom(
      postIdempotent<SentGroupInvite>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites`,
        request,
      ),
    );
  }

  revokeInvite(groupId: string, inviteId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/invites/${inviteId}`,
      ),
    );
  }

  // Adds a child the caller guards directly as a Member -- no invite/accept step, since a
  // guardian already has authority over their own child (mirrors CreateChild's direct-provision
  // pattern rather than InviteToGroup's email-based flow).
  addChildToGroup(groupId: string, childId: string): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/children/${childId}`,
        {},
      ),
    );
  }

  deleteGroup(groupId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}`),
    );
  }

  getGroup(groupId: string): Promise<GroupDetail> {
    return firstValueFrom(
      this.http.get<GroupDetail>(`${this.runtimeConfig.apiBaseUrl}/groups/${groupId}`),
    ).then((group) => ({ ...group, members: sortByFullName(group.members) }));
  }

  updateCalendarPermissionPolicy(groupId: string, policy: CalendarPermissionPolicy): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/calendar-permission-policy`,
        { policy },
      ),
    );
  }

  updateMealplanPermissionPolicy(groupId: string, policy: MealplanPermissionPolicy): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(
        `${this.runtimeConfig.apiBaseUrl}/groups/${groupId}/mealplan-permission-policy`,
        { policy },
      ),
    );
  }

  previewInvite(token: string): Promise<GroupInvitePreview> {
    return firstValueFrom(
      this.http.get<GroupInvitePreview>(
        `${this.runtimeConfig.apiBaseUrl}/invites/${token}/preview`,
      ),
    );
  }

  acceptInvite(token: string): Promise<void> {
    return firstValueFrom(
      postIdempotent<void>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/invites/${token}/accept`,
        {},
      ),
    );
  }
}
