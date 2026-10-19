import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { provideFeatures } from '../../testing/features-fixture';
import { nestOccurrence } from '../../testing/occurrence-fixture';
import { CalendarOccurrence, CalendarsService } from './calendars.service';
import { ChildWarningFeed, WARNING_REFRESH_MS } from './child-warning-feed.service';
import { FeatureName } from './features.service';
import { PickupOccurrence, PickupsService } from './pickups.service';
import { MyTransitionWarnings } from './transition-warnings';
import { TransitionWarningsService } from './transition-warnings.service';
import { UsersService } from './users.service';

describe('ChildWarningFeed', () => {
  // 16:20 in Copenhagen on the 13th; the child's profile zone.
  const NOW = Date.parse('2026-10-13T14:20:00Z');
  const ZONE = 'Europe/Copenhagen';

  const ON: MyTransitionWarnings = {
    enabled: true,
    leadMinutes: [15, 5, 1],
    kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
    quietHours: null,
    overrides: [],
  };

  const football = nestOccurrence<CalendarOccurrence>({
    itemId: 'football',
    kind: 'Event',
    title: 'Football',
    icon: '⚽',
    iconOverride: null,
    color: '#0f0',
    startsAt: '2026-10-13T14:30:00Z',
    endsAt: '2026-10-13T15:30:00Z',
    isAllDay: false,
    isCompleted: false,
    isSkipped: false,
    isRepeating: false,
    createdBy: 'g',
    lastModifiedBy: 'g',
    assignedTo: null,
    calendarId: 'cal',
    calendarName: 'Family',
  });

  const pickup: PickupOccurrence = {
    date: '2026-10-13',
    slot: 'PickUp',
    assignee: { kind: 1 } as PickupOccurrence['assignee'],
    time: '16:30:00',
    notes: '',
    assignedBy: 'g',
  };

  function setup(settings: MyTransitionWarnings | Error, disabled: FeatureName[] = []) {
    const api = {
      getMine: vi.fn(async () => {
        if (settings instanceof Error) {
          throw settings;
        }
        return settings;
      }),
    };
    const calendars = {
      listTodayOccurrences: vi.fn(async () => [football]),
      listOccurrencesInRange: vi.fn(async () => [football]),
    };
    const pickups = { listSchedule: vi.fn(async () => [pickup]) };
    TestBed.configureTestingModule({
      providers: [
        provideFeatures(disabled),
        { provide: TransitionWarningsService, useValue: api },
        { provide: CalendarsService, useValue: calendars },
        { provide: PickupsService, useValue: pickups },
        {
          provide: UsersService,
          useValue: {
            profileTimeZoneId: signal<string | null>(ZONE).asReadonly(),
            ensureCurrentUser: vi.fn(async () => ({ id: 'child-1' })),
          },
        },
      ],
    });
    return { feed: TestBed.inject(ChildWarningFeed), api, calendars, pickups };
  }

  it('stops at the settings while warnings are off: no occurrences, no pickups, no warnings', async () => {
    const { feed, api, calendars, pickups } = setup({ ...ON, enabled: false });

    await feed.refresh(false, NOW);

    expect(api.getMine).toHaveBeenCalledTimes(1);
    expect(calendars.listTodayOccurrences).not.toHaveBeenCalled();
    expect(calendars.listOccurrencesInRange).not.toHaveBeenCalled();
    expect(pickups.listSchedule).not.toHaveBeenCalled();
    expect(feed.active()).toBe(false);
    expect(feed.warningsAt(NOW)).toEqual([]);
  });

  it('asks again for settings that are off only every 10 minutes, not on every check', async () => {
    const { feed, api } = setup({ ...ON, enabled: false });

    await feed.refresh(false, NOW);
    await feed.refresh(false, NOW + 60_000);
    await feed.refresh(false, NOW + WARNING_REFRESH_MS - 1);
    expect(api.getMine).toHaveBeenCalledTimes(1);

    await feed.refresh(false, NOW + WARNING_REFRESH_MS);
    expect(api.getMine).toHaveBeenCalledTimes(2);
  });

  it('does not force a reload unless asked to', async () => {
    const { feed, api } = setup(ON);

    await feed.refresh(false, NOW);
    await feed.refresh(undefined, NOW + 60_000);

    expect(api.getMine).toHaveBeenCalledTimes(1);
  });

  it('reloads as soon as the day changes, even within 10 minutes', async () => {
    const { feed, api } = setup(ON);

    // 23:58 and 00:01 in Copenhagen.
    await feed.refresh(false, Date.parse('2026-10-13T21:58:00Z'));
    await feed.refresh(false, Date.parse('2026-10-13T22:01:00Z'));

    expect(api.getMine).toHaveBeenCalledTimes(2);
  });

  it("loads the child's today and computes the warnings due", async () => {
    const { feed, calendars, pickups } = setup(ON);

    await feed.refresh(false, NOW);

    expect(calendars.listTodayOccurrences).toHaveBeenCalledWith('2026-10-13');
    expect(pickups.listSchedule).toHaveBeenCalledWith('child-1', '2026-10-13', '2026-10-13');
    expect(feed.active()).toBe(true);
    // Both start at 16:30; same-time warnings keep a stable order.
    expect(feed.warningsAt(NOW).map((w) => w.key)).toEqual([
      'pickup:PickUp:2026-10-13:15',
      'item:football:2026-10-13:15',
    ]);
  });

  it('uses the profile zone for today even late in the evening on a UTC device', async () => {
    const { feed, calendars } = setup(ON);
    // 01:30 on the 14th in Copenhagen is still 23:30 on the 13th in UTC.
    await feed.refresh(false, Date.parse('2026-10-13T23:30:00Z'));

    expect(calendars.listTodayOccurrences).toHaveBeenCalledWith('2026-10-14');
  });

  it('hides a dismissed stage but shows the next one', async () => {
    const { feed } = setup(ON);
    await feed.refresh(false, NOW);

    feed.dismiss('item:football:2026-10-13:15');

    expect(feed.warningsAt(NOW).map((w) => w.key)).toEqual(['pickup:PickUp:2026-10-13:15']);
    expect(feed.warningsAt(NOW + 6 * 60_000).map((w) => w.key)).toContain(
      'item:football:2026-10-13:5',
    );
  });

  it('refetches only once the data is stale, or when forced, and then asks the server again', async () => {
    const { feed, api, calendars } = setup(ON);
    await feed.refresh(false, NOW);

    await feed.refresh(false, NOW + WARNING_REFRESH_MS - 1);
    expect(api.getMine).toHaveBeenCalledTimes(1);

    await feed.refresh(false, NOW + WARNING_REFRESH_MS);
    expect(api.getMine).toHaveBeenCalledTimes(2);
    // The same-day cache only knows this device's writes, so later loads skip it.
    expect(calendars.listOccurrencesInRange).toHaveBeenCalledWith('2026-10-13', '2026-10-13');

    await feed.refresh(true, NOW + WARNING_REFRESH_MS + 1);
    expect(api.getMine).toHaveBeenCalledTimes(3);
  });

  it('shows nothing when the settings fail to load', async () => {
    const { feed, calendars } = setup(new Error('offline'));

    await feed.refresh(false, NOW);

    expect(feed.active()).toBe(false);
    expect(calendars.listTodayOccurrences).not.toHaveBeenCalled();
  });

  it('asks for nothing while the feature is off', async () => {
    const { feed, api } = setup(ON, ['transitionWarnings']);

    await feed.refresh(true, NOW);

    expect(api.getMine).not.toHaveBeenCalled();
    expect(feed.active()).toBe(false);
  });

  it('leaves pickups out while pickups are off', async () => {
    const { feed, pickups } = setup(ON, ['pickups']);

    await feed.refresh(false, NOW);

    expect(pickups.listSchedule).not.toHaveBeenCalled();
    expect(feed.warningsAt(NOW).map((w) => w.kind)).toEqual(['Event']);
  });
});
