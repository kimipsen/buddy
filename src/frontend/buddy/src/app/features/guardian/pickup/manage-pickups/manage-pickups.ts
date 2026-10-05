import { Component, computed, inject, linkedSignal, resource } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { firstAndLast, sortByChildName } from '../../../../core/array-utils';
import { BabysittersService, ChildBabysitter } from '../../../../core/babysitters.service';
import { toIsoDate } from '../../../../core/date-utils';
import {
  ChildSummary,
  GuardianSummary,
  GuardiansService,
} from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  AssignPickupRequest,
  PickupOccurrence,
  PickupSlot,
  PickupsService,
} from '../../../../core/pickups.service';
import { ActionState, createAction } from '../../../../shared/action-state/action-state';
import { PickupCell } from '../pickup-cell/pickup-cell';

const SLOT_LABELS: Record<PickupSlot, string> = {
  0: 'pickup.slots.dropOff',
  1: 'pickup.slots.pickUp',
};

const SLOTS: PickupSlot[] = [0, 1];
const DAYS_AHEAD = 7;

type EntriesByKey = Partial<Record<string, PickupOccurrence>>;

// What the grid shows for the selected child: who can be assigned, and the week's occurrences.
interface ChildSchedule {
  childGuardians: GuardianSummary[];
  childBabysitters: ChildBabysitter[];
  entriesByKey: EntriesByKey;
}

interface ScheduleRequest {
  childId: string;
  from: string;
  to: string;
}

const EMPTY_SCHEDULE: ChildSchedule = {
  childGuardians: [],
  childBabysitters: [],
  entriesByKey: {},
};

interface WeekDay {
  date: string;
  label: string;
}

function buildWeek(locale: string): WeekDay[] {
  const today = new Date();

  return Array.from({ length: DAYS_AHEAD }, (_, offset) => {
    const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() + offset);

    return {
      date: toIsoDate(date),
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' }),
    };
  });
}

@Component({
  selector: 'app-manage-pickups',
  imports: [FormsModule, PickupCell, TranslatePipe],
  templateUrl: './manage-pickups.html',
})
export class ManagePickups {
  private readonly babysitters = inject(BabysittersService);
  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);
  private readonly translation = inject(TranslationService);

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;
  protected readonly week = computed(() => buildWeek(this.translation.language()));

  protected readonly children = resource({
    loader: () => this.guardians.listMyChildren().then(sortByChildName),
  });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  // The first child until the guardian picks another.
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);

  protected readonly siblings = computed((): ChildSummary[] =>
    this.childList().filter((child) => child.id !== this.selectedChildId()),
  );

  // Ids are `${childId}|${date}|${slot}`, so a save only ever shows against its own child.
  protected readonly saving = createAction<string>();
  protected readonly childSaveState = computed((): ActionState<string> => {
    const state = this.saving.state();
    return state.status === 'idle' || state.id.startsWith(`${this.selectedChildId()}|`)
      ? state
      : { status: 'idle' };
  });

  // What the schedule is fetched for. Compared by value, so relabelling week() (a language switch)
  // doesn't refetch; a save compares it by identity to tell whether its child and week still show.
  private readonly request = computed(
    (): ScheduleRequest | undefined => {
      const childId = this.selectedChildId();

      if (!childId) {
        return undefined;
      }

      const [first, last] = firstAndLast(this.week());
      return { childId, from: first.date, to: last.date };
    },
    {
      equal: (a, b) => a?.childId === b?.childId && a?.from === b?.from && a?.to === b?.to,
    },
  );

  // Switching child cancels the previous child's load, so a slow response can't overwrite the
  // newer selection's data (see selectChildIfPresent in e2e/support/guardian-data.ts). It also
  // drops a save error left over from the previous child.
  protected readonly schedule = resource({
    params: () => this.request(),
    loader: ({ params }) => {
      this.saving.clearError();
      return this.loadSchedule(params.childId, params.from, params.to);
    },
  });
  // Empty while loading or after a failed load, so the grid still renders.
  protected readonly loaded = computed((): ChildSchedule =>
    this.schedule.hasValue() ? this.schedule.value() : EMPTY_SCHEDULE,
  );

  protected key(date: string, slot: PickupSlot): string {
    return `${date}|${slot}`;
  }

  protected saveId(date: string, slot: PickupSlot): string {
    return `${this.selectedChildId()}|${this.key(date, slot)}`;
  }

  protected occurrenceFor(date: string, slot: PickupSlot): PickupOccurrence | null {
    return this.loaded().entriesByKey[this.key(date, slot)] ?? null;
  }

  protected async onAssign(
    date: string,
    slot: PickupSlot,
    request: AssignPickupRequest,
  ): Promise<void> {
    const shown = this.request();

    if (!shown) {
      return;
    }

    const key = this.key(date, slot);

    await this.saving.run(
      `${shown.childId}|${key}`,
      async () => {
        const occurrence = await this.pickups.assignPickup(shown.childId, date, slot, request);
        this.updateEntries(shown, (current) => ({ ...current, [key]: occurrence }));
      },
      'pickup.assign.updateError',
    );
  }

  protected async onClear(date: string, slot: PickupSlot): Promise<void> {
    const shown = this.request();

    if (!shown) {
      return;
    }

    const key = this.key(date, slot);

    await this.saving.run(
      `${shown.childId}|${key}`,
      async () => {
        await this.pickups.clearPickup(shown.childId, date, slot);
        this.updateEntries(shown, (current) => {
          const next = { ...current };
          delete next[key];
          return next;
        });
      },
      'pickup.assign.updateError',
    );
  }

  // Applies a save's result only if the grid still shows the child and week it was made for, and
  // that schedule has loaded: setting the resource mid-load would cancel the load.
  private updateEntries(
    shown: ScheduleRequest,
    change: (current: EntriesByKey) => EntriesByKey,
  ): void {
    const schedule = this.schedule;

    if (this.request() === shown && schedule.hasValue() && !schedule.isLoading()) {
      const current = schedule.value();
      schedule.set({ ...current, entriesByKey: change(current.entriesByKey) });
    }
  }

  private async loadSchedule(childId: string, from: string, to: string): Promise<ChildSchedule> {
    const [childGuardians, childBabysitters, occurrences] = await Promise.all([
      this.guardians.listChildGuardians(childId),
      // Best effort: without babysitters the grid still works, only the babysitter picker is empty.
      this.babysitters.listForChild(childId).catch((): ChildBabysitter[] => []),
      this.pickups.listSchedule(childId, from, to),
    ]);

    const entriesByKey: EntriesByKey = {};

    for (const occurrence of occurrences) {
      entriesByKey[this.key(occurrence.date, occurrence.slot)] = occurrence;
    }

    return { childGuardians, childBabysitters, entriesByKey };
  }
}
