import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { TranslationService } from './i18n/translation.service';
import { TimeOfDayPipe } from './time-of-day.pipe';

describe('TimeOfDayPipe', () => {
  function createPipe(language: 'en' | 'da' = 'en'): TimeOfDayPipe {
    const translationStub: Partial<TranslationService> = { language: signal(language).asReadonly() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: TranslationService, useValue: translationStub }] });

    return TestBed.runInInjectionContext(() => new TimeOfDayPipe());
  }

  it('returns an empty string for null, undefined, and empty string input', () => {
    const pipe = createPipe();

    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });

  it('formats an "HH:mm" wall-clock string in 12-hour English style', () => {
    const pipe = createPipe('en');

    expect(pipe.transform('14:00')).toBe('2:00 PM');
  });

  it('formats the same wall-clock string in 24-hour Danish style', () => {
    const pipe = createPipe('da');

    expect(pipe.transform('14:00')).toBe('14.00');
  });

  it('zero-pads minutes but not a single-digit English hour', () => {
    const pipe = createPipe('en');

    expect(pipe.transform('09:05')).toBe('9:05 AM');
  });

  it('renders midnight as 12 AM in English', () => {
    const pipe = createPipe('en');

    expect(pipe.transform('00:00')).toBe('12:00 AM');
  });

  it('ignores a trailing seconds component', () => {
    const pipe = createPipe('en');

    expect(pipe.transform('14:00:30')).toBe('2:00 PM');
  });

  it('re-formats when the language signal changes between calls', () => {
    const languageState = signal<'en' | 'da'>('en');
    const translationStub: Partial<TranslationService> = { language: languageState.asReadonly() };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: TranslationService, useValue: translationStub }] });
    const pipe = TestBed.runInInjectionContext(() => new TimeOfDayPipe());

    expect(pipe.transform('14:00')).toBe('2:00 PM');

    languageState.set('da');

    expect(pipe.transform('14:00')).toBe('14.00');
  });
});
