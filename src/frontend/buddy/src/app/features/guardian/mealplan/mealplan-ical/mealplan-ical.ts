import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, linkedSignal, resource } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealplansService } from '../../../../core/mealplans.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { IcalSubscribeLinks } from '../../../../shared/ical-subscribe-links/ical-subscribe-links';
import { Card } from '../../../../shared/card/card';

@Component({
  selector: 'app-mealplan-ical',
  imports: [DatePipe, TranslatePipe, IcalSubscribeLinks, Card],
  templateUrl: './mealplan-ical.html',
})
export class MealplanIcal {
  private readonly mealplans = inject(MealplansService);

  readonly childId = input.required<string>();

  protected readonly tokens = resource({
    params: () => this.childId(),
    loader: ({ params: childId }) => this.mealplans.listIcalTokens(childId),
  });

  protected readonly creating = createAction();
  // A fresh revoke state per child, so a revoke error doesn't follow the guardian to another child.
  protected readonly revoking = computed(() => {
    this.childId();
    return createAction<string>();
  });

  // The plaintext URL is only ever available right after creation -- once a new token is issued
  // or this component's childId changes, it's gone from the client just like it's gone from the
  // server.
  protected readonly newIcalUrl = linkedSignal<string, string | null>({
    source: this.childId,
    computation: () => null,
  });
  protected readonly icalCopied = linkedSignal({ source: this.childId, computation: () => false });

  protected async createIcalToken(): Promise<void> {
    this.newIcalUrl.set(null);
    this.icalCopied.set(false);

    await this.creating.run(
      true,
      async () => {
        const issued = await this.mealplans.createIcalToken(this.childId());
        this.newIcalUrl.set(this.mealplans.icalFeedUrl(issued.subscriptionPath));
        // Reloading the list also clears an earlier revoke error from its place.
        this.revoking().clearError();
        this.tokens.reload();
      },
      'mealplan.ical.createError',
    );
  }

  protected async revokeIcalToken(tokenId: string): Promise<void> {
    await this.revoking().run(
      tokenId,
      async () => {
        await this.mealplans.revokeIcalToken(this.childId(), tokenId);
        this.tokens.reload();
      },
      'mealplan.ical.revokeError',
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
