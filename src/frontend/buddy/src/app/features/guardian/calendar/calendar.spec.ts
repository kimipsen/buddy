import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { CalendarsService } from '../../../core/calendars.service';
import { UsersService } from '../../../core/users.service';
import { GuardianCalendar } from './calendar';

// GuardianCalendar is a trivial shell: a back link plus <app-calendar-agenda>, no logic of its
// own. This smoke test only confirms it renders that composition -- CalendarAgenda's own behavior
// is covered by agenda.spec.ts. Its child services still need stubs here, since mounting the real
// CalendarAgenda otherwise instantiates the real (HttpClient-backed) services via DI.
describe('GuardianCalendar', () => {
  async function setup() {
    const usersStub: Partial<UsersService> = {
      timeZoneId: signal('UTC').asReadonly()
    };
    const calendarsStub: Partial<CalendarsService> = {
      listMyCalendars: vi.fn(async () => []),
      listOccurrencesInRange: vi.fn(async () => []),
      listAssignableMembers: vi.fn(async () => [])
    };

    await TestBed.configureTestingModule({
      imports: [GuardianCalendar],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: usersStub },
        { provide: CalendarsService, useValue: calendarsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianCalendar);

    return { fixture };
  }

  it('renders the agenda and a back link to the guardian home', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-calendar-agenda')).toBeTruthy();
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
  });
});
