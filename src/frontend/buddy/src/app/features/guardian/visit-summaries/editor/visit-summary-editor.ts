import { Component, computed, inject, input, linkedSignal, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { addDaysIso, todayIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  VISIT_SUMMARY_SECTIONS,
  VisitSummariesService,
  VisitSummary,
  VisitSummarySection,
} from '../../../../core/visit-summaries.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { DateSelect } from '../../../../shared/date-select/date-select';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Toggle } from '../../../../shared/toggle/toggle';

// The backend's limits (VisitSummaryRules), checked here first so the guardian gets the message
// without a round trip.
export const MAX_PERIOD_DAYS = 92;
export const MAX_TITLE_LENGTH = 100;
export const MAX_NOTE_LENGTH = 4000;

type Preset = 'weeks4' | 'months3' | 'custom';

// Each preset ends today: a summary is usually prepared just before the visit.
const PRESET_DAYS: Record<Exclude<Preset, 'custom'>, number> = { weeks4: 28, months3: 91 };

// Creates a summary, or edits one when `summary` is set. Saving sends the whole set of details
// (UpdateVisitSummary overwrites them all).
@Component({
  selector: 'app-visit-summary-editor',
  imports: [Card, DateSelect, FormsModule, SegmentedControl, Toggle, TranslatePipe],
  templateUrl: './visit-summary-editor.html',
})
export class VisitSummaryEditor {
  private readonly visitSummaries = inject(VisitSummariesService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly summary = input<VisitSummary | null>(null);

  readonly saved = output<VisitSummary>();
  readonly cancelled = output();

  private readonly today = todayIsoDate();

  protected readonly title = linkedSignal(() => this.summary()?.title ?? '');
  protected readonly visitDate = linkedSignal(() => this.summary()?.visitDate ?? '');
  protected readonly preset = linkedSignal<Preset>(() => (this.summary() ? 'custom' : 'weeks4'));
  protected readonly from = linkedSignal(
    () => this.summary()?.from ?? addDaysIso(this.today, -(PRESET_DAYS.weeks4 - 1)),
  );
  protected readonly to = linkedSignal(() => this.summary()?.to ?? this.today);
  protected readonly sections = linkedSignal<readonly VisitSummarySection[]>(
    () => this.summary()?.sections ?? VISIT_SUMMARY_SECTIONS,
  );
  protected readonly note = linkedSignal(() => this.summary()?.note ?? '');

  protected readonly allSections = VISIT_SUMMARY_SECTIONS;
  protected readonly maxTitleLength = MAX_TITLE_LENGTH;
  protected readonly maxNoteLength = MAX_NOTE_LENGTH;
  protected readonly presetOptions = computed((): SegmentedControlOption<Preset>[] => [
    { value: 'weeks4', label: this.translation.translate('visitSummaries.editor.presets.weeks4') },
    {
      value: 'months3',
      label: this.translation.translate('visitSummaries.editor.presets.months3'),
    },
    { value: 'custom', label: this.translation.translate('visitSummaries.editor.presets.custom') },
  ]);

  protected readonly saving = createAction();
  protected readonly editing = computed(() => this.summary() !== null);
  protected readonly titleId = `visit-summary-title-${Math.random().toString(36).slice(2)}`;
  protected readonly noteId = `${this.titleId}-note`;

  protected choosePreset(preset: Preset): void {
    this.preset.set(preset);

    if (preset !== 'custom') {
      this.to.set(this.today);
      this.from.set(addDaysIso(this.today, -(PRESET_DAYS[preset] - 1)));
    }
  }

  protected setFrom(date: string): void {
    this.preset.set('custom');
    this.from.set(date);
  }

  protected setTo(date: string): void {
    this.preset.set('custom');
    this.to.set(date);
  }

  protected hasSection(section: VisitSummarySection): boolean {
    return this.sections().includes(section);
  }

  protected toggleSection(section: VisitSummarySection, on: boolean): void {
    this.sections.update((current) =>
      VISIT_SUMMARY_SECTIONS.filter((s) => (s === section ? on : current.includes(s))),
    );
  }

  protected async save(): Promise<void> {
    const problem = this.validate();

    if (problem) {
      this.saving.fail(true, problem);
      return;
    }

    const request = {
      title: this.title().trim(),
      visitDate: this.visitDate() || null,
      from: this.from(),
      to: this.to(),
      sections: [...this.sections()],
      note: this.note().trim(),
    };
    const existing = this.summary();

    await this.saving.run(
      true,
      async () => {
        const saved = existing
          ? await this.visitSummaries.update(this.childId(), existing.id, request)
          : await this.visitSummaries.create(this.childId(), request);
        this.saved.emit(saved);
      },
      'visitSummaries.editor.saveError',
    );
  }

  private validate(): string | null {
    if (this.title().trim() === '') {
      return 'visitSummaries.editor.titleRequired';
    }

    if (this.sections().length === 0) {
      return 'visitSummaries.editor.sectionsRequired';
    }

    const from = this.from();
    const to = this.to();

    if (!from || !to || to < from || addDaysIso(from, MAX_PERIOD_DAYS) <= to) {
      return 'visitSummaries.editor.periodInvalid';
    }

    return null;
  }
}
