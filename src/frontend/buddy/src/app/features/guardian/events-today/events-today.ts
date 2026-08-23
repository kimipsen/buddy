import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';

const EVENT_KIND = 0;

@Component({
  selector: 'app-events-today',
  imports: [DatePipe],
  templateUrl: './events-today.html'
})
export class EventsToday implements OnInit {
  private readonly calendars = inject(CalendarsService);

  protected readonly events = signal<CalendarOccurrence[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadEvents();
  }

  private async loadEvents(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const occurrences = await this.calendars.listTodayOccurrences();

      this.events.set(
        occurrences
          .filter((occurrence) => occurrence.kind === EVENT_KIND)
          .sort((a, b) => (a.startsAt ?? '').localeCompare(b.startsAt ?? ''))
      );
    } catch {
      this.error.set('Unable to load today’s events.');
    } finally {
      this.loading.set(false);
    }
  }
}
