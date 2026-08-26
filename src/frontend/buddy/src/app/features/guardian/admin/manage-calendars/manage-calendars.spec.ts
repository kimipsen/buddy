import { DatePipe } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { CalendarSummary, CalendarsService, IcalTokenSummary, IssuedIcalToken } from '../../../../core/calendars.service';
import { browserTimeZoneId } from '../../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { ManageCalendars } from './manage-calendars';

describe('ManageCalendars', () => {
  function calendar(overrides: Partial<CalendarSummary> = {}): CalendarSummary {
    return { id: 'cal-1', name: 'Home', icon: '🏠', role: 0, ...overrides };
  }

  function group(overrides: Partial<GroupSummary> = {}): GroupSummary {
    return { id: 'group-1', name: 'Family', role: 0, ...overrides };
  }

  function icalToken(overrides: Partial<IcalTokenSummary> = {}): IcalTokenSummary {
    return { tokenId: 'token-1', issuedAt: '2026-08-01T00:00:00Z', ...overrides };
  }

  function issuedToken(overrides: Partial<IssuedIcalToken> = {}): IssuedIcalToken {
    return { tokenId: 'token-new', token: 'plaintext-secret', subscriptionPath: '/ical/token-new.ics', ...overrides };
  }

  interface Stubs {
    calendars?: Partial<CalendarsService>;
    groups?: Partial<GroupsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const calendarsStub: Partial<CalendarsService> = {
      listMyCalendars: vi.fn(async () => []),
      createCalendar: vi.fn(async (request) => ({ id: 'cal-new', name: request.name, icon: request.icon ?? '📅', role: 0 }) as CalendarSummary),
      updateCalendarIcon: vi.fn(async () => undefined),
      transferToGroup: vi.fn(async () => undefined),
      deleteCalendar: vi.fn(async () => undefined),
      listIcalTokens: vi.fn(async () => []),
      createIcalToken: vi.fn(async () => issuedToken()),
      revokeIcalToken: vi.fn(async () => undefined),
      icalFeedUrl: vi.fn((path: string) => `https://api.buddy.test${path}`),
      ...stubs.calendars
    };
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => [group()]),
      ...stubs.groups
    };

    await TestBed.configureTestingModule({
      imports: [ManageCalendars],
      providers: [
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: GroupsService, useValue: groupsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageCalendars);

    return { fixture, calendars: calendarsStub, groups: groupsStub };
  }

  // loadCalendars/loadManageableGroups/loadIcalTokens each chain at least one await before the
  // signals driving the template settle, and some flows (e.g. create -> reload, toggle -> load)
  // chain two mocked service calls back to back -- mirrors tasks-today.spec.ts's settle() since a
  // single whenStable() flush isn't always enough for a stubbed-service chain.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function nameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="calendarName"]')!;
  }

  function iconInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="calendarIcon"]')!;
  }

  function timeZoneSelect(compiled: HTMLElement): HTMLSelectElement {
    return compiled.querySelector<HTMLSelectElement>('select[name="calendarTimeZoneId"]')!;
  }

  function groupSelect(compiled: HTMLElement): HTMLSelectElement {
    return compiled.querySelector<HTMLSelectElement>('select[name="calendarGroupId"]')!;
  }

  function createForm(compiled: HTMLElement): HTMLFormElement {
    return nameInput(compiled).closest('form')!;
  }

  function addCalendarButton(compiled: HTMLElement): HTMLButtonElement {
    return findButtonByText(compiled, 'Add calendar')!;
  }

  // ----- Calendars list: loading / empty / error -----

  it('shows a loading message before the calendars list resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading calendars…');
  });

  it('shows the empty state once loading finishes with no calendars', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => []) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No calendars yet. Create one below.');
  });

  it('shows an error message when loading calendars fails', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load calendars.');
  });

  it('renders each calendar with its icon, name and role label', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar({ id: 'cal-1', name: 'Home', icon: '🏠', role: 0 }), calendar({ id: 'cal-2', name: 'Work', icon: '💼', role: 1 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('🏠');
    expect(compiled.textContent).toContain('Home');
    expect(compiled.textContent).toContain('💼');
    expect(compiled.textContent).toContain('Work');
    expect(compiled.textContent).toContain('Owner');
    expect(compiled.textContent).toContain('Contributor');
  });

  it('shows the Viewer role label for a role-2 calendar', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 2 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Viewer');
  });

  it('hides the owner-only action buttons for a calendar the caller only contributes to', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 1 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Subscribe')).toBeUndefined();
    expect(findButtonByText(compiled, 'Move to group')).toBeUndefined();
    expect(findButtonByText(compiled, 'Change icon')).toBeUndefined();
    expect(findButtonByText(compiled, 'Delete')).toBeUndefined();
  });

  it('hides the owner-only action buttons for a calendar the caller only views', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 2 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Subscribe')).toBeUndefined();
    expect(findButtonByText(compiled, 'Delete')).toBeUndefined();
  });

  it('shows the owner-only action buttons for a calendar the caller owns', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 0 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Subscribe')).toBeTruthy();
    expect(findButtonByText(compiled, 'Move to group')).toBeTruthy();
    expect(findButtonByText(compiled, 'Change icon')).toBeTruthy();
    expect(findButtonByText(compiled, 'Delete')).toBeTruthy();
  });

  // ----- Create-calendar form -----

  it('hides the create-calendar form and shows a hint when the caller manages no group', async () => {
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('You need a group before you can add a calendar. Create one under Groups first.');
    expect(compiled.querySelector('input[name="calendarName"]')).toBeNull();
  });

  it('silently treats a failure to load manageable groups as having no groups', async () => {
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('You need a group before you can add a calendar. Create one under Groups first.');
    // No dedicated error state exists for this failure -- it degrades to the same hint as "no groups".
    expect(compiled.textContent).not.toContain('Unable to load calendars.');
  });

  it('offers only groups the caller owns or administers as create-calendar options, not ones where they are a member', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-owner', name: 'Owned', role: 0 }), group({ id: 'g-admin', name: 'Administered', role: 1 }), group({ id: 'g-member', name: 'MemberOnly', role: 2 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const optionNames = Array.from(groupSelect(compiled).querySelectorAll('option')).map((option) => option.textContent?.trim());
    expect(optionNames).toContain('Owned');
    expect(optionNames).toContain('Administered');
    expect(optionNames).not.toContain('MemberOnly');
  });

  it('auto-selects the first manageable group for the create form', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-first', name: 'First', role: 0 }), group({ id: 'g-second', name: 'Second', role: 1 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(groupSelect(compiled).value).toBe('g-first');
  });

  it('defaults the icon field to the calendar icon placeholder emoji', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(iconInput(compiled).value).toBe('📅');
  });

  it('keeps the add-calendar button disabled until a name is entered', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(addCalendarButton(compiled).disabled).toBe(true);
  });

  it('keeps the add-calendar button disabled for a whitespace-only name', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(nameInput(compiled), '   ');
    await settle(fixture);

    expect(addCalendarButton(compiled).disabled).toBe(true);
  });

  it('enables the add-calendar button once a name is entered and a group is selected', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(nameInput(compiled), 'New Calendar');
    await settle(fixture);

    expect(addCalendarButton(compiled).disabled).toBe(false);
  });

  it('does not call createCalendar when the form is submitted with a blank name', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createCalendar).not.toHaveBeenCalled();
  });

  it('creates a calendar with the trimmed name, trimmed icon, selected time zone and selected group', async () => {
    const listMyCalendars = vi.fn(async () => [calendar()]);
    const { fixture, calendars } = await setup({
      calendars: { listMyCalendars },
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-1', name: 'Family', role: 0 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    // Pick an explicit option rather than relying on the pre-selected default -- see the
    // dedicated test below pinning that the default (browserTimeZoneId()) isn't always a valid
    // option in this list, which would make a value read off the unselected <select> unreliable.
    const tzSelect = timeZoneSelect(compiled);
    const selectedTimeZone = tzSelect.querySelectorAll('option')[1].value;
    tzSelect.value = selectedTimeZone;
    tzSelect.dispatchEvent(new Event('change'));
    setInputValue(nameInput(compiled), '  Home Calendar  ');
    setInputValue(iconInput(compiled), ' 🏡 ');
    await settle(fixture);

    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createCalendar).toHaveBeenCalledWith({ name: 'Home Calendar', timeZoneId: selectedTimeZone, groupId: 'g-1', icon: '🏡' });
    expect(listMyCalendars).toHaveBeenCalledTimes(2);
  });

  it('falls back to a real dropdown option when the detected browser time zone is not one of listTimeZoneIds()', async () => {
    // In this environment browserTimeZoneId() resolves to 'UTC' (Intl.DateTimeFormat().resolvedOptions().timeZone),
    // but listTimeZoneIds() is built from Intl.supportedValuesOf('timeZone'), which does not include the
    // 'UTC' alias -- only IANA zone names like 'Etc/UTC'. resolveDefaultTimeZoneId() now falls back to the
    // first listed zone in that case, so the pre-selected value is always one the <select> actually has an
    // <option> for.
    const detectedTimeZone = browserTimeZoneId();
    const { fixture, calendars } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = timeZoneSelect(compiled);
    const optionValues = Array.from(select.querySelectorAll('option')).map((option) => option.value);
    expect(optionValues).not.toContain(detectedTimeZone);
    expect(optionValues).toContain(select.value);

    setInputValue(nameInput(compiled), 'Home Calendar');
    await settle(fixture);
    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createCalendar).toHaveBeenCalledWith(expect.objectContaining({ timeZoneId: select.value }));
    expect(calendars.createCalendar).not.toHaveBeenCalledWith(expect.objectContaining({ timeZoneId: detectedTimeZone }));
  });

  it('submits a null icon when the icon field is cleared', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(nameInput(compiled), 'Home Calendar');
    setInputValue(iconInput(compiled), '');
    await settle(fixture);

    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createCalendar).toHaveBeenCalledWith(expect.objectContaining({ icon: null }));
  });

  it('clears the name and resets the icon to its default after a successful create', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(nameInput(compiled), 'Home Calendar');
    setInputValue(iconInput(compiled), '🏡');
    await settle(fixture);

    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(nameInput(compiled).value).toBe('');
    expect(iconInput(compiled).value).toBe('📅');
  });

  it('shows an error and keeps the typed name when creating a calendar fails', async () => {
    const { fixture } = await setup({ calendars: { createCalendar: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(nameInput(compiled), 'Home Calendar');
    await settle(fixture);

    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to create the calendar.');
    expect(nameInput(compiled).value).toBe('Home Calendar');
  });

  // ----- Change-icon flow -----

  it('opens the change-icon form pre-filled with the calendar\'s current icon', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar({ icon: '🏠' })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);

    const editInput = compiled.querySelector<HTMLInputElement>('input[name="editIconValue"]')!;
    expect(editInput.value).toBe('🏠');
  });

  it('closes the change-icon form on a second click of the toggle button', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="editIconValue"]')).toBeTruthy();

    findButtonByText(compiled, 'Close')!.click();
    await settle(fixture);

    expect(compiled.querySelector('input[name="editIconValue"]')).toBeNull();
  });

  it('disables the save button while the icon field is empty', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);

    const editInput = compiled.querySelector<HTMLInputElement>('input[name="editIconValue"]')!;
    setInputValue(editInput, '   ');
    await settle(fixture);

    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(true);
  });

  it('saves the new icon, closes the form, and reloads the calendars list', async () => {
    const listMyCalendars = vi.fn(async () => [calendar()]);
    const { fixture, calendars } = await setup({ calendars: { listMyCalendars } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);

    const editInput = compiled.querySelector<HTMLInputElement>('input[name="editIconValue"]')!;
    setInputValue(editInput, '🎉');
    await settle(fixture);

    editInput.closest('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.updateCalendarIcon).toHaveBeenCalledWith('cal-1', '🎉');
    expect(listMyCalendars).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('input[name="editIconValue"]')).toBeNull();
  });

  it('shows an error and keeps the form open when changing the icon fails', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), updateCalendarIcon: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);

    const editInput = compiled.querySelector<HTMLInputElement>('input[name="editIconValue"]')!;
    setInputValue(editInput, '🎉');
    await settle(fixture);

    editInput.closest('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain("Unable to change this calendar's icon.");
    expect(compiled.querySelector('input[name="editIconValue"]')).toBeTruthy();
  });

  // ----- Transfer-to-group flow -----

  it('shows the no-other-groups hint when the caller manages no group to move into', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('You need another group you manage before you can move this calendar.');
  });

  it('offers only manageable groups as move targets', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-owner', name: 'Owned', role: 0 }), group({ id: 'g-member', name: 'MemberOnly', role: 2 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    const select = compiled.querySelector<HTMLSelectElement>('select[name="moveTargetGroupId"]')!;
    const optionNames = Array.from(select.querySelectorAll('option')).map((option) => option.textContent?.trim());
    expect(optionNames).toContain('Owned');
    expect(optionNames).not.toContain('MemberOnly');
  });

  it('disables the move-confirm button until a target group is chosen', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-1', name: 'Family', role: 0 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Move')!.disabled).toBe(true);
  });

  it('closes the move form on a second click of the toggle button', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);
    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeTruthy();

    findButtonByText(compiled, 'Close')!.click();
    await settle(fixture);

    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeNull();
  });

  it('transfers the calendar to the selected group, closes the form, and reloads the list', async () => {
    const listMyCalendars = vi.fn(async () => [calendar()]);
    const { fixture, calendars } = await setup({
      calendars: { listMyCalendars },
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-target', name: 'New Group', role: 0 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    const select = compiled.querySelector<HTMLSelectElement>('select[name="moveTargetGroupId"]')!;
    select.value = 'g-target';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    select.closest('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.transferToGroup).toHaveBeenCalledWith('cal-1', 'g-target');
    expect(listMyCalendars).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeNull();
  });

  it('shows an error and keeps the form open when moving a calendar fails', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), transferToGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-target', name: 'New Group', role: 0 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    const select = compiled.querySelector<HTMLSelectElement>('select[name="moveTargetGroupId"]')!;
    select.value = 'g-target';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    select.closest('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to move this calendar. You may not manage the destination group.');
    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeTruthy();
  });

  // ----- Delete flow -----

  it('shows a confirmation prompt instead of deleting immediately', async () => {
    const { fixture, calendars } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Delete this calendar? This cannot be undone.');
    expect(findButtonByText(compiled, 'Confirm')).toBeTruthy();
    expect(calendars.deleteCalendar).not.toHaveBeenCalled();
  });

  it('cancels the delete confirmation without deleting', async () => {
    const { fixture, calendars } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Delete this calendar? This cannot be undone.');
    expect(calendars.deleteCalendar).not.toHaveBeenCalled();
    expect(findButtonByText(compiled, 'Delete')).toBeTruthy();
  });

  it('deletes the calendar on confirm, closes the prompt, and reloads the list', async () => {
    let loadCount = 0;
    const listMyCalendars = vi.fn(async () => (loadCount++ === 0 ? [calendar()] : []));
    const { fixture, calendars } = await setup({ calendars: { listMyCalendars } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    expect(calendars.deleteCalendar).toHaveBeenCalledWith('cal-1');
    expect(listMyCalendars).toHaveBeenCalledTimes(2);
    // The list re-renders empty once the reload reflects the deletion.
    expect(compiled.textContent).toContain('No calendars yet. Create one below.');
  });

  it('shows an error and keeps the confirmation prompt open when deleting fails', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), deleteCalendar: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to delete this calendar.');
    // Unlike a successful delete, a failed attempt leaves the confirm/cancel prompt in place
    // (confirmDelete only clears confirmingDeleteCalendarId inside the try block, not on error)
    // so the guardian can retry without re-clicking "Delete".
    expect(findButtonByText(compiled, 'Confirm')).toBeTruthy();
    expect(findButtonByText(compiled, 'Cancel')).toBeTruthy();
  });

  it('disables the confirm and cancel buttons while a delete is in flight', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Confirm')!.click();
    fixture.detectChanges();

    expect(findButtonByText(compiled, 'Confirm')!.disabled).toBe(true);
    expect(findButtonByText(compiled, 'Cancel')!.disabled).toBe(true);
  });

  // ----- Panel mutual exclusivity -----

  it('opening the move panel closes an open change-icon panel', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="editIconValue"]')).toBeTruthy();

    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    expect(compiled.querySelector('input[name="editIconValue"]')).toBeNull();
    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeTruthy();
  });

  it('requesting delete closes an open move panel and an open iCal panel', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);
    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeTruthy();

    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeNull();
    expect(compiled.textContent).toContain('Delete this calendar? This cannot be undone.');
  });

  it('opening the iCal panel closes an open change-icon panel', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="editIconValue"]')).toBeTruthy();

    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    expect(compiled.querySelector('input[name="editIconValue"]')).toBeNull();
  });

  // ----- iCal subscription tokens -----

  it('loads and shows a transient loading message when the iCal panel is opened', async () => {
    const { fixture, calendars } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Loading links…');
    await settle(fixture);

    expect(calendars.listIcalTokens).toHaveBeenCalledWith('cal-1');
  });

  it('shows the empty state when a calendar has no subscription tokens', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows an error when loading iCal tokens fails', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to load subscription links.');
  });

  it('lists existing tokens with their issued date and a revoke button each', async () => {
    const datePipe = new DatePipe('en-US');
    const expectedDate = datePipe.transform('2026-08-01T00:00:00Z', 'mediumDate');
    const tokens = [icalToken({ tokenId: 'token-a', issuedAt: '2026-08-01T00:00:00Z' }), icalToken({ tokenId: 'token-b', issuedAt: '2026-08-05T00:00:00Z' })];
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens: vi.fn(async () => tokens) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(`Created ${expectedDate}`);
    expect(Array.from(compiled.querySelectorAll('button')).filter((button) => button.textContent?.trim() === 'Revoke')).toHaveLength(2);
  });

  it('closes the iCal panel on a second click without reloading tokens', async () => {
    const listIcalTokens = vi.fn(async () => []);
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);
    expect(listIcalTokens).toHaveBeenCalledTimes(1);

    findButtonByText(compiled, 'Close')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('No subscription links yet.');
    expect(listIcalTokens).toHaveBeenCalledTimes(1);
  });

  it('shows a generating state while creating a new token, then the plaintext feed URL and a copy button', async () => {
    const listIcalTokens = vi.fn(async () => []);
    const { fixture, calendars } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        listIcalTokens,
        createIcalToken: vi.fn(async () => issuedToken({ subscriptionPath: '/ical/token-new.ics' }))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Generate new link')!.click();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Generating…');
    expect(findButtonByText(compiled, 'Generating…')!.disabled).toBe(true);

    await settle(fixture);

    expect(calendars.icalFeedUrl).toHaveBeenCalledWith('/ical/token-new.ics');
    expect(compiled.textContent).toContain('https://api.buddy.test/ical/token-new.ics');
    expect(compiled.textContent).toContain('Copy this link now -- it will not be shown again.');
    expect(findButtonByText(compiled, 'Copy')).toBeTruthy();
    // The panel reloads the token list after issuing a new one.
    expect(listIcalTokens).toHaveBeenCalledTimes(2);
  });

  it('shows an error when creating a new token fails', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), createIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Generate new link')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to generate a subscription link.');
  });

  it('copies the newly issued feed URL to the clipboard and shows a confirmation', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        createIcalToken: vi.fn(async () => issuedToken({ subscriptionPath: '/ical/token-new.ics' }))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);
    findButtonByText(compiled, 'Generate new link')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Copy')!.click();
    await settle(fixture);

    expect(writeText).toHaveBeenCalledWith('https://api.buddy.test/ical/token-new.ics');
    expect(findButtonByText(compiled, 'Copied')).toBeTruthy();
  });

  it('leaves the copy button unchanged when the clipboard write fails', async () => {
    const writeText = vi.fn(async () => Promise.reject(new Error('denied')));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        createIcalToken: vi.fn(async () => issuedToken({ subscriptionPath: '/ical/token-new.ics' }))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);
    findButtonByText(compiled, 'Generate new link')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Copy')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Copy')).toBeTruthy();
    expect(findButtonByText(compiled, 'Copied')).toBeUndefined();
  });

  it('revokes a token and reloads the token list', async () => {
    let loadCount = 0;
    const tokens = [icalToken({ tokenId: 'token-a' })];
    const listIcalTokens = vi.fn(async () => (loadCount++ === 0 ? tokens : []));
    const { fixture, calendars } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(calendars.revokeIcalToken).toHaveBeenCalledWith('cal-1', 'token-a');
    expect(listIcalTokens).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows an error when revoking a token fails', async () => {
    const tokens = [icalToken({ tokenId: 'token-a' })];
    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        listIcalTokens: vi.fn(async () => tokens),
        revokeIcalToken: vi.fn(async () => Promise.reject(new Error('boom')))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to revoke this link.');
  });
});
