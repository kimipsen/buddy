import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, linkedSignal, resource, untracked } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { NightDutyService } from '../../../../core/night-duty.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { IcalSubscribeLinks } from '../../../../shared/ical-subscribe-links/ical-subscribe-links';

// The guardian's own iCal subscription links to the selected child's nights, one link per child
// (docs/backend/analysis/night-duty-ical-feed.md). A copy of PickupIcal, scoped to a child.
@Component({
  selector: 'app-night-ical',
  imports: [DatePipe, TranslatePipe, IcalSubscribeLinks, Card],
  templateUrl: './night-ical.html',
})
export class NightIcal {
  private readonly nightDuty = inject(NightDutyService);

  readonly childId = input.required<string>();
  readonly childName = input.required<string>();

  protected readonly tokens = resource({
    params: () => this.childId(),
    loader: ({ params: childId }) => this.nightDuty.listIcalTokens(childId),
  });

  protected readonly creating = createAction();
  protected readonly revoking = createAction<string>();

  // A create or revoke error belongs to the child it happened for: picking another child drops it.
  private readonly clearErrorsOnChildChange = effect(() => {
    this.childId();
    untracked(() => {
      this.creating.clearError();
      this.revoking.clearError();
    });
  });

  // The plaintext URL is only available right after creation, as on the server; it belongs to the
  // child it was created for, so picking another child hides it.
  protected readonly newIcalUrl = linkedSignal<string, string | null>({
    source: () => this.childId(),
    computation: () => null,
  });
  protected readonly icalCopied = linkedSignal<string | null, boolean>({
    source: () => this.newIcalUrl(),
    computation: () => false,
  });

  protected async createIcalToken(): Promise<void> {
    this.newIcalUrl.set(null);

    await this.creating.run(
      true,
      async () => {
        const issued = await this.nightDuty.createIcalToken(this.childId());
        this.newIcalUrl.set(this.nightDuty.icalFeedUrl(issued.subscriptionPath));
        // Reloading the list also clears an earlier revoke error from its place.
        this.revoking.clearError();
        this.tokens.reload();
      },
      'nightDuty.ical.createError',
    );
  }

  protected async revokeIcalToken(tokenId: string): Promise<void> {
    await this.revoking.run(
      tokenId,
      async () => {
        await this.nightDuty.revokeIcalToken(this.childId(), tokenId);
        this.tokens.reload();
      },
      'nightDuty.ical.revokeError',
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
