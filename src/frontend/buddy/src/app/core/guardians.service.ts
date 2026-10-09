import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { sortByPersonName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type PersonName = Schemas['Name'];

export type GuardianKind = Schemas['GuardianKind'];

export type ChildSummary = Schemas['ChildSummary'];

export type GuardianSummary = Schemas['GuardianSummary'];

export type SiblingSummary = Schemas['SiblingSummary'];

export type CreateChildResult = Schemas['ChildResponse'];

export type ChildPasswordReset = Schemas['ChildPasswordResetResponse'];

export type CreateChildRequest = Schemas['CreateChildRequest'];

export type GuardianInvite = Schemas['GuardianInviteResponse'];

export type InviteGuardianRequest = Schemas['InviteGuardianRequest'];

export type GuardianInvitePreview = Schemas['GuardianInvitePreviewResponse'];

@Injectable({ providedIn: 'root' })
export class GuardiansService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listMyChildren(): Promise<ChildSummary[]> {
    return firstValueFrom(
      this.http.get<ChildSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/children`),
    ).then(sortByPersonName);
  }

  // A non-empty result means the current user is a child linked to at least one guardian.
  listMyGuardians(): Promise<GuardianSummary[]> {
    return firstValueFrom(
      this.http.get<GuardianSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/guardians`),
    ).then(sortByPersonName);
  }

  // Unlike listMyGuardians (which only answers "who are the caller's own guardians", i.e. caller
  // is the child), this answers "who are this child's guardians", as one of them -- e.g. a
  // co-parent -- needed for the Pickups "assign a guardian" picker.
  listChildGuardians(childId: string): Promise<GuardianSummary[]> {
    return firstValueFrom(
      this.http.get<GuardianSummary[]>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardians`,
      ),
    ).then(sortByPersonName);
  }

  // The calling child's own siblings (other children sharing at least one of their guardians) --
  // used to show a sibling's name for a pickup/drop-off assignment instead of just "a sibling".
  listMySiblings(): Promise<SiblingSummary[]> {
    return firstValueFrom(
      this.http.get<SiblingSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/siblings`),
    ).then(sortByPersonName);
  }

  createChild(request: CreateChildRequest): Promise<CreateChildResult> {
    return firstValueFrom(
      postIdempotent<CreateChildResult>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/users/me/children`,
        request,
      ),
    );
  }

  // Erases the child's account and all its data. Only the child's sole guardian may; with other
  // guardians the API answers 409 (child_has_other_guardians).
  deleteChild(childId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}`),
    );
  }

  // Gives the child a new one-time password (shown once) and signs them out everywhere. A child
  // has no email, so Keycloak's own "forgot password" flow can't reach them.
  resetChildPassword(childId: string): Promise<ChildPasswordReset> {
    return firstValueFrom(
      postIdempotent<ChildPasswordReset>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/password-reset`,
        {},
      ),
    );
  }

  revokeChild(childId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-link`,
      ),
    );
  }

  updateChildLanguage(childId: string, language: string): Promise<ChildSummary> {
    return firstValueFrom(
      this.http.patch<ChildSummary>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/language`,
        { language },
      ),
    );
  }

  updateChildTimeZone(childId: string, timeZoneId: string): Promise<ChildSummary> {
    return firstValueFrom(
      this.http.patch<ChildSummary>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/timezone`,
        { timeZoneId },
      ),
    );
  }

  listGuardianInvites(childId: string): Promise<GuardianInvite[]> {
    return firstValueFrom(
      this.http.get<GuardianInvite[]>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-invites`,
      ),
    );
  }

  inviteGuardian(childId: string, request: InviteGuardianRequest): Promise<GuardianInvite> {
    return firstValueFrom(
      postIdempotent<GuardianInvite>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-invites`,
        request,
      ),
    );
  }

  revokeGuardianInvite(childId: string, inviteId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-invites/${inviteId}`,
      ),
    );
  }

  previewGuardianInvite(token: string): Promise<GuardianInvitePreview> {
    return firstValueFrom(
      this.http.get<GuardianInvitePreview>(
        `${this.runtimeConfig.apiBaseUrl}/guardian-invites/${token}/preview`,
      ),
    );
  }

  acceptGuardianInvite(token: string): Promise<void> {
    return firstValueFrom(
      postIdempotent<void>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/guardian-invites/${token}/accept`,
        {},
      ),
    );
  }
}
