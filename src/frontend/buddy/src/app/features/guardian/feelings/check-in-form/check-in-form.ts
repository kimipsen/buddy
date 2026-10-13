import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MAX_NOTE_LENGTH, MAX_TAGS, tagsFittingZone } from '../../../../core/feelings';
import {
  FeelingCheckIn,
  FeelingTag,
  FeelingZone,
  FeelingsService,
} from '../../../../core/feelings.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { FeelingTagChips } from '../../../../shared/feeling-tag-chips/feeling-tag-chips';
import { FeelingZoneButtons } from '../../../../shared/feeling-zone-buttons/feeling-zone-buttons';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { TimeSelect } from '../../../../shared/time-select/time-select';

type When = 'now' | 'earlier';

/**
 * A guardian's observation: logged now or backdated, or an edit of one already logged (a full
 * overwrite). The child's zone buttons at guardian size, the same chips, and app-date-select /
 * app-time-select for an earlier moment (Question 5: backdating is fine, the future is not).
 */
@Component({
  selector: 'app-feelings-check-in-form',
  imports: [
    Card,
    DateSelect,
    FeelingTagChips,
    FeelingZoneButtons,
    FormsModule,
    SegmentedControl,
    TimeSelect,
    TranslatePipe,
  ],
  templateUrl: './check-in-form.html',
})
export class FeelingsCheckInForm {
  private readonly feelings = inject(FeelingsService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  // The day selected on the page: an earlier observation defaults to it.
  readonly defaultDate = input.required<string>();
  readonly editing = input<FeelingCheckIn | null>(null);

  readonly saved = output<FeelingCheckIn>();
  readonly cancelled = output<void>();

  protected readonly zone = signal<FeelingZone | null>(null);
  protected readonly tags = signal<FeelingTag[]>([]);
  protected readonly note = signal('');
  protected readonly when = signal<When>('now');
  protected readonly date = signal('');
  protected readonly time = signal('');
  protected readonly savedNotice = signal(false);

  protected readonly saving = createAction();
  protected readonly maxNote = MAX_NOTE_LENGTH;

  protected readonly whenOptions = computed((): SegmentedControlOption<When>[] => [
    { value: 'now', label: this.translation.translate('feelings.guardian.form.now') },
    { value: 'earlier', label: this.translation.translate('feelings.guardian.form.earlier') },
  ]);
  protected readonly canSave = computed(
    () =>
      this.zone() !== null &&
      (this.when() === 'now' || (this.date() !== '' && this.time() !== '')) &&
      !this.saving.busy(),
  );

  constructor() {
    // A new edit (or the end of one) refills the form.
    effect(() => {
      const editing = this.editing();
      this.reset(editing);

      if (editing) {
        this.savedNotice.set(false);
      }
    });
  }

  protected pick(zone: FeelingZone): void {
    this.zone.set(zone);
    this.tags.update((tags) => tagsFittingZone(tags, zone));
    this.savedNotice.set(false);
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

  protected setWhen(when: When): void {
    this.when.set(when);

    if (when === 'earlier' && this.date() === '') {
      this.date.set(this.defaultDate());
    }
  }

  protected async save(): Promise<void> {
    const zone = this.zone();
    const editing = this.editing();

    if (zone === null) {
      return;
    }

    const content = { zone, tags: this.tags(), note: this.note().trim() };

    await this.saving.run(
      true,
      async () => {
        const saved = editing
          ? await this.feelings.editCheckIn(this.childId(), editing.id, {
              ...content,
              date: this.date(),
              time: this.time(),
            })
          : this.when() === 'now'
            ? await this.feelings.logCheckIn(this.childId(), content, null, null)
            : await this.feelings.logCheckIn(this.childId(), content, this.date(), this.time());
        this.saved.emit(saved);
        this.reset(null);
        this.savedNotice.set(true);
      },
      'feelings.guardian.form.saveError',
    );
  }

  protected cancel(): void {
    this.reset(null);
    this.savedNotice.set(false);
    this.cancelled.emit();
  }

  private reset(editing: FeelingCheckIn | null): void {
    this.zone.set(editing?.zone ?? null);
    this.tags.set(editing ? [...editing.tags] : []);
    this.note.set(editing?.note ?? '');
    this.when.set(editing ? 'earlier' : 'now');
    this.date.set(editing?.date ?? '');
    this.time.set(editing?.time ?? '');
    this.saving.reset();
  }
}
