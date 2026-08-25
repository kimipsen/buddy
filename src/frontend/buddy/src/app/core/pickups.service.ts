import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// PickupSlot values match the backend's PickupSlot enum ordinals (no string enum converter is
// registered server-side): 0 = DropOff, 1 = PickUp.
export type PickupSlot = 0 | 1;

// PickupAssigneeKind values match the backend's enum ordinals: 0 = Guardian, 1 = SelfEscort,
// 2 = Sibling, 3 = Playdate.
export type PickupAssigneeKind = 0 | 1 | 2 | 3;

// Only GuardianId/SiblingChildId/Playdate* meaningful for their matching kind are ever set --
// mirrors the backend's flat PickupAssignment shape (see
// docs/backend/analysis/pickup-schedules.md#question-3).
export interface PickupOccurrence {
  date: string;
  slot: PickupSlot;
  kind: PickupAssigneeKind;
  guardianId: string | null;
  siblingChildId: string | null;
  playdateHostName: string | null;
  playdateLocation: string | null;
  playdateContactInfo: string | null;
  time: string | null;
  notes: string | null;
  assignedBy: string;
}

export interface AssignPickupRequest {
  kind: PickupAssigneeKind;
  guardianId?: string | null;
  siblingChildId?: string | null;
  playdateHostName?: string | null;
  playdateLocation?: string | null;
  playdateContactInfo?: string | null;
  time?: string | null;
  notes?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PickupsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listSchedule(childId: string, from: string, to: string): Promise<PickupOccurrence[]> {
    return firstValueFrom(
      this.http.get<PickupOccurrence[]>(`${this.runtimeConfig.apiBaseUrl}/pickups/children/${childId}/schedule`, {
        params: { from, to }
      })
    );
  }

  assignPickup(childId: string, date: string, slot: PickupSlot, request: AssignPickupRequest): Promise<PickupOccurrence> {
    return firstValueFrom(
      this.http.put<PickupOccurrence>(`${this.runtimeConfig.apiBaseUrl}/pickups/children/${childId}/assignments`, request, {
        params: { date, slot: String(slot) }
      })
    );
  }

  clearPickup(childId: string, date: string, slot: PickupSlot): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.runtimeConfig.apiBaseUrl}/pickups/children/${childId}/assignments`, {
        params: { date, slot: String(slot) }
      })
    );
  }
}
