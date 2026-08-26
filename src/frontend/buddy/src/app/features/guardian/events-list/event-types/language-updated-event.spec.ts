import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { LanguageUpdatedEvent } from './language-updated-event';
import { LanguageUpdatedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('LanguageUpdatedEvent', () => {
  async function setup(data: LanguageUpdatedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [LanguageUpdatedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(LanguageUpdatedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the title and translates known language codes to their display names', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: 'en',
      after: 'da',
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Language updated');
    expect(compiled.textContent).toContain('Language changed from English to Dansk.');
    expect(compiled.textContent).toContain('Jan 15, 2026, 9:30:00 AM');
  });

  it('falls back to the raw language code when it is not a recognized language', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: 'da',
      after: 'fr',
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Language changed from Dansk to fr.');
  });
});
