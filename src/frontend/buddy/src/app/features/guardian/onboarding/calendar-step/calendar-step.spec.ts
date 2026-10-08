import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { CalendarsService } from '../../../../core/calendars.service';
import { GroupsService } from '../../../../core/groups.service';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import {
  buttonByText,
  calendarDetail,
  groupDetail,
  settle,
  setupWith,
  submit,
  typeInto,
} from '../../../../../testing/onboarding-fixture';
import { CalendarStep } from './calendar-step';

describe('CalendarStep', () => {
  async function setup(setupState: OnboardingSetup = setupWith()) {
    const calendars: Partial<CalendarsService> = {
      createCalendar: vi.fn(async () => ({
        id: 'cal-new',
        name: 'Family',
        icon: '📅',
        role: 0 as const,
      })),
    };
    const groups: Partial<GroupsService> = {
      updateCalendarPermissionPolicy: vi.fn(async () => {}),
    };

    await TestBed.configureTestingModule({
      imports: [CalendarStep],
      providers: [
        { provide: CalendarsService, useValue: calendars },
        { provide: GroupsService, useValue: groups },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CalendarStep);
    fixture.componentRef.setInput('setup', setupState);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, calendars, groups, changed };
  }

  it('creates a calendar owned by the setup group', async () => {
    const { fixture, compiled, calendars, changed } = await setup();

    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingCalendarName')!, 'Family');
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingCalendarIcon')!, '🏠');
    const zone = compiled.querySelector<HTMLSelectElement>('#onboardingCalendarTimeZone')!;
    typeInto(zone, zone.options[0]!.value);
    await settle(fixture);
    submit(compiled);
    await settle(fixture);

    expect(calendars.createCalendar).toHaveBeenCalledWith({
      name: 'Family',
      timeZoneId: zone.options[0]!.value,
      groupId: 'group-1',
      icon: '🏠',
    });
    expect(changed).toHaveBeenCalled();
  });

  it('shows the group’s actual policy and no fix when children can only view', async () => {
    const { compiled } = await setup();

    expect(compiled.textContent).toContain('Admins: can add and change items');
    expect(compiled.textContent).toContain('Children and other members: can see it');
    expect(buttonByText(compiled, 'Make children view-only')).toBeUndefined();
  });

  it('offers an explicit change when members could edit the calendar', async () => {
    const { fixture, compiled, groups, changed } = await setup(
      setupWith({
        group: groupDetail({ calendarPermissionPolicy: { Owner: 0, Admin: 1, Member: 1 } }),
      }),
    );

    expect(compiled.textContent).toContain('Members, including your children, can change');
    buttonByText(compiled, 'Make children view-only')!.click();
    await settle(fixture);

    expect(groups.updateCalendarPermissionPolicy).toHaveBeenCalledWith('group-1', {
      Owner: 0,
      Admin: 1,
      Member: 2,
    });
    expect(changed).toHaveBeenCalled();
  });

  it('lists the group’s calendars', async () => {
    const { compiled } = await setup(
      setupWith({ calendars: [calendarDetail({ name: 'School' })] }),
    );

    expect(compiled.textContent).toContain('Calendars in your group');
    expect(compiled.textContent).toContain('School');
  });
});
