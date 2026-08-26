import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { EmailVerifiedEvent } from './email-verified-event';
import { EmailVerifiedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('EmailVerifiedEvent', () => {
  async function setup(data: EmailVerifiedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [EmailVerifiedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(EmailVerifiedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the static title and description alongside the formatted timestamp', async () => {
    const { compiled } = await setup({ userId: 'user-1', occurredAt: '2026-01-15T09:30:00Z' });

    expect(compiled.textContent).toContain('Email verified');
    expect(compiled.textContent).toContain('The email address was verified.');
    expect(compiled.textContent).toContain('Jan 15, 2026, 9:30:00 AM');
  });

  it('renders the timestamp in the injected time zone rather than a hardcoded one', async () => {
    const { compiled } = await setup({ userId: 'user-1', occurredAt: '2026-01-15T09:30:00Z' }, 'Pacific/Kiritimati');

    // 09:30 UTC on 2026-01-15 is 23:30 the same day in UTC+14 Kiritimati.
    expect(compiled.textContent).toContain('Jan 15, 2026, 11:30:00 PM');
    expect(compiled.textContent).not.toContain('Jan 15, 2026, 9:30:00 AM');
  });
});
