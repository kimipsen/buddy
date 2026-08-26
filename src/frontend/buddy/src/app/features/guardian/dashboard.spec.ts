import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { CalendarsService } from '../../core/calendars.service';
import { GuardiansService } from '../../core/guardians.service';
import { MealplansService } from '../../core/mealplans.service';
import { MedicinesService } from '../../core/medicines.service';
import { PickupsService } from '../../core/pickups.service';
import { CurrentUser, UsersService } from '../../core/users.service';
import { GuardianDashboard } from './dashboard';

// GuardianDashboard is a pure composition shell -- it wires together six already-covered "today"
// widgets (see each widget's own .spec.ts for its behavior) with no logic of its own. This spec
// only checks that the whole tree constructs and renders without throwing, and that every widget
// is present, mirroring the other shell specs in this phase.
describe('GuardianDashboard', () => {
  const currentUser: CurrentUser = {
    id: 'guardian-1',
    email: { value: 'guardian@buddy.test', isVerified: true },
    userName: 'guardian',
    name: { givenName: 'Gina', familyName: 'G' },
    timeZoneId: 'UTC',
    language: 'en'
  };

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
      timeZoneId: signal(currentUser.timeZoneId).asReadonly()
    };
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      listChildGuardians: vi.fn(async () => [])
    };
    const calendarsStub: Partial<CalendarsService> = {
      listTodayOccurrences: vi.fn(async () => []),
      listAssignableMembers: vi.fn(async () => []),
      setTaskCompletion: vi.fn()
    };
    const mealplansStub: Partial<MealplansService> = { listMealPlan: vi.fn(async () => []) };
    const medicinesStub: Partial<MedicinesService> = { listDoses: vi.fn(async () => []), setDoseStatus: vi.fn() };
    const pickupsStub: Partial<PickupsService> = { listSchedule: vi.fn(async () => []) };

    await TestBed.configureTestingModule({
      imports: [GuardianDashboard],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: usersStub },
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: MealplansService, useValue: mealplansStub },
        { provide: MedicinesService, useValue: medicinesStub },
        { provide: PickupsService, useValue: pickupsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianDashboard);

    return { fixture };
  }

  it('renders every today widget and the greeting without throwing', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Good to see you.');

    const selectors = [
      'app-children-overview',
      'app-mealplan-today',
      'app-tasks-today',
      'app-events-today',
      'app-doses-today',
      'app-pickup-today'
    ];
    for (const selector of selectors) {
      expect(compiled.querySelector(selector)).toBeTruthy();
    }
  });
});
