import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, linkedSignal, resource, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { FeelingsService } from '../../../../core/feelings.service';
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

export const SHARED_FEELINGS_PATH = '/shared/feelings/';

// Share links for a doctor: a revocable, optionally expiring URL that opens the child's check-ins
// with no login -- a copy of the sleep diary's share links (feelings-check-in.md, Question 8).
@Component({
  selector: 'app-feelings-share-links',
  imports: [DatePipe, SegmentedControl, TranslatePipe, Card],
  templateUrl: './share-links.html',
})
export class FeelingsShareLinks {
  private readonly feelings = inject(FeelingsService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();

  protected readonly links = resource({
    params: () => this.childId(),
    loader: ({ params: childId }) => this.feelings.listShareLinks(childId),
  });

  protected readonly expiryDays = signal<ExpiryDays>(DEFAULT_EXPIRY_DAYS);
  protected readonly expiryOptions = computed((): SegmentedControlOption<ExpiryDays>[] => [
    { value: 7, label: this.translation.translate('feelings.guardian.share.expiry.days7') },
    { value: 30, label: this.translation.translate('feelings.guardian.share.expiry.days30') },
    { value: 90, label: this.translation.translate('feelings.guardian.share.expiry.days90') },
    { value: 0, label: this.translation.translate('feelings.guardian.share.expiry.never') },
  ]);

  protected readonly creating = createAction();
  // A fresh revoke state per child, so a revoke error doesn't follow the guardian to another child.
  protected readonly revoking = linkedSignal({
    source: this.childId,
    computation: () => createAction<string>(),
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
        const created = await this.feelings.createShareLink(childId, expiresAt);

        // The guardian switched child while it was created: never show one child's link under
        // another's card. The link is in the list it reloads to, and can be revoked there.
        if (this.childId() === childId) {
          this.newLinkUrl.set(`${window.location.origin}${SHARED_FEELINGS_PATH}${created.token}`);
        }
        this.revoking().clearError();
        this.links.reload();
      },
      'feelings.guardian.share.createError',
    );
  }

  protected async revoke(shareLinkId: string): Promise<void> {
    await this.revoking().run(
      shareLinkId,
      async () => {
        await this.feelings.revokeShareLink(this.childId(), shareLinkId);
        this.links.reload();
      },
      'feelings.guardian.share.revokeError',
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
