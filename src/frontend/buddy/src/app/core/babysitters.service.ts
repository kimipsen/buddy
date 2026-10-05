import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// One of the caller's own saved babysitters (BabysitterSummary). Archived ones stay in the list so
// pickup slots that still point at them keep their name -- see docs/backend/analysis/babysitters.md.
export interface Babysitter {
  id: string;
  name: string;
  contactInfo: string;
  isArchived: boolean;
}

// One entry of a child's babysitter picker (ChildBabysitter): an active babysitter on the list of
// guardianId, one of the child's guardians. The (guardianId, id) pair is what a kind 4 pickup
// assignee carries.
export interface ChildBabysitter {
  guardianId: string;
  id: string;
  name: string;
  contactInfo: string;
}

export interface BabysitterDetails {
  name: string;
  contactInfo: string;
}

@Injectable({ providedIn: 'root' })
export class BabysittersService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listMine(): Promise<Babysitter[]> {
    return firstValueFrom(
      this.http.get<Babysitter[]>(`${this.runtimeConfig.apiBaseUrl}/babysitters/me`),
    );
  }

  listForChild(childId: string): Promise<ChildBabysitter[]> {
    return firstValueFrom(
      this.http.get<ChildBabysitter[]>(
        `${this.runtimeConfig.apiBaseUrl}/babysitters/children/${childId}`,
      ),
    );
  }

  add(details: BabysitterDetails): Promise<Babysitter> {
    return firstValueFrom(
      postIdempotent<Babysitter>(
        this.http,
        `${this.runtimeConfig.apiBaseUrl}/babysitters/me`,
        details,
      ),
    );
  }

  update(babysitterId: string, details: BabysitterDetails): Promise<Babysitter> {
    return firstValueFrom(
      this.http.patch<Babysitter>(
        `${this.runtimeConfig.apiBaseUrl}/babysitters/me/${babysitterId}`,
        details,
      ),
    );
  }

  archive(babysitterId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/babysitters/me/${babysitterId}`),
    );
  }
}
