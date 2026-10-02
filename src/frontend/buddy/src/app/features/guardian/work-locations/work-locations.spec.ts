import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { CurrentUser, UsersService } from '../../../core/users.service';
import { WorkLocationSchedule, WorkLocationsService } from '../../../core/work-locations.service';
import { GuardianWorkLocations } from './work-locations';

describe('GuardianWorkLocations', () => {
  const schedule: WorkLocationSchedule = {
    guardianId: 'me',
    locations: [],
    pattern: { cycleWeeks: 1, anchorMonday: '2026-09-28', days: [] },
  };

  async function setup(getSchedule = vi.fn(async () => schedule)) {
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => ({ id: 'me' }) as CurrentUser),
    };
    const workLocationsStub: Partial<WorkLocationsService> = {
      getSchedule,
      listWorkDays: vi.fn(async () => []),
    };

    await TestBed.configureTestingModule({
      imports: [GuardianWorkLocations],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: usersStub },
        { provide: WorkLocationsService, useValue: workLocationsStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianWorkLocations);
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    return { fixture, getSchedule };
  }

  it('loads the current guardian’s schedule and renders all three sections', async () => {
    const { fixture, getSchedule } = await setup();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(getSchedule).toHaveBeenCalledWith('me');
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
    expect(compiled.querySelector('app-manage-work-locations')).toBeTruthy();
    expect(compiled.querySelector('app-work-pattern-editor')).toBeTruthy();
    expect(compiled.querySelector('app-work-day-overrides')).toBeTruthy();
  });

  it('shows an error when the schedule cannot be loaded', async () => {
    const { fixture } = await setup(
      vi.fn(async () => {
        throw new Error('boom');
      }),
    );
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Unable to load your work locations.');
    expect(compiled.querySelector('app-manage-work-locations')).toBeNull();
  });
});
