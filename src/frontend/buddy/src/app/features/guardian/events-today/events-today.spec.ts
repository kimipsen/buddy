import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { UsersService } from '../../../core/users.service';
import { EventsToday } from './events-today';

describe('EventsToday', () => {
  const today = todayIsoDate();

  // kind 0 = Event, 1 = Task -- see CalendarItemKind in calendars.service.ts.
  function occurrence(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'event-1',
      kind: 0,
      title: 'Dentist',
      icon: '🦷',
      iconOverride: null,
      color: '#112233',
      startsAt: `${today}T09:00:00Z`,
      endsAt: `${today}T10:00:00Z`,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      calendarId: 'cal-1',
      calendarName: 'Home',
      ...overrides
    };
  }

  interface Stubs {
    users?: Partial<UsersService>;
    calendars?: Partial<CalendarsService>;
  }

  async function setup(stubs: Stubs = {}) {
    // UsersService isn't injected by EventsToday itself, but its template's userDate pipe injects
    // it directly (to read timeZoneId), so it still needs a DI-friendly stub here.
    const usersStub: Partial<UsersService> = {
      timeZoneId: signal('UTC').asReadonly(),
      ...stubs.users
    };
    const calendarsStub: Partial<CalendarsService> = {
      listTodayOccurrences: vi.fn(async () => []),
      ...stubs.calendars
    };

    await TestBed.configureTestingModule({
      imports: [EventsToday],
      providers: [
        { provide: UsersService, useValue: usersStub },
        { provide: CalendarsService, useValue: calendarsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(EventsToday);

    return { fixture, calendars: calendarsStub };
  }

  // loadEvents chains a single await, but per docs/testing.md a stubbed service's plain Promise
  // never registers as a PendingTask under zoneless change detection, so whenStable() alone
  // wouldn't reliably wait for it -- flush a generous number of times instead (same pattern as
  // tasks-today.spec.ts, this widget's sibling).
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  it('shows the loading spinner while events are loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the empty state once loading finishes with no events', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeFalsy();
    expect(compiled.textContent).toContain('Nothing else on the calendar today.');
  });

  it('shows the translated error message when loading events fails', async () => {
    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s events.');
    expect(compiled.textContent).not.toContain('Nothing else on the calendar today.');
  });

  it('renders an event with its icon and title', async () => {
    const dentist = occurrence({ title: 'Dentist', icon: '🦷' });
    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [dentist]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Dentist');
    expect(compiled.textContent).toContain('🦷');
  });

  it('renders a start time for a timed event', async () => {
    const timed = occurrence({ itemId: 'timed', title: 'Dentist', startsAt: `${today}T09:00:00Z` });
    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [timed]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const item = Array.from(compiled.querySelectorAll('li')).find((li) => li.textContent?.includes('Dentist'));
    // The template's ml-auto span only renders `@if (event.startsAt)`.
    const timeSpan = item?.querySelector('.ml-auto');
    expect(timeSpan?.textContent?.trim()).not.toBe('');
  });

  it('omits the time span for an event with no startsAt', async () => {
    const untimed = occurrence({ itemId: 'untimed', title: 'Field trip', startsAt: null, isAllDay: true });
    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [untimed]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const item = Array.from(compiled.querySelectorAll('li')).find((li) => li.textContent?.includes('Field trip'));
    expect(item?.querySelector('.ml-auto')).toBeFalsy();
  });

  it('shows only events, filtering out tasks from the same mixed response', async () => {
    const event = occurrence({ itemId: 'event-1', kind: 0, title: 'Dentist' });
    const task = occurrence({ itemId: 'task-1', kind: 1, title: 'Buy groceries', startsAt: null, dueAt: `${today}T17:00:00Z` });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [task, event]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Dentist');
    expect(compiled.textContent).not.toContain('Buy groceries');
  });

  it('sorts same-day events by start time, earliest first, regardless of fetch order', async () => {
    const late = occurrence({ itemId: 'late', title: 'Late meeting', startsAt: `${today}T18:00:00Z` });
    const early = occurrence({ itemId: 'early', title: 'Early meeting', startsAt: `${today}T08:00:00Z` });

    // Deliberately out of order to prove the component sorts, not just echoes fetch order.
    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [late, early]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const text = compiled.textContent ?? '';
    expect(text.indexOf('Early meeting')).toBeLessThan(text.indexOf('Late meeting'));
  });

  it('requests only today’s occurrences once on load', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    expect(calendars.listTodayOccurrences).toHaveBeenCalledTimes(1);
  });

  it('marks an event whose end time has passed as done', async () => {
    const past = occurrence({
      itemId: 'past',
      title: 'Morning meeting',
      startsAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
      endsAt: new Date(Date.now() - 60 * 60 * 1000).toISOString()
    });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [past]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const item = Array.from(compiled.querySelectorAll('li')).find((li) => li.textContent?.includes('Morning meeting'));
    expect(item?.querySelector('.line-through')?.textContent).toContain('Morning meeting');
    expect(item?.textContent).toContain('✓');
  });

  it('does not mark an upcoming event as done', async () => {
    const upcoming = occurrence({
      itemId: 'upcoming',
      title: 'Afternoon meeting',
      startsAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      endsAt: new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString()
    });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [upcoming]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const item = Array.from(compiled.querySelectorAll('li')).find((li) => li.textContent?.includes('Afternoon meeting'));
    expect(item?.querySelector('.line-through')).toBeFalsy();
  });

  it('shows a progress fill for an event that is currently in progress', async () => {
    const ongoing = occurrence({
      itemId: 'ongoing',
      title: 'Team standup',
      startsAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
      endsAt: new Date(Date.now() + 30 * 60 * 1000).toISOString()
    });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [ongoing]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const item = Array.from(compiled.querySelectorAll('li')).find((li) => li.textContent?.includes('Team standup'));
    expect(item?.style.background).toContain('linear-gradient');
    expect(item?.querySelector('.line-through')).toBeFalsy();
  });

  it('never marks an all-day event as done or in progress, regardless of the time', async () => {
    const allDay = occurrence({
      itemId: 'all-day',
      title: 'School holiday',
      isAllDay: true,
      startsAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
      endsAt: new Date(Date.now() - 60 * 60 * 1000).toISOString()
    });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [allDay]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const item = Array.from(compiled.querySelectorAll('li')).find((li) => li.textContent?.includes('School holiday'));
    expect(item?.querySelector('.line-through')).toBeFalsy();
    expect(item?.style.background).toBeFalsy();
  });
});
