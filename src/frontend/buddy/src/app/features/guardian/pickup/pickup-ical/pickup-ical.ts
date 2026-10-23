import { DatePipe } from '@angular/common';
import { Component, inject, resource, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { PickupsService } from '../../../../core/pickups.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { IcalSubscribeLinks } from '../../../../shared/ical-subscribe-links/ical-subscribe-links';

// The guardian's own iCal feed of the drop-offs and pickups assigned to them, across all their
// children -- unlike MealplanIcal there is no child to pick (docs/backend/analysis/pickup-ical-feed.md).
@Component({
  selector: 'app-pickup-ical',
  imports: [DatePipe, TranslatePipe, IcalSubscribeLinks, Card],
  templateUrl: './pickup-ical.html',
})
export class PickupIcal {
  private readonly pickups = inject(PickupsService);

  protected readonly tokens = resource({
    loader: () => this.pickups.listIcalTokens(),
  });

  protected readonly creating = createAction();
  protected readonly revoking = createAction<string>();

  // The plaintext URL is only available right after creation, as on the server.
  protected readonly newIcalUrl = signal<string | null>(null);
  protected readonly icalCopied = signal(false);

  protected async createIcalToken(): Promise<void> {
    this.newIcalUrl.set(null);
    this.icalCopied.set(false);

    await this.creating.run(
      true,
      async () => {
        const issued = await this.pickups.createIcalToken();
        this.newIcalUrl.set(this.pickups.icalFeedUrl(issued.subscriptionPath));
        // Reloading the list also clears an earlier revoke error from its place.
        this.revoking.clearError();
        this.tokens.reload();
      },
      'pickup.ical.createError',
    );
  }

  protected async revokeIcalToken(tokenId: string): Promise<void> {
    await this.revoking.run(
      tokenId,
      async () => {
        await this.pickups.revokeIcalToken(tokenId);
        this.tokens.reload();
      },
      'pickup.ical.revokeError',
    );
  }

  protected async copyIcalUrl(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.icalCopied.set(true);
    } catch {
      this.icalCopied.set(false);
    }
  }
}
