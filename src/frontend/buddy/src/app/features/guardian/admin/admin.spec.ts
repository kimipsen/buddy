import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth.service';
import { CalendarsService } from '../../../core/calendars.service';
import { GroupsService } from '../../../core/groups.service';
import { GuardiansService } from '../../../core/guardians.service';
import { UserEventsPage, UserEventsService } from '../../../core/user-events.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { GuardianAdmin } from './admin';

// GuardianAdmin is a pure composition shell -- it wires together six already-covered admin
// sections (see each section's own .spec.ts for its behavior) plus a static back link, with no
// logic of its own. This spec only checks that the whole tree constructs and renders without
// throwing, and that every section is present, mirroring the other shell specs in this phase.
describe('GuardianAdmin', () => {
  const currentUser: CurrentUser = {
    id: 'guardian-1',
    email: { value: 'guardian@buddy.test', isVerified: true },
    userName: 'guardian',
    name: { givenName: 'Gina', familyName: 'G' },
    timeZoneId: 'UTC',
    language: 'en'
  };

  const eventsPage: UserEventsPage = { items: [], previousCursor: null, nextCursor: null };

  // The stubbed services are called directly rather than through HttpClient, so no PendingTasks
  // entry is registered and whenStable() would resolve immediately without waiting for them. A
  // macrotask flush drains any depth of chained awaits instead -- see docs/testing.md ("Waiting
  // for async work in component tests").
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function setup() {
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => currentUser),
      updateName: vi.fn(),
      updateTimeZone: vi.fn(),
      updateLanguage: vi.fn(),
      deleteCurrentUser: vi.fn(async () => undefined)
    };
    const authStub: Partial<AuthService> = { logout: vi.fn() };
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      listGuardianInvites: vi.fn(async () => [])
    };
    const calendarsStub: Partial<CalendarsService> = {
      listMyCalendars: vi.fn(async () => []),
      listIcalTokens: vi.fn(async () => [])
    };
    const groupsStub: Partial<GroupsService> = { listMyGroups: vi.fn(async () => []) };
    const userEventsStub: Partial<UserEventsService> = { listCurrentUserEvents: vi.fn(async () => eventsPage) };

    await TestBed.configureTestingModule({
      imports: [GuardianAdmin],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: usersStub },
        { provide: AuthService, useValue: authStub },
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: GroupsService, useValue: groupsStub },
        { provide: UserEventsService, useValue: userEventsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianAdmin);

    return { fixture };
  }

  it('renders every admin section and the back-to-dashboard link without throwing', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Manage your household.');
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();

    const selectors = [
      'app-my-profile',
      'app-manage-children',
      'app-manage-calendars',
      'app-manage-groups',
      'app-events-list',
      'app-delete-account'
    ];
    for (const selector of selectors) {
      expect(compiled.querySelector(selector)).toBeTruthy();
    }
  });
});
