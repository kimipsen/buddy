import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { ChildClockService } from './child-clock.service';
import { browserTimeZoneId, toIsoDateInTimeZone } from './date-utils';
import { UsersService } from './users.service';

describe('ChildClockService', () => {
  function setup(profileZone: string | null) {
    const zone = signal<string | null>(profileZone);
    TestBed.configureTestingModule({
      providers: [{ provide: UsersService, useValue: { profileTimeZoneId: zone.asReadonly() } }],
    });
    return { clock: TestBed.inject(ChildClockService), zone };
  }

  // 23:30 in New York on the 12th is already 03:30 on the 13th in UTC and 05:30 in Copenhagen.
  const lateEveningInNewYork = Date.parse('2026-10-13T03:30:00Z');

  it("uses the child's profile zone for today, whatever the device's zone", () => {
    const { clock } = setup('America/New_York');

    expect(clock.timeZone()).toBe('America/New_York');
    expect(clock.today(lateEveningInNewYork)).toBe('2026-10-12');
    expect(clock.tomorrow(lateEveningInNewYork)).toBe('2026-10-13');
    expect(clock.minutesOfDay(lateEveningInNewYork)).toBe(23 * 60 + 30);
  });

  it('crosses midnight on the profile zone’s clock, not the device’s', () => {
    const { clock } = setup('Europe/Copenhagen');

    // 23:59 and 00:01 in Copenhagen (CEST, UTC+2) are both on the 13th in UTC.
    expect(clock.today(Date.parse('2026-10-12T21:59:00Z'))).toBe('2026-10-12');
    expect(clock.today(Date.parse('2026-10-12T22:01:00Z'))).toBe('2026-10-13');
    expect(clock.minutesOfDay(Date.parse('2026-10-12T22:01:00Z'))).toBe(1);
  });

  it("falls back to the device's zone until the signed-in user has loaded", () => {
    const { clock, zone } = setup(null);

    expect(clock.timeZone()).toBe(browserTimeZoneId());
    expect(clock.today(lateEveningInNewYork)).toBe(
      toIsoDateInTimeZone(new Date(lateEveningInNewYork), browserTimeZoneId()),
    );

    zone.set('Pacific/Kiritimati');
    expect(clock.today(lateEveningInNewYork)).toBe('2026-10-13');
  });
});
