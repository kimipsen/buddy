import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  SHARED_VISIT_SUMMARY_PATH,
  VisitSummariesService,
  VisitSummary,
} from '../../../../core/visit-summaries.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';

// Days until the link expires; 0 means no expiry. 30 by default, as the sleep diary's links: a link
// handed to a clinic is usually for one consultation.
type ExpiryDays = 0 | 7 | 30 | 90;
const DEFAULT_EXPIRY_DAYS: ExpiryDays = 30;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

// The doctor's links for one summary, the sleep diary's share flow (SleepShareLinks) for a summary:
// the live links come with the summary, and `changed` asks the page to reload it.
@Component({
  selector: 'app-visit-summary-share-links',
  imports: [Card, DatePipe, SegmentedControl, TranslatePipe],
  templateUrl: './visit-summary-share-links.html',
})
export class VisitSummaryShareLinks {
  private readonly visitSummaries = inject(VisitSummariesService);
  private readonly translation = inject(TranslationService);

  readonly summary = input.required<VisitSummary>();
  readonly changed = output();

  protected readonly expiryDays = signal<ExpiryDays>(DEFAULT_EXPIRY_DAYS);
  protected readonly expiryOptions = computed((): SegmentedControlOption<ExpiryDays>[] => [
    { value: 7, label: this.translation.translate('visitSummaries.share.expiry.days7') },
    { value: 30, label: this.translation.translate('visitSummaries.share.expiry.days30') },
    { value: 90, label: this.translation.translate('visitSummaries.share.expiry.days90') },
    { value: 0, label: this.translation.translate('visitSummaries.share.expiry.never') },
  ]);

  protected readonly creating = createAction();
  protected readonly revoking = createAction<string>();

  // The plaintext link exists only right after creation.
  protected readonly newLinkUrl = signal<string | null>(null);
  protected readonly copied = signal(false);

  protected async create(): Promise<void> {
    const { childId, id } = this.summary();
    const days = this.expiryDays();
    const expiresAt = days === 0 ? null : new Date(Date.now() + days * MS_PER_DAY).toISOString();

    this.newLinkUrl.set(null);
    this.copied.set(false);

    await this.creating.run(
      true,
      async () => {
        const created = await this.visitSummaries.createShareLink(childId, id, expiresAt);
        this.newLinkUrl.set(
          `${window.location.origin}${SHARED_VISIT_SUMMARY_PATH}${created.token}`,
        );
        this.revoking.clearError();
        this.changed.emit();
      },
      'visitSummaries.share.createError',
    );
  }

  protected async revoke(linkId: string): Promise<void> {
    const { childId, id } = this.summary();

    await this.revoking.run(
      linkId,
      async () => {
        await this.visitSummaries.revokeShareLink(childId, id, linkId);
        this.changed.emit();
      },
      'visitSummaries.share.revokeError',
    );
  }

  protected async copy(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(true);
    } catch {
      this.copied.set(false);
    }
  }
}
