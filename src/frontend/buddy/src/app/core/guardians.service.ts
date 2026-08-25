import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

export interface PersonName {
  givenName: string;
  familyName: string;
}

// GuardianKind values match the backend's GuardianKind enum ordinals (no string enum converter
// is registered server-side): 0 = Parent, 1 = Guardian.
export type GuardianKind = 0 | 1;

export interface ChildSummary {
  id: string;
  name: PersonName;
  guardianLinkId: string;
  kind: GuardianKind;
  language: string;
}

export interface GuardianSummary {
  id: string;
  name: PersonName;
  guardianLinkId: string;
  kind: GuardianKind;
}

export interface SiblingSummary {
  id: string;
  name: PersonName;
}

export interface CreateChildResult extends ChildSummary {
  username: string;
  temporaryPassword: string;
}

export interface CreateChildRequest {
  givenName: string;
  familyName: string;
  username: string;
}

export interface GuardianInvite {
  id: string;
  email: string;
  kind: GuardianKind;
  invitedAt: string;
  expiresAt: string;
}

export interface InviteGuardianRequest {
  email: string;
  kind: GuardianKind;
}

export interface GuardianInvitePreview {
  childGivenName: string;
  kind: GuardianKind;
}

@Injectable({ providedIn: 'root' })
export class GuardiansService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listMyChildren(): Promise<ChildSummary[]> {
    return firstValueFrom(this.http.get<ChildSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/children`));
  }

  // A non-empty result means the current user is a child linked to at least one guardian.
  listMyGuardians(): Promise<GuardianSummary[]> {
    return firstValueFrom(this.http.get<GuardianSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/guardians`));
  }

  // Unlike listMyGuardians (which only answers "who are the caller's own guardians", i.e. caller
  // is the child), this answers "who are this child's guardians", as one of them -- e.g. a
  // co-parent -- needed for the Pickups "assign a guardian" picker.
  listChildGuardians(childId: string): Promise<GuardianSummary[]> {
    return firstValueFrom(
      this.http.get<GuardianSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardians`)
    );
  }

  // The calling child's own siblings (other children sharing at least one of their guardians) --
  // used to show a sibling's name for a pickup/drop-off assignment instead of just "a sibling".
  listMySiblings(): Promise<SiblingSummary[]> {
    return firstValueFrom(this.http.get<SiblingSummary[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/siblings`));
  }

  createChild(request: CreateChildRequest): Promise<CreateChildResult> {
    return firstValueFrom(
      this.http.post<CreateChildResult>(`${this.runtimeConfig.apiBaseUrl}/users/me/children`, request)
    );
  }

  revokeChild(childId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-link`)
    );
  }

  updateChildLanguage(childId: string, language: string): Promise<ChildSummary> {
    return firstValueFrom(
      this.http.patch<ChildSummary>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/language`, { language })
    );
  }

  listGuardianInvites(childId: string): Promise<GuardianInvite[]> {
    return firstValueFrom(
      this.http.get<GuardianInvite[]>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-invites`)
    );
  }

  inviteGuardian(childId: string, request: InviteGuardianRequest): Promise<GuardianInvite> {
    return firstValueFrom(
      this.http.post<GuardianInvite>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-invites`, request)
    );
  }

  revokeGuardianInvite(childId: string, inviteId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-invites/${inviteId}`)
    );
  }

  previewGuardianInvite(token: string): Promise<GuardianInvitePreview> {
    return firstValueFrom(this.http.get<GuardianInvitePreview>(`${this.runtimeConfig.apiBaseUrl}/guardian-invites/${token}/preview`));
  }

  acceptGuardianInvite(token: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.runtimeConfig.apiBaseUrl}/guardian-invites/${token}/accept`, {}));
  }
}
