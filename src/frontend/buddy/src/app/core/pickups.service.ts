import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RuntimeConfigService } from './runtime-config.service';

// PickupSlot values match the backend's PickupSlot enum ordinals (no string enum converter is
// registered server-side): 0 = DropOff, 1 = PickUp.
export type PickupSlot = 0 | 1;

// PickupAssigneeKind values match the backend's enum ordinals: 0 = Guardian, 1 = SelfEscort,
// 2 = Sibling, 3 = Playdate, 4 = Babysitter.
export type PickupAssigneeKind = 0 | 1 | 2 | 3 | 4;

// Who handles a slot, discriminated by `kind` -- each case carries only its own fields, matching
// the backend's PickupAssigneeDto. A playdate's location/contactInfo are '' when not given. A
// babysitter is a saved entry on guardianId's list; its name is resolved by the server on every
// response ('' when it no longer resolves) and ignored when sent.
export type PickupAssignee =
  | { kind: 0; guardianId: string }
  | { kind: 1 }
  | { kind: 2; siblingChildId: string }
  | { kind: 3; hostName: string; location: string; contactInfo: string }
  | { kind: 4; guardianId: string; babysitterId: string; name: string };

// The playdate host's name, or '' for any other kind -- lets templates show it without narrowing.
export function playdateHostName(assignee: PickupAssignee): string {
  return assignee.kind === 3 ? assignee.hostName : '';
}

// The babysitter's resolved name, or '' for any other kind (or an unresolvable babysitter).
export function babysitterName(assignee: PickupAssignee): string {
  return assignee.kind === 4 ? assignee.name : '';
}

export interface PickupOccurrence {
  date: string;
  slot: PickupSlot;
  assignee: PickupAssignee;
  time: string | null;
  notes: string;
  assignedBy: string;
}

export interface AssignPickupRequest {
  assignee: PickupAssignee;
  time: string | null;
  notes: string;
}

@Injectable({ providedIn: 'root' })
export class PickupsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  listSchedule(childId: string, from: string, to: string): Promise<PickupOccurrence[]> {
    return firstValueFrom(
      this.http.get<PickupOccurrence[]>(
        `${this.runtimeConfig.apiBaseUrl}/pickups/children/${childId}/schedule`,
        {
          params: { from, to },
        },
      ),
    );
  }

  assignPickup(
    childId: string,
    date: string,
    slot: PickupSlot,
    request: AssignPickupRequest,
  ): Promise<PickupOccurrence> {
    return firstValueFrom(
      this.http.put<PickupOccurrence>(
        `${this.runtimeConfig.apiBaseUrl}/pickups/children/${childId}/assignments`,
        request,
        {
          params: { date, slot: String(slot) },
        },
      ),
    );
  }

  clearPickup(childId: string, date: string, slot: PickupSlot): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(
        `${this.runtimeConfig.apiBaseUrl}/pickups/children/${childId}/assignments`,
        {
          params: { date, slot: String(slot) },
        },
      ),
    );
  }
}
