import { Component, computed, inject, input, linkedSignal, resource, signal } from '@angular/core';

import { addDaysIso, parseIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  NIGHT_RESPONSIBLE,
  Night,
  NightDutyService,
  NightEntry,
  NightOverride,
  isNightOverride,
  nightEntry,
  scheduledEntry,
} from '../../../../core/night-duty.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { NightEditDialog } from '../night-edit-dialog/night-edit-dialog';
import { NightPeople, ResponsibleLabels, ResponsibleText, responsibleText } from '../night-people';

const DAYS = 7;

interface NightTile {
  night: Night;
  label: string;
  morning: string;
  who: ResponsibleText | null;
  note: string;
  changed: boolean;
  // What the fixed schedule says, shown under a changed night.
  scheduledWho: string | null;
}

// One week of a child's resolved nights. Selecting a night opens the edit dialog; "Switch two
// nights" swaps who is on duty between two nights in one request, each keeping its own note
// (docs/backend/analysis/night-duty.md, Question 5).
@Component({
  selector: 'app-night-week',
  imports: [TranslatePipe, Card, NightEditDialog],
  templateUrl: './night-week.html',
})
export class NightWeek {
  private readonly nightDuty = inject(NightDutyService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly people = input.required<NightPeople>();
  // Bumped by the page when the fixed schedule changes, so the week reloads.
  readonly revision = input(0);
  // The child's today (from the server, in the child's time zone), so the week starts on the same
  // night as the child's "Tonight" card.
  readonly today = input.required<string>();

  // The first night shown; starts tonight, like the pickup planner, and moves a week at a time.
  protected readonly start = linkedSignal(() => this.today());
  protected readonly rangeLabel = computed(() => {
    const locale = this.translation.language();
    const format = (iso: string) =>
      parseIsoDate(iso).toLocaleDateString(locale, { month: 'short', day: 'numeric' });
    const start = this.start();
    return { from: format(start), to: format(addDaysIso(start, DAYS - 1)) };
  });

  protected readonly nights = resource({
    params: () => ({ childId: this.childId(), from: this.start(), revision: this.revision() }),
    loader: ({ params }) =>
      this.nightDuty.listNights(params.childId, params.from, addDaysIso(params.from, DAYS - 1)),
  });

  // Kept on screen while the next week or a reload loads.
  private readonly shown = linkedSignal<Night[] | undefined, Night[]>({
    source: () => (this.nights.hasValue() ? this.nights.value() : undefined),
    computation: (next, previous) => next ?? previous?.value ?? [],
  });

  protected readonly tiles = computed((): NightTile[] => {
    const locale = this.translation.language();
    const people = this.people();
    return this.shown().map((night) => this.tile(night, people, locale));
  });

  protected readonly saving = createAction<string>();
  protected readonly editing = signal<NightTile | null>(null);

  protected readonly switching = signal(false);
  protected readonly picked = signal<string[]>([]);
  protected readonly pickedTiles = computed(() =>
    this.picked().map((date) => this.tiles().find((tile) => tile.night.date === date)),
  );

  protected previousWeek(): void {
    this.start.update((start) => addDaysIso(start, -DAYS));
    this.picked.set([]);
  }

  protected nextWeek(): void {
    this.start.update((start) => addDaysIso(start, DAYS));
    this.picked.set([]);
  }

  protected open(tile: NightTile): void {
    if (this.switching()) {
      this.togglePick(tile.night.date);
      return;
    }
    this.saving.clearError();
    this.editing.set(tile);
  }

  protected closeEditor(): void {
    this.editing.set(null);
  }

  protected async saveNight(date: string, entry: NightEntry | null): Promise<void> {
    await this.saveOverrides([{ date, entry }], date);
  }

  protected async undo(date: string): Promise<void> {
    const childId = this.childId();
    const done = await this.saving.run(
      date,
      () => this.nightDuty.clearOverrides(childId, date, date),
      'nightDuty.week.saveError',
    );
    if (done) {
      this.editing.set(null);
      this.nights.reload();
    }
  }

  protected toggleSwitching(): void {
    this.switching.update((on) => !on);
    this.picked.set([]);
  }

  protected isPicked(date: string): boolean {
    return this.picked().includes(date);
  }

  private togglePick(date: string): void {
    this.picked.update((picked) => {
      if (picked.includes(date)) {
        return picked.filter((d) => d !== date);
      }
      // A third pick is ignored until one is deselected.
      return picked.length < 2 ? [...picked, date] : picked;
    });
  }

  // Swaps the responsible persons of the two picked nights; each keeps its own note. A night with
  // nobody named on one side gives the other side only its note -- or clears it if it has none.
  protected async confirmSwitch(): Promise<void> {
    const [first, second] = this.pickedTiles();
    if (!first || !second) {
      return;
    }

    const swapped = (night: Night, other: Night): NightOverride => {
      const otherResponsible = nightEntry(other)?.responsible;
      const note = nightEntry(night)?.note ?? '';
      if (otherResponsible && otherResponsible.kind !== NIGHT_RESPONSIBLE.unassigned) {
        return { date: night.date, entry: { responsible: otherResponsible, note } };
      }
      const noteOnly: NightEntry = { responsible: { kind: NIGHT_RESPONSIBLE.unassigned }, note };
      return { date: night.date, entry: note ? noteOnly : null };
    };

    const ok = await this.saveOverrides(
      [swapped(first.night, second.night), swapped(second.night, first.night)],
      'switch',
    );
    if (ok) {
      this.switching.set(false);
      this.picked.set([]);
    }
  }

  private async saveOverrides(nights: NightOverride[], id: string): Promise<boolean> {
    const childId = this.childId();
    const done = await this.saving.run(
      id,
      async () => {
        await this.nightDuty.setOverrides(childId, nights);
      },
      'nightDuty.week.saveError',
    );
    if (done) {
      this.editing.set(null);
      this.nights.reload();
    }
    return done;
  }

  private tile(night: Night, people: NightPeople, locale: string): NightTile {
    const date = parseIsoDate(night.date);
    const next = parseIsoDate(addDaysIso(night.date, 1));
    const entry = nightEntry(night);
    const changed = isNightOverride(night);
    const scheduled = scheduledEntry(night);
    const labels = {
      guardian: this.translation.translate('nightDuty.who.guardian'),
      babysitter: this.translation.translate('nightDuty.who.babysitter'),
      sleepover: (host: string) => this.translation.translate('nightDuty.who.sleepover', { host }),
    };

    return {
      night,
      label: date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' }),
      morning: next.toLocaleDateString(locale, { weekday: 'short' }),
      who: entry ? responsibleText(entry.responsible, people, labels) : null,
      note: entry?.note ?? '',
      changed,
      scheduledWho: changed ? this.scheduledText(scheduled, people, labels) : null,
    };
  }

  // What the fixed schedule says for a changed night: its person, else its note; '' for nothing.
  private scheduledText(
    scheduled: NightEntry | null,
    people: NightPeople,
    labels: ResponsibleLabels,
  ): string {
    if (!scheduled) {
      return '';
    }
    return responsibleText(scheduled.responsible, people, labels)?.text ?? scheduled.note ?? '';
  }
}
