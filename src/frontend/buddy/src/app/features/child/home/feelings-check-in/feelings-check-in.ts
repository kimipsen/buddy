import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, effect, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ChildClockService } from '../../../../core/child-clock.service';
import {
  MAX_NOTE_LENGTH,
  MAX_TAGS,
  ZONES,
  promptState,
  tagsFittingZone,
  undoMsLeft,
  zoneInfo,
} from '../../../../core/feelings';
import {
  FeelingCheckIn,
  FeelingTag,
  FeelingZone,
  FeelingsService,
  MyFeelingsToday,
} from '../../../../core/feelings.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { UsersService } from '../../../../core/users.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { FeelingTagChips } from '../../../../shared/feeling-tag-chips/feeling-tag-chips';
import { FeelingZoneButtons } from '../../../../shared/feeling-zone-buttons/feeling-zone-buttons';

// How often the card re-reads the child's clock: the undo countdown, a prompt opening, and the
// child's day turning over. A minute-level countdown needs nothing finer.
export const FEELINGS_TICK_MS = 15_000;

type Step = 'pick' | 'details' | 'saved';

/**
 * The child's feelings check-in on the home page (docs/backend/analysis/feelings-check-in.md,
 * Frontend): tap a face, optionally add words or a note, "Done". Then "Thanks!" with Undo for 10
 * minutes, and today's own check-ins as face chips. "Today" and "now" are the child's own clock
 * (ChildClockService), never the device's zone. No stars: rewarding check-ins would reward checking
 * in, not honesty.
 */
@Component({
  selector: 'app-feelings-check-in',
  imports: [FormsModule, FeelingTagChips, FeelingZoneButtons, TranslatePipe],
  templateUrl: './feelings-check-in.html',
})
export class FeelingsCheckIn {
  private readonly feelings = inject(FeelingsService);
  private readonly users = inject(UsersService);
  private readonly clock = inject(ChildClockService);
  private readonly translation = inject(TranslationService);

  protected readonly today = resource({ loader: () => this.feelings.getMyToday() });
  protected readonly me = resource({ loader: () => this.users.ensureCurrentUser() });

  // Re-read every tick, so the countdown and the prompt follow the clock.
  protected readonly now = signal(this.clock.now());

  protected readonly step = signal<Step>('pick');
  protected readonly zone = signal<FeelingZone | null>(null);
  protected readonly tags = signal<FeelingTag[]>([]);
  // Stryker disable next-line StringLiteral: the first pick() always resets it, and nothing reads it before that
  protected readonly note = signal('');
  // Stryker disable next-line BooleanLiteral: the first pick() always resets it, and nothing reads it before that
  protected readonly noteOpen = signal(false);
  protected readonly lastSaved = signal<FeelingCheckIn | null>(null);
  // When the save came back, on this device's clock. The undo countdown runs from here rather than
  // from the server's recordedAt, so a device clock that is off doesn't close (or stretch) it.
  private readonly savedAtMs = signal(0);
  // The child's date the card last reloaded for, so a clock that disagrees with the server's around
  // midnight reloads once, not on every tick.
  private reloadedFor: string | null = null;
  protected readonly announcement = signal('');

  protected readonly saving = createAction();
  protected readonly undoing = createAction();

  protected readonly maxNote = MAX_NOTE_LENGTH;
  protected readonly maxTags = MAX_TAGS;
  protected readonly zones = ZONES;
  protected readonly zoneInfo = zoneInfo;

  protected readonly displayMode = computed(() =>
    // Stryker disable next-line StringLiteral: without today the steps aren't shown, and anything but 'FacesOnly' reads the same
    this.today.hasValue() ? this.today.value().displayMode : 'FacesAndWords',
  );
  protected readonly facesOnly = computed(() => this.displayMode() === 'FacesOnly');
  protected readonly checkIns = computed(() =>
    this.today.hasValue() ? this.today.value().checkIns : [],
  );
  protected readonly prompt = computed(() =>
    this.today.hasValue()
      ? promptState(
          this.today.value().promptTimes,
          this.checkIns(),
          this.clock.minutesOfDay(this.now()),
        )
      : { open: null },
  );
  protected readonly undoMinutesLeft = computed(() => {
    return this.lastSaved() ? Math.ceil(undoMsLeft(this.savedAtMs(), this.now()) / 60_000) : 0;
  });
  protected readonly givenName = computed(() =>
    this.me.hasValue() ? this.me.value().name.givenName : '',
  );

