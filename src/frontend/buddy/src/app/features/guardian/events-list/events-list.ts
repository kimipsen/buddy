import { Component, inject, resource, signal } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UserEventsService } from '../../../core/user-events.service';
import { EmailUpdatedEvent } from './event-types/email-updated-event';
import { EmailVerificationRequestedEvent } from './event-types/email-verification-requested-event';
import { EmailVerifiedEvent } from './event-types/email-verified-event';
import { LanguageUpdatedEvent } from './event-types/language-updated-event';
import { NameUpdatedEvent } from './event-types/name-updated-event';
import { TimeZoneUpdatedEvent } from './event-types/timezone-updated-event';
import { UnknownEvent } from './event-types/unknown-event';
import { UserCreatedEvent } from './event-types/user-created-event';
import { UserDeletedEvent } from './event-types/user-deleted-event';
import { TypedUserEvent, toTypedUserEvent } from './event-types/user-event.model';

const EVENTS_PAGE_SIZE = 5;

@Component({
  selector: 'app-events-list',
  imports: [
    UserCreatedEvent,
    UserDeletedEvent,
    NameUpdatedEvent,
    EmailUpdatedEvent,
    EmailVerificationRequestedEvent,
    EmailVerifiedEvent,
    TimeZoneUpdatedEvent,
    LanguageUpdatedEvent,
    UnknownEvent,
    TranslatePipe,
  ],
  templateUrl: './events-list.html',
})
export class EventsList {
  private readonly userEvents = inject(UserEventsService);

  // Cursor used to fetch each page already visited, keyed by page index (page 0 has no cursor).
  private readonly pageCursors: (string | null)[] = [null];
  private readonly pageIndex = signal(0);

  protected readonly page = resource({
    params: () => this.pageIndex(),
    loader: ({ params }) => this.loadPage(params),
  });

  protected previousPage(): void {
    this.pageIndex.update((index) => index - 1);
  }

  protected nextPage(): void {
    this.pageIndex.update((index) => index + 1);
  }

  private async loadPage(
    pageIndex: number,
  ): Promise<{ events: TypedUserEvent[]; hasPreviousPage: boolean; hasNextPage: boolean }> {
    const page = await this.userEvents.listCurrentUserEvents(
      this.pageCursors[pageIndex] ?? null,
      EVENTS_PAGE_SIZE,
    );

    this.pageCursors[pageIndex + 1] = page.nextCursor;

    return {
      events: page.items.map(toTypedUserEvent),
      hasPreviousPage: pageIndex > 0,
      hasNextPage: page.nextCursor !== null,
    };
  }
}
