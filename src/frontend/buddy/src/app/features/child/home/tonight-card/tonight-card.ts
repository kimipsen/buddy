import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { ChildClockService } from '../../../../core/child-clock.service';
import { FeaturesService } from '../../../../core/features.service';
import {
  GuardianSummary,
  GuardiansService,
  childFacingName,
} from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  NIGHT_RESPONSIBLE,
  NightDutyService,
  NightEntry,
  nightEntry,
} from '../../../../core/night-duty.service';
import { UsersService } from '../../../../core/users.service';

interface TonightView {
  icon: string;
  // The big name; null for a note on its own.
  name: string | null;
  // i18n key of the line under the name.
  lineKey: string | null;
  host: string;
  // A name that couldn't be resolved falls back to a generic word.
  nameKey: string | null;
  note: string;
}

// "Tonight" on the child's day dashboard (docs/backend/analysis/night-duty.md, Question 7): who is
// on duty tonight, large, and the night's note ("Screen night"). Renders nothing for an empty
// night, and nothing when the load fails -- the rest of the dashboard has its own error.
@Component({
  selector: 'app-tonight-card',
  imports: [TranslatePipe],
  templateUrl: './tonight-card.html',
  host: { class: 'contents' },
})
export class TonightCard implements OnInit {
  private readonly nightDuty = inject(NightDutyService);
  private readonly guardians = inject(GuardiansService);
  private readonly users = inject(UsersService);
  private readonly clock = inject(ChildClockService);
  private readonly features = inject(FeaturesService);

  private readonly entry = signal<NightEntry | null>(null);
  private readonly guardianList = signal<GuardianSummary[]>([]);

  protected readonly view = computed((): TonightView | null => {
    const entry = this.entry();
    return entry ? this.toView(entry) : null;
  });

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    if (!this.features.enabled('nightDuty')) {
      return;
    }

    try {
      const me = await this.users.ensureCurrentUser();
      const today = this.clock.today();
      const [nights, guardians] = await Promise.all([
        this.nightDuty.listNights(me.id, today, today),
        this.guardians.listMyGuardians().catch((): GuardianSummary[] => []),
      ]);
      this.guardianList.set(guardians);
      this.entry.set(nights[0] ? nightEntry(nights[0]) : null);
    } catch {
      // No card is the safe fallback.
    }
  }

  private toView(entry: NightEntry): TonightView {
    const responsible = entry.responsible;
    const note = entry.note ?? '';
    // Stryker disable next-line StringLiteral: host only fills the sleepover line, which sets its own
    const base = { host: '', nameKey: null, note };

    switch (responsible.kind) {
      case NIGHT_RESPONSIBLE.guardian: {
        const guardian = this.guardianList().find((g) => g.id === responsible.guardianId);
        return {
          ...base,
          icon: '🌙',
          name: guardian ? childFacingName(guardian) : null,
          nameKey: guardian ? null : 'child.home.tonight.aGuardian',
          lineKey: 'child.home.tonight.guardian',
        };
      }
      case NIGHT_RESPONSIBLE.babysitter:
        return {
          ...base,
          icon: '🧑‍🍼',
          name: (responsible.name ?? '') || null,
          nameKey: responsible.name ? null : 'child.home.tonight.aBabysitter',
          lineKey: 'child.home.tonight.babysitter',
        };
      case NIGHT_RESPONSIBLE.sleepover:
        return {
          ...base,
          icon: '🛏️',
          name: responsible.hostName,
          host: responsible.hostName,
          lineKey: 'child.home.tonight.sleepover',
        };
      default:
        // Stryker disable next-line StringLiteral: with no name the card shows no avatar, so the icon is never rendered
        return { ...base, icon: '🌙', name: null, lineKey: null };
    }
  }
}
