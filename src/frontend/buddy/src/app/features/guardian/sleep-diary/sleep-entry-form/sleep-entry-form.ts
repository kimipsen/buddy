import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { SleepDiaryService, SleepEntry, SleepInterval } from '../../../../core/sleep-diary.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { RepeatableRow } from '../../../../shared/repeatable-row/repeatable-row';
import { TimeRange } from '../../../../shared/time-range/time-range';
import { TimeSelect } from '../../../../shared/time-select/time-select';
import { Toggle } from '../../../../shared/toggle/toggle';
import {
  DEFAULT_INTERVAL_MINUTES,
  SleepEntryDraft,
  SleepIntervalField,
  SleepTimeField,
  draftFromEntry,
  draftToRequest,
  emptyDraft,
  splitMinutes,
  suggestTotalSleepMinutes,
} from '../sleep-entry-draft';

type FormAction = 'save' | 'clear';

// The API's bounds (LogSleepEntryValidator), so a typed value can't turn into a 400 on save.
const MAX_INTERVAL_MINUTES = 720;
const MAX_TOTAL_MINUTES = 24 * 60;

function clamp(value: number | null, min: number, max: number): number {
  return Math.min(max, Math.max(min, Math.round(value ?? min)));
}

// Wake-ups and naps share one repeatable time + duration row (visual-specification.md).
const INTERVAL_GROUPS: {
  field: SleepIntervalField;
  title: string;
  timeLabel: string;
  addLabel: string;
}[] = [
  {
    field: 'nightWakeUps',
    title: 'sleepDiary.form.nightWakeUps',
    timeLabel: 'sleepDiary.form.wakeUpTime',
    addLabel: 'sleepDiary.form.addWakeUp',
  },
  {
    field: 'naps',
    title: 'sleepDiary.form.naps',
    timeLabel: 'sleepDiary.form.napTime',
    addLabel: 'sleepDiary.form.addNap',
  },
];

// One night's row, laid out in the order the night happens (visual-specification.md, worked
// example). Owns only the draft; the page owns which child/date is shown and the logged entries.
@Component({
  selector: 'app-sleep-entry-form',
  imports: [FormsModule, DateSelect, RepeatableRow, TimeRange, TimeSelect, Toggle, TranslatePipe],
  templateUrl: './sleep-entry-form.html',
})
export class SleepEntryForm {
  private readonly sleepDiary = inject(SleepDiaryService);

  readonly childId = input.required<string>();
  readonly date = input.required<string>();
  // The entry already logged for date, or null for a night not logged yet.
  readonly entry = input<SleepEntry | null>(null);
  // True while the page is still loading what's stored, so a save can't overwrite it unseen.
  readonly locked = input(false);

  readonly dateChange = output<string>();
  readonly saved = output<SleepEntry>();
  readonly cleared = output<string>();

  private readonly shown = computed(() => ({
    childId: this.childId(),
    date: this.date(),
    entry: this.entry(),
  }));

  // Reset to the logged entry (or a blank night) whenever the child, date or entry changes.
  protected readonly draft = linkedSignal<{ entry: SleepEntry | null }, SleepEntryDraft>({
    source: this.shown,
    computation: ({ entry }) => (entry ? draftFromEntry(entry) : emptyDraft()),
  });
  // Until the guardian types a total, the form shows (and saves) the suggestion.
  private readonly totalEdited = linkedSignal<{ entry: SleepEntry | null }, boolean>({
    source: this.shown,
    computation: ({ entry }) => entry?.totalSleepMinutes != null,
  });

  protected readonly suggestion = computed(() => suggestTotalSleepMinutes(this.draft()));
  protected readonly total = computed(() =>
    this.totalEdited() ? this.draft().totalSleepMinutes : this.suggestion(),
  );
  protected readonly totalParts = computed(() => {
    const total = this.total();
    return total === null ? null : splitMinutes(total);
  });
  protected readonly showsSuggestion = computed(
    () => !this.totalEdited() && this.suggestion() !== null,
  );
  protected readonly canUseSuggestion = computed(() => {
    const suggestion = this.suggestion();
    return (
      this.totalEdited() && suggestion !== null && suggestion !== this.draft().totalSleepMinutes
    );
  });

  protected readonly splitSuggestion = computed(() => splitMinutes(this.suggestion() ?? 0));

  protected readonly intervalGroups = INTERVAL_GROUPS;
  protected readonly action = createAction<FormAction>();
  // `${childId}|${date}` of the last successful save, until the next edit.
  private readonly savedKey = signal<string | null>(null);
  protected readonly justSaved = computed(
    () => this.savedKey() === `${this.childId()}|${this.date()}`,
  );

  protected setTime(field: SleepTimeField, value: string): void {
    this.patch({ [field]: value });
  }

  protected setTired(isTired: boolean): void {
    this.patch({ isTired });
  }

  protected setRemarks(remarks: string): void {
    this.patch({ remarks });
  }

  protected addInterval(field: SleepIntervalField): void {
    this.patch({
      [field]: [
        ...this.draft()[field],
        { startTime: '', durationMinutes: DEFAULT_INTERVAL_MINUTES },
      ],
    });
  }

  protected removeInterval(field: SleepIntervalField, index: number): void {
    this.patch({ [field]: this.draft()[field].filter((_, i) => i !== index) });
  }

  protected updateInterval(
    field: SleepIntervalField,
    index: number,
    change: Partial<SleepInterval>,
  ): void {
    this.patch({
      [field]: this.draft()[field].map((interval, i) =>
        i === index ? { ...interval, ...change } : interval,
      ),
    });
  }

  protected setDuration(field: SleepIntervalField, index: number, value: number | null): void {
    this.updateInterval(field, index, { durationMinutes: clamp(value, 1, MAX_INTERVAL_MINUTES) });
  }

  protected setTotal(part: 'hours' | 'minutes', value: number | null): void {
    const current = this.totalParts() ?? { hours: 0, minutes: 0 };
    const next = { ...current, [part]: clamp(value, 0, part === 'hours' ? 24 : 59) };

    this.totalEdited.set(true);
    this.patch({ totalSleepMinutes: Math.min(next.hours * 60 + next.minutes, MAX_TOTAL_MINUTES) });
  }

  protected useSuggestion(): void {
    this.totalEdited.set(false);
    this.savedKey.set(null);
  }

  protected async save(): Promise<void> {
    const childId = this.childId();
    const date = this.date();
    const request = draftToRequest(this.draft(), this.total());

    await this.action.run(
      'save',
      async () => {
        const entry = await this.sleepDiary.logEntry(childId, date, request);
        this.saved.emit(entry);
        this.savedKey.set(`${childId}|${date}`);
      },
      'sleepDiary.form.saveError',
    );
  }

  protected async clear(): Promise<void> {
    const childId = this.childId();
    const date = this.date();

    await this.action.run(
      'clear',
      async () => {
        await this.sleepDiary.clearEntry(childId, date);
        this.cleared.emit(date);
      },
      'sleepDiary.form.clearError',
    );
  }

  private patch(change: Partial<SleepEntryDraft>): void {
    this.draft.update((draft) => ({ ...draft, ...change }));
    this.savedKey.set(null);
  }
}
