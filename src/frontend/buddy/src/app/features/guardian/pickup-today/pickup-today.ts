import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';

import { todayIsoDate } from '../../../core/date-utils';
import { GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import {
  PickupAssigneeKind,
  PickupOccurrence,
  PickupsService,
  playdateHostName,
} from '../../../core/pickups.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

const GUARDIAN = 0 satisfies PickupAssigneeKind;
const SELF_ESCORT = 1 satisfies PickupAssigneeKind;
const SIBLING = 2 satisfies PickupAssigneeKind;
const PLAYDATE = 3 satisfies PickupAssigneeKind;

const SLOT_LABELS = {
  0: 'dashboard.pickup.slots.dropOff',
  1: 'dashboard.pickup.slots.pickUp',
} as const;

// assigneeName is the assigned guardian's given name, resolved against that child's own guardian
// list; null when the assignee isn't a guardian or the id can't be resolved.
type PickupRow = PickupOccurrence & {
  childId: string;
  childName: string;
  assigneeName: string | null;
};

// What the widget loaded: whether the guardian has children at all, and today's pickups across them.
interface LoadedPickups {
  hasChildren: boolean;
  multipleChildren: boolean;
  rows: PickupRow[];
}

@Component({
  selector: 'app-pickup-today',
  imports: [RouterLink, TranslatePipe, LoadingSpinner],
  templateUrl: './pickup-today.html',
})
export class PickupToday {
  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;
  protected readonly slotLabels = SLOT_LABELS;

  protected readonly today = resource({ loader: () => this.loadToday() });

  protected readonly playdateHostName = playdateHostName;

  private async loadToday(): Promise<LoadedPickups> {
    const children = await this.guardians.listMyChildren();

    if (children.length === 0) {
      return { hasChildren: false, multipleChildren: false, rows: [] };
    }

    const today = todayIsoDate();
    // Two requests per child (schedule + guardians) with at most PER_ITEM_REQUEST_CONCURRENCY
    // children in flight, so never more than 2x the cap requests at once. Still all-or-nothing:
    // any child's failure shows the widget's load error.
    const perChild = await mapWithConcurrency(
      children,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (child) => {
        const [occurrences, childGuardians] = await Promise.all([
          this.pickups.listSchedule(child.id, today, today),
          this.guardians.listChildGuardians(child.id),
        ]);
        return occurrences.map((occurrence) => ({
          ...occurrence,
          childId: child.id,
          childName: child.name.givenName,
          assigneeName: this.assigneeName(occurrence, childGuardians),
        }));
      },
    );

    return {
      hasChildren: true,
      multipleChildren: children.length > 1,
      rows: perChild.flat().sort((a, b) => a.slot - b.slot),
    };
  }

  private assigneeName(
    occurrence: PickupOccurrence,
    childGuardians: GuardianSummary[],
  ): string | null {
    const { assignee } = occurrence;

    if (assignee.kind === GUARDIAN) {
      return childGuardians.find((g) => g.id === assignee.guardianId)?.name.givenName ?? null;
    }

    return null;
  }
}
