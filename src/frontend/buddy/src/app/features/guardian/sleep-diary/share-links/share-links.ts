import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, linkedSignal, resource, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { SleepDiaryService } from '../../../../core/sleep-diary.service';
import { createAction } from '../../../../shared/action-state/action-state';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Card } from '../../../../shared/card/card';

// Days until the link expires; 0 means no expiry. 30 is the default the design doc suggests -- a
// link handed to a clinician is usually for one consultation.
type ExpiryDays = 0 | 7 | 30 | 90;
const DEFAULT_EXPIRY_DAYS: ExpiryDays = 30;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

export const SHARED_SLEEP_DIARY_PATH = '/shared/sleep-diary/';

// Share links for a clinician: a revocable, optionally expiring URL that opens the diary with no
// login (the same one-time-secret flow as the meal plan's iCal feed in mealplan-ical).
@Component({
  selector: 'app-sleep-share-links',
  imports: [DatePipe, SegmentedControl, TranslatePipe, Card],
  templateUrl: './share-links.html',
})
export class SleepShareLinks {
  private readonly sleepDiary = inject(SleepDiaryService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();

  protected readonly links = resource({
    params: () => this.childId(),
    loader: ({ params: childId }) => this.sleepDiary.listShareLinks(childId),
  });

  protected readonly expiryDays = signal<ExpiryDays>(DEFAULT_EXPIRY_DAYS);
  protected readonly expiryOptions = computed((): SegmentedControlOption<ExpiryDays>[] => [
    { value: 7, label: this.translation.translate('sleepDiary.share.expiry.days7') },
    { value: 30, label: this.translation.translate('sleepDiary.share.expiry.days30') },
    { value: 90, label: this.translation.translate('sleepDiary.share.expiry.days90') },
    { value: 0, label: this.translation.translate('sleepDiary.share.expiry.never') },
  ]);

  protected readonly creating = createAction();
  // A fresh revoke state per child, so a revoke error doesn't follow the guardian to another child.
  protected readonly revoking = computed(() => {
    this.childId();
    return createAction<string>();
  });

  // The plaintext link exists only right after creation, and is dropped on a child switch.
  protected readonly newLinkUrl = linkedSignal<string, string | null>({
    source: this.childId,
    computation: () => null,
  });
  protected readonly copied = linkedSignal({ source: this.childId, computation: () => false });

  protected async create(): Promise<void> {
    const childId = this.childId();
    const days = this.expiryDays();
    const expiresAt = days === 0 ? null : new Date(Date.now() + days * MS_PER_DAY).toISOString();

    this.newLinkUrl.set(null);
    this.copied.set(false);

    await this.creating.run(
      true,
      async () => {
        const created = await this.sleepDiary.createShareLink(childId, expiresAt);
        this.newLinkUrl.set(`${window.location.origin}${SHARED_SLEEP_DIARY_PATH}${created.token}`);
        this.revoking().clearError();
        this.links.reload();
      },
      'sleepDiary.share.createError',
    );
  }

  protected async revoke(shareLinkId: string): Promise<void> {
    await this.revoking().run(
      shareLinkId,
      async () => {
        await this.sleepDiary.revokeShareLink(this.childId(), shareLinkId);
        this.links.reload();
      },
      'sleepDiary.share.revokeError',
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
