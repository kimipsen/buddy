import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { TimeZoneUpdatedEvent } from './timezone-updated-event';
import { TimeZoneUpdatedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('TimeZoneUpdatedEvent', () => {
  async function setup(data: TimeZoneUpdatedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [TimeZoneUpdatedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(TimeZoneUpdatedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the title and the before/after time zone ids interpolated into the description', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: 'UTC',
      after: 'Europe/Copenhagen',
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Time zone updated');
    expect(compiled.textContent).toContain('Time zone changed from UTC to Europe/Copenhagen.');
    expect(compiled.textContent).toContain('Jan 15, 2026, 9:30:00 AM');
  });

  it('renders IANA zone ids verbatim, without humanizing the slash-separated segments', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: 'America/New_York',
      after: 'Pacific/Kiritimati',
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Time zone changed from America/New_York to Pacific/Kiritimati.');
  });
});
