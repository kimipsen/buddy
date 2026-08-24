import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarSummary, CalendarsService } from '../../../../core/calendars.service';
import { browserTimeZoneId, listTimeZoneIds } from '../../../../core/date-utils';

const ROLE_LABELS: Record<number, string> = {
  0: 'Owner',
  1: 'Contributor',
  2: 'Viewer'
};

@Component({
  selector: 'app-manage-calendars',
  imports: [FormsModule],
  templateUrl: './manage-calendars.html'
})
export class ManageCalendars implements OnInit {
  private readonly calendars = inject(CalendarsService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly timeZoneIds = listTimeZoneIds();

  protected readonly items = signal<CalendarSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newCalendarName = signal('');
  protected readonly newCalendarTimeZoneId = signal(browserTimeZoneId());
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadCalendars();
  }

  protected async createCalendar(): Promise<void> {
    const name = this.newCalendarName().trim();
    const timeZoneId = this.newCalendarTimeZoneId().trim();

    if (!name || !timeZoneId) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.calendars.createCalendar({ name, timeZoneId });
      this.newCalendarName.set('');
      await this.loadCalendars();
    } catch {
      this.createError.set('Unable to create the calendar.');
    } finally {
      this.creating.set(false);
    }
  }

  private async loadCalendars(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.items.set(await this.calendars.listMyCalendars());
    } catch {
      this.error.set('Unable to load calendars.');
    } finally {
      this.loading.set(false);
    }
  }
}
