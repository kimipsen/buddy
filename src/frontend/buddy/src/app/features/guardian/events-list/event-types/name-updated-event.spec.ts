import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { NameUpdatedEvent } from './name-updated-event';
import { NameUpdatedData } from './user-event.model';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/events.ts rather than raw translation keys.
describe('NameUpdatedEvent', () => {
  async function setup(data: NameUpdatedData, timeZoneId = 'UTC') {
    const usersStub: Partial<UsersService> = { timeZoneId: signal(timeZoneId).asReadonly() };

    await TestBed.configureTestingModule({
      imports: [NameUpdatedEvent],
      providers: [{ provide: UsersService, useValue: usersStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(NameUpdatedEvent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the title and the before/after full names joined from given and family name', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: { givenName: 'Ann', familyName: 'A' },
      after: { givenName: 'Anna', familyName: 'A' },
      occurredAt: '2026-01-15T09:30:00Z'
    });

    expect(compiled.textContent).toContain('Name updated');
    expect(compiled.textContent).toContain('Name changed from Ann A to Anna A.');
    expect(compiled.textContent).toContain('Jan 15, 2026, 9:30:00 AM');
  });

  it('leaves a double space in the sentence when a name has an empty family name', async () => {
    const { compiled } = await setup({
      userId: 'user-1',
      before: { givenName: 'Ann', familyName: '' },
      after: { givenName: 'Anna', familyName: 'A' },
      occurredAt: '2026-01-15T09:30:00Z'
    });

    // givenName + ' ' + familyName with an empty familyName leaves a trailing space on "Ann ",
    // which collides with the template's own literal space before "to" -- pinning this exact
    // (slightly awkward) rendering rather than a normalized single space.
    expect(compiled.textContent).toContain('Name changed from Ann  to Anna A.');
  });
});
