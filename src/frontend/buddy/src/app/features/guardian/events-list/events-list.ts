import { Component, inject, OnInit, signal } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UserEventItem, UserEventsService } from '../../../core/user-events.service';
import { EmailUpdatedEvent } from './event-types/email-updated-event';
import { EmailVerificationRequestedEvent } from './event-types/email-verification-requested-event';
import { EmailVerifiedEvent } from './event-types/email-verified-event';
import { GroupInvitationSentEvent } from './event-types/group-invitation-sent-event';
import { GroupMembershipJoinedEvent } from './event-types/group-membership-joined-event';
import { LanguageUpdatedEvent } from './event-types/language-updated-event';
import { NameUpdatedEvent } from './event-types/name-updated-event';
import { TimeZoneUpdatedEvent } from './event-types/timezone-updated-event';
import { UnknownEvent } from './event-types/unknown-event';
import { UserCreatedEvent } from './event-types/user-created-event';
import { UserDeletedEvent } from './event-types/user-deleted-event';

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
    GroupInvitationSentEvent,
    GroupMembershipJoinedEvent,
    UnknownEvent,
    TranslatePipe
  ],
  templateUrl: './events-list.html'
})
export class EventsList implements OnInit {
  private readonly userEvents = inject(UserEventsService);

  // Cursor used to fetch each page already visited, keyed by page index (page 0 has no cursor).
  private readonly pageCursors: (string | null)[] = [null];
  private currentPageIndex = 0;

  protected readonly events = signal<UserEventItem[]>([]);
  protected readonly eventsLoading = signal(true);
  protected readonly eventsError = signal<string | null>(null);
  protected readonly hasPreviousPage = signal(false);
  protected readonly hasNextPage = signal(false);

  ngOnInit(): void {
    void this.loadPage(0);
  }

  protected previousPage(): void {
    void this.loadPage(this.currentPageIndex - 1);
  }

  protected nextPage(): void {
    void this.loadPage(this.currentPageIndex + 1);
  }

  private async loadPage(pageIndex: number): Promise<void> {
    this.eventsLoading.set(true);
    this.eventsError.set(null);

    try {
      const page = await this.userEvents.listCurrentUserEvents(this.pageCursors[pageIndex] ?? null, EVENTS_PAGE_SIZE);

      this.currentPageIndex = pageIndex;
      this.pageCursors[pageIndex + 1] = page.nextCursor;
      this.events.set(page.items);
      this.hasPreviousPage.set(pageIndex > 0);
      this.hasNextPage.set(page.nextCursor !== null);
    } catch {
      this.eventsError.set('events.list.loadError');
    } finally {
      this.eventsLoading.set(false);
    }
  }
}
