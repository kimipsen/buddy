import { Component, OnDestroy, OnInit, computed, inject, resource, signal } from '@angular/core';

import {
  CalendarItemKind,
  CalendarOccurrence,
  CalendarsService,
} from '../../../core/calendars.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { compareOccurrences } from '../../../core/task-run';
import { UserDatePipe } from '../../../core/user-date.pipe';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { Card } from '../../../shared/card/card';

const EVENT_KIND = 'Event' satisfies CalendarItemKind;

// How often the ongoing-event progress fill and past/done state are recomputed -- same cadence
// as the child dashboard's equivalent (see ChildHome.NOW_REFRESH_INTERVAL_MS).
const NOW_REFRESH_INTERVAL_MS = 60_000;

export interface EventView extends CalendarOccurrence {
  isPast: boolean;
  isOngoing: boolean;
  progressPercent: number;
}

@Component({
  selector: 'app-events-today',
  imports: [UserDatePipe, TranslatePipe, LoadingSpinner, Card],
  templateUrl: './events-today.html',
})
export class EventsToday implements OnInit, OnDestroy {
  private readonly calendars = inject(CalendarsService);

  protected readonly events = resource({ loader: () => this.loadEvents() });

  // Ticks on an interval (rather than reading Date.now() directly in the template) so the
  // ongoing-event progress fill and past/done state actually update while the dashboard sits
  // open, instead of only reflecting "now" at the moment the page loaded.
  private readonly now = signal(Date.now());
  private nowIntervalId: ReturnType<typeof setInterval> | undefined;

  protected readonly eventsView = computed<EventView[]>(() => {
    const nowMs = this.now();
    const events = this.events.hasValue() ? this.events.value() : [];
    return events.map((event) => ({ ...event, ...this.eventProgress(event, nowMs) }));
  });

  ngOnInit(): void {
    this.nowIntervalId = setInterval(() => this.now.set(Date.now()), NOW_REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    clearInterval(this.nowIntervalId);
  }

  // A gradient rather than a separate overlay element -- the card's own background fills in from
  // the left as the event progresses, so it reads as darkening in place like a progress bar.
  protected eventProgressBackground(progressPercent: number): string {
    const clamped = Math.min(100, Math.max(0, progressPercent));
    return `linear-gradient(to right, rgb(203 213 225) ${clamped}%, transparent ${clamped}%)`;
  }

  // All-day events have no time of day to measure against, so they never read as past or
  // ongoing here -- they stay "current" for the whole day, same as their allDay badge implies.
  private eventProgress(
    event: CalendarOccurrence,
    nowMs: number,
  ): { isPast: boolean; isOngoing: boolean; progressPercent: number } {
    const { timing } = event;

    if (event.isAllDay || timing.kind !== 0) {
      return { isPast: false, isOngoing: false, progressPercent: 0 };
    }

    const startMs = new Date(timing.startsAt).getTime();
    const endMs = new Date(timing.endsAt).getTime();

    if (nowMs >= endMs) {
      return { isPast: true, isOngoing: false, progressPercent: 100 };
    }

    if (nowMs < startMs) {
      return { isPast: false, isOngoing: false, progressPercent: 0 };
    }

    const progressPercent = endMs > startMs ? ((nowMs - startMs) / (endMs - startMs)) * 100 : 100;
    return { isPast: false, isOngoing: true, progressPercent };
  }

  private async loadEvents(): Promise<CalendarOccurrence[]> {
    const occurrences = await this.calendars.listTodayOccurrences();

    return occurrences
      .filter((occurrence) => occurrence.kind === EVENT_KIND)
      .sort(compareOccurrences);
  }
}
