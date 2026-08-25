import { Component, OnInit, inject, signal } from '@angular/core';

import { AuthService } from '../../../core/auth.service';
import { todayIsoDate } from '../../../core/date-utils';
import { GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { PickupAssigneeKind, PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { UsersService } from '../../../core/users.service';

const GUARDIAN: PickupAssigneeKind = 0;
const SELF_ESCORT: PickupAssigneeKind = 1;
const SIBLING: PickupAssigneeKind = 2;
const PLAYDATE: PickupAssigneeKind = 3;

const SLOT_LABELS = { 0: 'child.home.pickup.slots.dropOff', 1: 'child.home.pickup.slots.pickUp' } as const;

@Component({
  selector: 'app-child-home',
  imports: [TranslatePipe],
  templateUrl: './home.html'
})
export class ChildHome implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);
  private readonly users = inject(UsersService);

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;
  protected readonly slotLabels = SLOT_LABELS;

  protected readonly guardianList = signal<GuardianSummary[]>([]);
  protected readonly todaysPickups = signal<PickupOccurrence[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    void this.loadGuardians();
    void this.loadTodaysPickups();
  }

  protected logout(): void {
    this.auth.logout();
  }

  protected assigneeName(occurrence: PickupOccurrence): string | null {
    if (occurrence.kind === this.guardianKind) {
      return this.guardianList().find((guardian) => guardian.id === occurrence.guardianId)?.name.givenName ?? null;
    }

    return null;
  }

  private async loadGuardians(): Promise<void> {
    this.loading.set(true);

    try {
      this.guardianList.set(await this.guardians.listMyGuardians());
    } finally {
      this.loading.set(false);
    }
  }

  private async loadTodaysPickups(): Promise<void> {
    try {
      const me = await this.users.ensureCurrentUser();
      const today = todayIsoDate();
      this.todaysPickups.set(await this.pickups.listSchedule(me.id, today, today));
    } catch {
      this.todaysPickups.set([]);
    }
  }
}