  constructor() {
    const timer = setInterval(() => this.tick(), FEELINGS_TICK_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    // The undo window closing ends the "Thanks" state.
    effect(() => {
      // Stryker disable next-line ConditionalExpression: undoMinutesLeft only drops to 0 on the saved step or where the step is set to 'pick' anyway
      if (this.step() === 'saved' && this.undoMinutesLeft() === 0) {
        this.step.set('pick');
      }
    });
  }

  // A first pick starts afresh; changing the face on the details step keeps the note and the
  // words that still fit.
  protected pick(zone: FeelingZone): void {
    if (this.step() === 'details') {
      this.tags.update((tags) => tagsFittingZone(tags, zone));
    } else {
      this.tags.set([]);
      this.note.set('');
      this.noteOpen.set(false);
    }

    this.zone.set(zone);
    this.saving.reset();
    this.undoing.clearError();
    this.step.set('details');
  }

  protected toggleTag(tag: FeelingTag): void {
    this.tags.update((tags) =>
      tags.includes(tag)
        ? tags.filter((t) => t !== tag)
        : tags.length < MAX_TAGS
          ? [...tags, tag]
          : tags,
    );
  }

  protected back(): void {
    // Stryker disable next-line CallExpression: zone is only read on the details step, and every way into it sets zone first
    this.zone.set(null);
    this.step.set('pick');
  }

  protected async done(): Promise<void> {
    const zone = this.zone();

    if (zone === null) {
      return;
    }

    await this.saving.run(
      true,
      async () => {
        const saved = await this.feelings.checkIn({
          zone,
          tags: this.tags(),
          note: this.note().trim(),
        });
        this.now.set(this.clock.now());
        this.savedAtMs.set(this.now());
        this.lastSaved.set(saved);
        this.updateToday((today) => ({ ...today, checkIns: [...today.checkIns, saved] }));
        this.announcement.set(this.savedAnnouncement(zone));
        // Stryker disable next-line CallExpression: zone is only read on the details step, and every way into it sets zone first
        this.zone.set(null);
        this.step.set('saved');
      },
      'feelings.child.saveError',
    );
  }

  protected async undo(): Promise<void> {
    const saved = this.lastSaved();

    if (saved === null) {
      return;
    }

    await this.undoing.run(
      true,
      async () => {
        await this.feelings.undoCheckIn(saved.id);
        this.updateToday((today) => ({
          ...today,
          checkIns: today.checkIns.filter((c) => c.id !== saved.id),
        }));
        // Stryker disable next-line CallExpression: lastSaved is only read on the saved step, and every way into it sets lastSaved first
        this.lastSaved.set(null);
        this.announcement.set(this.translation.translate('feelings.child.undone'));
        this.step.set('pick');
      },
      (error) => {
        // The server's window closed first (check_in_undo_window_closed): the check-in stays, and
        // the card goes back to the buttons.
        if (error instanceof HttpErrorResponse && error.status === 409) {
          // Stryker disable next-line CallExpression: lastSaved is only read on the saved step, and every way into it sets lastSaved first
          this.lastSaved.set(null);
          this.step.set('pick');
          return 'feelings.child.undoTooLate';
        }

        return 'feelings.child.undoError';
      },
    );
  }

  protected checkInAgain(): void {
    // Stryker disable next-line CallExpression: lastSaved is only read on the saved step, and every way into it sets lastSaved first
    this.lastSaved.set(null);
    this.step.set('pick');
  }

  // A face chip's accessible text: the time plus the face (faces-only) or the zone's word.
  protected chipLabel(checkIn: FeelingCheckIn): string {
    const info = zoneInfo(checkIn.zone);
    const what = this.translation.translate(this.facesOnly() ? info.faceNameKey : info.labelKey);
    return `${checkIn.time} ${what}`;
  }

  private tick(): void {
    this.now.set(this.clock.now());

    // The child's day turned over: today's chips and the prompts belong to the new day. Once per
    // date: if the server still answers the old date (clocks a little apart), it waits for the next.
    const childToday = this.clock.today(this.now());

    if (
      this.today.hasValue() &&
      this.today.value().date !== childToday &&
      this.reloadedFor !== childToday
    ) {
      this.reloadedFor = childToday;
      this.today.reload();
    }
  }

  // The announcement names the colour and the word, or nothing at all in faces-only mode.
  private savedAnnouncement(zone: FeelingZone): string {
    if (this.facesOnly()) {
      return this.translation.translate('feelings.child.savedAnnounceFaces');
    }

    const info = zoneInfo(zone);
    return this.translation.translate('feelings.child.savedAnnounceWords', {
      label: this.translation.translate(info.labelKey),
      colour: this.translation.translate(info.colourKey),
    });
  }

  private updateToday(change: (today: MyFeelingsToday) => MyFeelingsToday): void {
    if (this.today.hasValue()) {
      this.today.set(change(this.today.value()));
    }
  }
}
