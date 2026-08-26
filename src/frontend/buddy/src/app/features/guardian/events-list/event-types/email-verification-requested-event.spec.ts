import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { EmailVerificationRequestedEvent } from './email-verification-requested-event';
import { EmailVerificationRequestedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('EmailVerificationRequestedEvent', () => {
  async function setup(data: EmailVerificationRequestedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [EmailVerificationRequestedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(EmailVerificationRequestedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the title and the formatted expiry timestamp interpolated into the description', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      expiresAt: '2026-01-22T18:00:00Z',
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Email verification requested');
    expect(compiled.textContent).toContain('A verification link was sent, expiring Jan 22, 2026, 6:00:00 PM.');
  });

  it('formats occurredAt and expiresAt independently, without swapping the two distinct instants', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      expiresAt: '2026-01-22T18:00:00Z',
      occurredAt: '2026-01-15T09:30:00Z'
    });

    // The description sentence carries expiresAt; the trailing timestamp line carries occurredAt.
    const description = compiled.querySelectorAll('p')[1]?.textContent ?? '';
    const timestampLine = compiled.querySelectorAll('p')[2]?.textContent ?? '';

    expect(description).toContain('Jan 22, 2026, 6:00:00 PM');
    expect(description).not.toContain('Jan 15, 2026, 9:30:00 AM');
    expect(timestampLine).toContain('Jan 15, 2026, 9:30:00 AM');
    expect(timestampLine).not.toContain('Jan 22, 2026, 6:00:00 PM');
  });
});
