import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealplanIcalTokenSummary, MealplansService } from '../../../../core/mealplans.service';

@Component({
  selector: 'app-mealplan-ical',
  imports: [DatePipe, TranslatePipe],
  templateUrl: './mealplan-ical.html'
})
export class MealplanIcal {
  private readonly mealplans = inject(MealplansService);

  readonly childId = input.required<string>();

  protected readonly tokens = signal<MealplanIcalTokenSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly revokingTokenId = signal<string | null>(null);

  // The plaintext URL is only ever available right after creation -- once a new token is issued
  // or this component's childId changes, it's gone from the client just like it's gone from the
  // server.
  protected readonly newIcalUrl = signal<string | null>(null);
  protected readonly icalCopied = signal(false);

  constructor() {
    effect(() => {
      void this.load(this.childId());
    });
  }

  protected async createIcalToken(): Promise<void> {
    this.creating.set(true);
    this.createError.set(null);
    this.newIcalUrl.set(null);
    this.icalCopied.set(false);

    try {
      const issued = await this.mealplans.createIcalToken(this.childId());
      this.newIcalUrl.set(this.mealplans.icalFeedUrl(issued.subscriptionPath));
      await this.loadTokens(this.childId());
    } catch {
      this.createError.set('mealplan.ical.createError');
    } finally {
      this.creating.set(false);
    }
  }

  protected async revokeIcalToken(tokenId: string): Promise<void> {
    this.revokingTokenId.set(tokenId);
    this.error.set(null);

    try {
      await this.mealplans.revokeIcalToken(this.childId(), tokenId);
      await this.loadTokens(this.childId());
    } catch {
      this.error.set('mealplan.ical.revokeError');
    } finally {
      this.revokingTokenId.set(null);
    }
  }

  protected async copyIcalUrl(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.icalCopied.set(true);
    } catch {
      this.icalCopied.set(false);
    }
  }

  private async load(childId: string): Promise<void> {
    this.newIcalUrl.set(null);
    this.icalCopied.set(false);
    await this.loadTokens(childId);
  }

  private async loadTokens(childId: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.tokens.set(await this.mealplans.listIcalTokens(childId));
    } catch {
      this.error.set('mealplan.ical.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
