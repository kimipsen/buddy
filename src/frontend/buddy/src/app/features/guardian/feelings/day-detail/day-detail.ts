import { Component, computed, inject, input, linkedSignal, output } from '@angular/core';

import { parseIsoDate } from '../../../../core/date-utils';
import { TAG_LABEL_KEYS, zoneInfo } from '../../../../core/feelings';
import { FeelingCheckIn, FeelingsService } from '../../../../core/feelings.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

/**
 * One day's check-ins with edit and remove. A child's self-report can be removed (a sibling grabbed
 * the iPad) but never edited, so Edit is hidden on it (Question 5).
 */
@Component({
  selector: 'app-feelings-day-detail',
  imports: [TranslatePipe, Card],
  templateUrl: './day-detail.html',
})
export class FeelingsDayDetail {
  private readonly feelings = inject(FeelingsService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly date = input.required<string>();
  readonly checkIns = input.required<readonly FeelingCheckIn[]>();

  readonly edit = output<FeelingCheckIn>();
  readonly removed = output<string>();

  protected readonly zoneInfo = zoneInfo;
  protected readonly tagKeys = TAG_LABEL_KEYS;

  // A fresh state per day and child, so an error doesn't follow the guardian elsewhere.
  protected readonly removing = linkedSignal({
    source: () => `${this.childId()}|${this.date()}`,
    computation: () => createAction<string>(),
  });

  protected readonly dayLabel = computed(() =>
    parseIsoDate(this.date()).toLocaleDateString(this.translation.language(), {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
    }),
  );
  protected readonly items = computed(() =>
    this.checkIns().filter((checkIn) => checkIn.date === this.date()),
  );
  protected readonly hasSelfReport = computed(() => this.items().some((c) => c.isSelfReport));

  protected async remove(checkIn: FeelingCheckIn): Promise<void> {
    await this.removing().run(
      checkIn.id,
      async () => {
        await this.feelings.removeCheckIn(this.childId(), checkIn.id);
        this.removed.emit(checkIn.id);
      },
      'feelings.guardian.day.removeError',
    );
  }
}
