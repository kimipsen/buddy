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
}

export interface GuardianSummary {
  id: string;
  name: PersonName;
  guardianLinkId: string;
  kind: GuardianKind;
}

export interface CreateChildResult extends ChildSummary {
  temporaryPassword: string;
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

  createChild(name: string): Promise<CreateChildResult> {
    return firstValueFrom(
      this.http.post<CreateChildResult>(`${this.runtimeConfig.apiBaseUrl}/users/me/children`, { name })
    );
  }

  revokeChild(childId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/users/me/children/${childId}/guardian-link`)
    );
  }
}
