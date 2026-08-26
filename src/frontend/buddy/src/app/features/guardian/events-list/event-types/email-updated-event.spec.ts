import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { EmailUpdatedEvent } from './email-updated-event';
import { EmailUpdatedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('EmailUpdatedEvent', () => {
  async function setup(data: EmailUpdatedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [EmailUpdatedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(EmailUpdatedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the title, the before/after email addresses, and the formatted timestamp', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: { value: 'old@buddy.test', isVerified: true },
      after: { value: 'new@buddy.test', isVerified: false },
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Email updated');
    expect(compiled.textContent).toContain('Email changed from old@buddy.test to new@buddy.test.');
    expect(compiled.textContent).toContain('Jan 15, 2026, 9:30:00 AM');
  });

  it('does not surface the isVerified flag anywhere, even when only verification status changed', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: { value: 'same@buddy.test', isVerified: true },
      after: { value: 'same@buddy.test', isVerified: false },
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Email changed from same@buddy.test to same@buddy.test.');
    expect(compiled.textContent).not.toMatch(/true|false/i);
  });
});
