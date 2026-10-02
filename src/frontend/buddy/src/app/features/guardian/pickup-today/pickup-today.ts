import { Component, OnInit, inject, signal } from '@angular/core';
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

type PickupRow = PickupOccurrence & { childId: string; childName: string };

@Component({
  selector: 'app-pickup-today',
  imports: [RouterLink, TranslatePipe, LoadingSpinner],
  templateUrl: './pickup-today.html',
})
export class PickupToday implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;
  protected readonly slotLabels = SLOT_LABELS;

  protected readonly rows = signal<PickupRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasChildren = signal(true);
  protected readonly multipleChildren = signal(false);

  private readonly childGuardiansById = new Map<string, GuardianSummary[]>();

  ngOnInit(): void {
    void this.loadToday();
  }

  protected readonly playdateHostName = playdateHostName;

  protected assigneeName(row: PickupRow): string | null {
    const { assignee } = row;

    if (assignee.kind === GUARDIAN) {
      return (
        this.childGuardiansById.get(row.childId)?.find((g) => g.id === assignee.guardianId)?.name
          .givenName ?? null
      );
    }

    return null;
  }

  private async loadToday(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.multipleChildren.set(children.length > 1);

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
          this.childGuardiansById.set(child.id, childGuardians);
          return occurrences.map((occurrence) => ({
            ...occurrence,
            childId: child.id,
            childName: child.name.givenName,
          }));
        },
      );

      this.rows.set(perChild.flat().sort((a, b) => a.slot - b.slot));
    } catch {
      this.error.set('dashboard.pickup.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
