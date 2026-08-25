import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { toIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardianSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { AssignPickupRequest, PickupOccurrence, PickupSlot, PickupsService } from '../../../../core/pickups.service';
import { PickupCell } from '../pickup-cell/pickup-cell';

const SLOT_LABELS: Record<PickupSlot, string> = {
  0: 'pickup.slots.dropOff',
  1: 'pickup.slots.pickUp'
};

const SLOTS: PickupSlot[] = [0, 1];
const DAYS_AHEAD = 7;

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
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' })
    };
  });
}

@Component({
  selector: 'app-manage-pickups',
  imports: [FormsModule, PickupCell, TranslatePipe],
  templateUrl: './manage-pickups.html'
})
export class ManagePickups implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);
  private readonly translation = inject(TranslationService);

  protected readonly slots = SLOTS;
  protected readonly slotLabels = SLOT_LABELS;
  protected readonly week = computed(() => buildWeek(this.translation.language()));

  protected readonly hasChildren = signal(true);
  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);

  protected readonly childGuardians = signal<GuardianSummary[]>([]);
  protected readonly siblings = computed(() =>
    this.children().filter((child) => child.id !== this.selectedChildId())
  );

  protected readonly entriesByKey = signal<Partial<Record<string, PickupOccurrence>>>({});
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingKey = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected key(date: string, slot: PickupSlot): string {
    return `${date}|${slot}`;
  }

  protected occurrenceFor(date: string, slot: PickupSlot): PickupOccurrence | null {
    return this.entriesByKey()[this.key(date, slot)] ?? null;
  }

  protected async onChildChange(childId: string): Promise<void> {
    this.selectedChildId.set(childId);
    await this.loadForChild(childId);
  }

  protected async onAssign(date: string, slot: PickupSlot, request: AssignPickupRequest): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    const key = this.key(date, slot);
    this.savingKey.set(key);
    this.error.set(null);

    try {
      const occurrence = await this.pickups.assignPickup(childId, date, slot, request);
      this.entriesByKey.update((current) => ({ ...current, [key]: occurrence }));
    } catch {
      this.error.set('pickup.assign.updateError');
    } finally {
      this.savingKey.set(null);
    }
  }

  protected async onClear(date: string, slot: PickupSlot): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    const key = this.key(date, slot);
    this.savingKey.set(key);
    this.error.set(null);

    try {
      await this.pickups.clearPickup(childId, date, slot);
      this.entriesByKey.update((current) => {
        const next = { ...current };
        delete next[key];
        return next;
      });
    } catch {
      this.error.set('pickup.assign.updateError');
    } finally {
      this.savingKey.set(null);
    }
  }

  private async loadChildren(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.children.set(children);
      this.selectedChildId.set(children[0].id);
      await this.loadForChild(children[0].id);
    } catch {
      this.error.set('pickup.assign.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadForChild(childId: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.entriesByKey.set({});

    try {
      const week = this.week();
      const [childGuardians, occurrences] = await Promise.all([
        this.guardians.listChildGuardians(childId),
        this.pickups.listSchedule(childId, week[0].date, week.at(-1)!.date)
      ]);

      this.childGuardians.set(childGuardians);

      const byKey: Partial<Record<string, PickupOccurrence>> = {};

      for (const occurrence of occurrences) {
        byKey[this.key(occurrence.date, occurrence.slot)] = occurrence;
      }

      this.entriesByKey.set(byKey);
    } catch {
      this.error.set('pickup.assign.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
