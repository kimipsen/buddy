import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { UserCreatedEvent } from './user-created-event';
import { UserCreatedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('UserCreatedEvent', () => {
  async function setup(data: UserCreatedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [UserCreatedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(UserCreatedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the title and the full name plus email interpolated into the description', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      keycloakSubject: 'sub-1',
      email: { value: 'ann@buddy.test', isVerified: true },
      userName: 'auser',
      name: { givenName: 'Ann', familyName: 'A' },
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Account created');
    expect(compiled.textContent).toContain('Ann A (ann@buddy.test) joined via Keycloak.');
    expect(compiled.textContent).toContain('Jan 15, 2026, 9:30:00 AM');
  });

  it('renders correctly with a null userName, and never surfaces userId or keycloakSubject', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      keycloakSubject: 'keycloak-sub-xyz',
      email: { value: 'ann@buddy.test', isVerified: true },
      userName: null,
      name: { givenName: 'Ann', familyName: 'A' },
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Ann A (ann@buddy.test) joined via Keycloak.');
    expect(compiled.textContent).not.toContain('user-1');
    expect(compiled.textContent).not.toContain('keycloak-sub-xyz');
  });
});
