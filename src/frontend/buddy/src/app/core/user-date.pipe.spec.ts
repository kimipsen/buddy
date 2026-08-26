import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { TranslationService } from './i18n/translation.service';
import { UserDatePipe } from './user-date.pipe';
import { UsersService } from './users.service';

describe('UserDatePipe', () => {
  function createPipe(timeZoneId: string, language: 'en' | 'da' = 'en'): UserDatePipe {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };
    const translationStub: Partial<TranslationService> = { language: signal(language).asReadonly() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: UsersService, useValue: usersStub },
        { provide: TranslationService, useValue: translationStub }
      ]
    });

    return TestBed.runInInjectionContext(() => new UserDatePipe());
  }

  const instant = '2024-03-05T14:30:00Z';

  it('returns an empty string for null, undefined, and empty string input', () => {
    const pipe = createPipe('UTC');

    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });

  it('formats an ISO string with the default "medium" format in the user time zone and language', () => {
    const pipe = createPipe('Europe/Copenhagen', 'en');

    expect(pipe.transform(instant)).toBe('Mar 5, 2024, 3:30:00 PM');
  });

  it('formats using the Danish locale when that is the current language', () => {
    const pipe = createPipe('Europe/Copenhagen', 'da');

    expect(pipe.transform(instant)).toBe('5. mar. 2024, 15.30.00');
  });

  it('formats "shortTime" as just a short time-of-day', () => {
    const pipe = createPipe('Europe/Copenhagen', 'en');

    expect(pipe.transform(instant, 'shortTime')).toBe('3:30 PM');
  });

  it('renders the same instant differently depending on the injected time zone', () => {
    const utcPipe = createPipe('UTC', 'en');
    const cphPipe = createPipe('Europe/Copenhagen', 'en');

    expect(utcPipe.transform(instant, 'shortTime')).toBe('2:30 PM');
    expect(cphPipe.transform(instant, 'shortTime')).toBe('3:30 PM');
  });

  it('accepts a Date instance directly', () => {
    const pipe = createPipe('UTC', 'en');

    expect(pipe.transform(new Date(instant), 'shortTime')).toBe('2:30 PM');
  });

  it('accepts a numeric epoch timestamp', () => {
    const pipe = createPipe('UTC', 'en');

    expect(pipe.transform(new Date(instant).getTime(), 'shortTime')).toBe('2:30 PM');
  });

  it('crosses a calendar day boundary when the time zone offset pushes the instant into the next day', () => {
    const pipe = createPipe('Pacific/Kiritimati', 'en');

    // 23:30 UTC on 2024-06-15 is already 2024-06-16, 13:30 in UTC+14 Kiritimati.
    expect(pipe.transform('2024-06-15T23:30:00Z')).toBe('Jun 16, 2024, 1:30:00 PM');
  });
});
