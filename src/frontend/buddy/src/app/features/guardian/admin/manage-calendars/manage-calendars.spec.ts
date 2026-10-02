import { DatePipe } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  CalendarSummary,
  CalendarsService,
  IcalTokenSummary,
  IssuedIcalToken,
} from '../../../../core/calendars.service';
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
    return {
      tokenId: 'token-new',
      token: 'plaintext-secret',
      subscriptionPath: '/ical/token-new.ics',
      ...overrides,
    };
  }

  interface Stubs {
    calendars?: Partial<CalendarsService>;
    groups?: Partial<GroupsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const calendarsStub: Partial<CalendarsService> = {
      listMyCalendars: vi.fn(async () => []),
      createCalendar: vi.fn(
        async (request) =>
          ({
            id: 'cal-new',
            name: request.name,
            icon: request.icon ?? '📅',
            role: 0,
          }) as CalendarSummary,
      ),
      updateCalendarIcon: vi.fn(async () => undefined),
      transferToGroup: vi.fn(async () => undefined),
      deleteCalendar: vi.fn(async () => undefined),
      listIcalTokens: vi.fn(async () => []),
      createIcalToken: vi.fn(async () => issuedToken()),
      revokeIcalToken: vi.fn(async () => undefined),
      icalFeedUrl: vi.fn((path: string) => `https://api.buddy.test${path}`),
      ...stubs.calendars,
    };
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => [group()]),
      ...stubs.groups,
    };

    await TestBed.configureTestingModule({
      imports: [ManageCalendars],
      providers: [
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: GroupsService, useValue: groupsStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageCalendars);

    return { fixture, calendars: calendarsStub, groups: groupsStub };
  }

  // loadCalendars/loadManageableGroups/loadIcalTokens each chain at least one await before the
  // signals driving the template settle, and some flows (e.g. create -> reload, toggle -> load)
  // chain two mocked service calls back to back -- mirrors tasks-today.spec.ts's settle() since a
  // single whenStable() flush isn't always enough for a stubbed-service chain.
  async function settle(fixture: {
    detectChanges: () => void;
    whenStable: () => Promise<boolean>;
  }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
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

  function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (reason: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  function editIconInput(compiled: HTMLElement): HTMLInputElement | null {
    return compiled.querySelector<HTMLInputElement>('input[name="editIconValue"]');
  }

  function moveSelect(compiled: HTMLElement): HTMLSelectElement | null {
    return compiled.querySelector<HTMLSelectElement>('select[name="moveTargetGroupId"]');
  }

  function chooseMoveTarget(compiled: HTMLElement, groupId: string): void {
    const select = moveSelect(compiled)!;
    select.value = groupId;
    select.dispatchEvent(new Event('change'));
  }

  function submitFormOf(element: Element): void {
    element.closest('form')!.dispatchEvent(new Event('submit'));
  }

  function buttonsByText(compiled: HTMLElement, text: string): HTMLButtonElement[] {
    return Array.from(compiled.querySelectorAll('button')).filter(
      (button) => button.textContent?.trim() === text,
    );
  }

  function icalPanelOpen(compiled: HTMLElement): boolean {
    return findButtonByText(compiled, 'Generate new link') !== undefined;
  }

  const twoCalendars = () => [
    calendar({ id: 'cal-1', name: 'Home' }),
    calendar({ id: 'cal-2', name: 'Work' }),
  ];

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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load calendars.');
  });

  it('renders each calendar with its icon, name and role label', async () => {
    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [
          calendar({ id: 'cal-1', name: 'Home', icon: '🏠', role: 0 }),
          calendar({ id: 'cal-2', name: 'Work', icon: '💼', role: 1 }),
        ]),
      },
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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 2 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Viewer');
  });

  it('hides the owner-only action buttons for a calendar the caller only contributes to', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 1 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Subscribe')).toBeUndefined();
    expect(findButtonByText(compiled, 'Move to group')).toBeUndefined();
    expect(findButtonByText(compiled, 'Change icon')).toBeUndefined();
    expect(findButtonByText(compiled, 'Delete')).toBeUndefined();
  });

  it('hides the owner-only action buttons for a calendar the caller only views', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 2 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Subscribe')).toBeUndefined();
    expect(findButtonByText(compiled, 'Delete')).toBeUndefined();
  });

  it('shows the owner-only action buttons for a calendar the caller owns', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar({ role: 0 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Subscribe')).toBeTruthy();
    expect(findButtonByText(compiled, 'Move to group')).toBeTruthy();
    expect(findButtonByText(compiled, 'Change icon')).toBeTruthy();
    expect(findButtonByText(compiled, 'Delete')).toBeTruthy();
  });

  // ----- Create-calendar form -----

  it('hides the create-calendar form and shows a hint when the caller manages no group', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain(
      'You need a group before you can add a calendar. Create one under Groups first.',
    );
    expect(compiled.querySelector('input[name="calendarName"]')).toBeNull();
  });

  it('silently treats a failure to load manageable groups as having no groups', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain(
      'You need a group before you can add a calendar. Create one under Groups first.',
    );
    // No dedicated error state exists for this failure -- it degrades to the same hint as "no groups".
    expect(compiled.textContent).not.toContain('Unable to load calendars.');
  });

  it('offers only groups the caller owns or administers as create-calendar options, not ones where they are a member', async () => {
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [
          group({ id: 'g-owner', name: 'Owned', role: 0 }),
          group({ id: 'g-admin', name: 'Administered', role: 1 }),
          group({ id: 'g-member', name: 'MemberOnly', role: 2 }),
        ]),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const optionNames = Array.from(groupSelect(compiled).querySelectorAll('option')).map((option) =>
      option.textContent?.trim(),
    );
    expect(optionNames).toContain('Owned');
    expect(optionNames).toContain('Administered');
    expect(optionNames).not.toContain('MemberOnly');
  });

  it('auto-selects the first manageable group for the create form', async () => {
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [
          group({ id: 'g-first', name: 'First', role: 0 }),
          group({ id: 'g-second', name: 'Second', role: 1 }),
        ]),
      },
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
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-1', name: 'Family', role: 0 })]) },
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

    expect(calendars.createCalendar).toHaveBeenCalledWith({
      name: 'Home Calendar',
      timeZoneId: selectedTimeZone,
      groupId: 'g-1',
      icon: '🏡',
    });
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
    const optionValues = Array.from(select.querySelectorAll('option')).map(
      (option) => option.value,
    );
    expect(optionValues).not.toContain(detectedTimeZone);
    expect(optionValues).toContain(select.value);

    setInputValue(nameInput(compiled), 'Home Calendar');
    await settle(fixture);
    createForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createCalendar).toHaveBeenCalledWith(
      expect.objectContaining({ timeZoneId: select.value }),
    );
    expect(calendars.createCalendar).not.toHaveBeenCalledWith(
      expect.objectContaining({ timeZoneId: detectedTimeZone }),
    );
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
    const { fixture } = await setup({
      calendars: { createCalendar: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
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

  it("opens the change-icon form pre-filled with the calendar's current icon", async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar({ icon: '🏠' })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Change icon')!.click();
    await settle(fixture);

    const editInput = compiled.querySelector<HTMLInputElement>('input[name="editIconValue"]')!;
    expect(editInput.value).toBe('🏠');
  });

  it('closes the change-icon form on a second click of the toggle button', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        updateCalendarIcon: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
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
      groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(
      'You need another group you manage before you can move this calendar.',
    );
  });

  it('offers only manageable groups as move targets', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      groups: {
        listMyGroups: vi.fn(async () => [
          group({ id: 'g-owner', name: 'Owned', role: 0 }),
          group({ id: 'g-member', name: 'MemberOnly', role: 2 }),
        ]),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    const select = compiled.querySelector<HTMLSelectElement>('select[name="moveTargetGroupId"]')!;
    const optionNames = Array.from(select.querySelectorAll('option')).map((option) =>
      option.textContent?.trim(),
    );
    expect(optionNames).toContain('Owned');
    expect(optionNames).not.toContain('MemberOnly');
  });

  it('disables the move-confirm button until a target group is chosen', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g-1', name: 'Family', role: 0 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Move to group')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Move')!.disabled).toBe(true);
  });

  it('closes the move form on a second click of the toggle button', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
      groups: {
        listMyGroups: vi.fn(async () => [group({ id: 'g-target', name: 'New Group', role: 0 })]),
      },
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
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        transferToGroup: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
      groups: {
        listMyGroups: vi.fn(async () => [group({ id: 'g-target', name: 'New Group', role: 0 })]),
      },
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

    expect(compiled.textContent).toContain(
      'Unable to move this calendar. You may not manage the destination group.',
    );
    expect(compiled.querySelector('select[name="moveTargetGroupId"]')).toBeTruthy();
  });

  // ----- Delete flow -----

  it('shows a confirmation prompt instead of deleting immediately', async () => {
    const { fixture, calendars } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Delete this calendar? This cannot be undone.');
    expect(findButtonByText(compiled, 'Confirm')).toBeTruthy();
    expect(calendars.deleteCalendar).not.toHaveBeenCalled();
  });

  it('cancels the delete confirmation without deleting', async () => {
    const { fixture, calendars } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        deleteCalendar: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
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
    const { fixture, calendars } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Loading links…');
    await settle(fixture);

    expect(calendars.listIcalTokens).toHaveBeenCalledWith('cal-1');
  });

  it('shows the empty state when a calendar has no subscription tokens', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows an error when loading iCal tokens fails', async () => {
    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        listIcalTokens: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
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
    const tokens = [
      icalToken({ tokenId: 'token-a', issuedAt: '2026-08-01T00:00:00Z' }),
      icalToken({ tokenId: 'token-b', issuedAt: '2026-08-05T00:00:00Z' }),
    ];
    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        listIcalTokens: vi.fn(async () => tokens),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(`Created ${expectedDate}`);
    expect(
      Array.from(compiled.querySelectorAll('button')).filter(
        (button) => button.textContent?.trim() === 'Revoke',
      ),
    ).toHaveLength(2);
  });

  it('closes the iCal panel on a second click without reloading tokens', async () => {
    const listIcalTokens = vi.fn(async () => []);
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens },
    });
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
        createIcalToken: vi.fn(async () =>
          issuedToken({ subscriptionPath: '/ical/token-new.ics' }),
        ),
      },
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
      calendars: {
        listMyCalendars: vi.fn(async () => [calendar()]),
        createIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
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
        createIcalToken: vi.fn(async () =>
          issuedToken({ subscriptionPath: '/ical/token-new.ics' }),
        ),
      },
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
        createIcalToken: vi.fn(async () =>
          issuedToken({ subscriptionPath: '/ical/token-new.ics' }),
        ),
      },
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
    const { fixture, calendars } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens },
    });
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
        revokeIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Subscribe')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to revoke this link.');
  });

  // ----- In-flight, retry and reset behaviour -----

  describe('create in flight and retry', () => {
    it('disables the add button while a create is in flight and clears an earlier error', async () => {
      const pending = deferred<CalendarSummary>();
      const createCalendar = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({ calendars: { createCalendar } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(nameInput(compiled), 'Home Calendar');
      await settle(fixture);
      createForm(compiled).dispatchEvent(new Event('submit'));
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to create the calendar.');
      // A failed create releases the button so the guardian can retry.
      expect(addCalendarButton(compiled).disabled).toBe(false);

      createForm(compiled).dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(addCalendarButton(compiled).disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to create the calendar.');

      pending.resolve(calendar({ id: 'cal-new' }));
      await settle(fixture);
    });

    it('clears an earlier load error once a reload after create succeeds', async () => {
      const listMyCalendars = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockResolvedValue([calendar({ name: 'Home Calendar' })]);
      const { fixture } = await setup({ calendars: { listMyCalendars } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to load calendars.');

      setInputValue(nameInput(compiled), 'Home Calendar');
      await settle(fixture);
      createForm(compiled).dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to load calendars.');
      expect(findButtonByText(compiled, 'Delete')).toBeTruthy();
    });
  });

  describe('change icon in flight and retry', () => {
    it('enables Save for the pre-filled icon, saves the trimmed value and ignores a blank submit', async () => {
      const { fixture, calendars } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);
      expect(findButtonByText(compiled, 'Save')!.disabled).toBe(false);

      setInputValue(editIconInput(compiled)!, '   ');
      await settle(fixture);
      submitFormOf(editIconInput(compiled)!);
      await settle(fixture);
      expect(calendars.updateCalendarIcon).not.toHaveBeenCalled();

      setInputValue(editIconInput(compiled)!, ' 🎉 ');
      await settle(fixture);
      submitFormOf(editIconInput(compiled)!);
      await settle(fixture);

      expect(calendars.updateCalendarIcon).toHaveBeenCalledExactlyOnceWith('cal-1', '🎉');
    });

    it('disables Save while in flight, re-enables it after a failure and hides the error on retry', async () => {
      const pending = deferred<void>();
      const updateCalendarIcon = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), updateCalendarIcon },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);
      submitFormOf(editIconInput(compiled)!);
      await settle(fixture);
      expect(compiled.textContent).toContain("Unable to change this calendar's icon.");
      expect(findButtonByText(compiled, 'Save')!.disabled).toBe(false);

      submitFormOf(editIconInput(compiled)!);
      await settle(fixture);

      expect(findButtonByText(compiled, 'Save')!.disabled).toBe(true);
      expect(compiled.textContent).not.toContain("Unable to change this calendar's icon.");

      pending.resolve();
      await settle(fixture);
    });

    it('clears an earlier icon error when the form is reopened', async () => {
      const { fixture } = await setup({
        calendars: {
          listMyCalendars: vi.fn(async () => [calendar()]),
          updateCalendarIcon: vi.fn(async () => Promise.reject(new Error('boom'))),
        },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);
      submitFormOf(editIconInput(compiled)!);
      await settle(fixture);
      expect(compiled.textContent).toContain("Unable to change this calendar's icon.");

      findButtonByText(compiled, 'Close')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain("Unable to change this calendar's icon.");
    });
  });

  describe('move in flight and retry', () => {
    const target = () => [group({ id: 'g-target', name: 'New Group', role: 0 })];

    it('enables Move once a target is chosen and ignores a submit without one', async () => {
      const { fixture, calendars } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
        groups: { listMyGroups: vi.fn(async () => target()) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);
      submitFormOf(moveSelect(compiled)!);
      await settle(fixture);
      expect(calendars.transferToGroup).not.toHaveBeenCalled();

      chooseMoveTarget(compiled, 'g-target');
      await settle(fixture);

      expect(findButtonByText(compiled, 'Move')!.disabled).toBe(false);
    });

    it('disables Move while in flight, re-enables it after a failure and hides the error on retry', async () => {
      const pending = deferred<void>();
      const transferToGroup = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), transferToGroup },
        groups: { listMyGroups: vi.fn(async () => target()) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);
      chooseMoveTarget(compiled, 'g-target');
      await settle(fixture);
      submitFormOf(moveSelect(compiled)!);
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to move this calendar.');
      expect(findButtonByText(compiled, 'Move')!.disabled).toBe(false);

      submitFormOf(moveSelect(compiled)!);
      await settle(fixture);

      expect(findButtonByText(compiled, 'Move')!.disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to move this calendar.');

      pending.resolve();
      await settle(fixture);
    });

    it('clears an earlier move error when the form is reopened', async () => {
      const { fixture } = await setup({
        calendars: {
          listMyCalendars: vi.fn(async () => [calendar()]),
          transferToGroup: vi.fn(async () => Promise.reject(new Error('boom'))),
        },
        groups: { listMyGroups: vi.fn(async () => target()) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);
      chooseMoveTarget(compiled, 'g-target');
      await settle(fixture);
      submitFormOf(moveSelect(compiled)!);
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to move this calendar.');

      findButtonByText(compiled, 'Close')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to move this calendar.');
    });
  });

  describe('delete in flight and retry', () => {
    it('closes the prompt after a successful delete even when the calendar is still listed', async () => {
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Delete')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Delete this calendar? This cannot be undone.');
      expect(findButtonByText(compiled, 'Delete')).toBeTruthy();
    });

    it('re-enables Confirm after a failure and hides the error while retrying', async () => {
      const pending = deferred<void>();
      const deleteCalendar = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), deleteCalendar },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Delete')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to delete this calendar.');
      expect(findButtonByText(compiled, 'Confirm')!.disabled).toBe(false);

      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to delete this calendar.');

      pending.resolve();
      await settle(fixture);
    });

    it('clears an earlier delete error when delete is requested again', async () => {
      const { fixture } = await setup({
        calendars: {
          listMyCalendars: vi.fn(async () => [calendar()]),
          deleteCalendar: vi.fn(async () => Promise.reject(new Error('boom'))),
        },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Delete')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Cancel')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to delete this calendar.');

      findButtonByText(compiled, 'Delete')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to delete this calendar.');
    });
  });

  describe('iCal in flight and retry', () => {
    async function openIcal(fixture: Awaited<ReturnType<typeof setup>>['fixture']) {
      await settle(fixture);
      findButtonByText(fixture.nativeElement as HTMLElement, 'Subscribe')!.click();
      await settle(fixture);
    }

    it('re-enables Generate after a failure and clears the error when generating again', async () => {
      const pending = deferred<IssuedIcalToken>();
      const createIcalToken = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), createIcalToken },
      });
      await openIcal(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to generate a subscription link.');
      expect(findButtonByText(compiled, 'Generate new link')!.disabled).toBe(false);

      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to generate a subscription link.');

      pending.resolve(issuedToken());
      await settle(fixture);
    });

    it('hides the previous feed URL while a replacement link is being generated', async () => {
      const pending = deferred<IssuedIcalToken>();
      const createIcalToken = vi
        .fn()
        .mockResolvedValueOnce(issuedToken({ subscriptionPath: '/ical/first.ics' }))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), createIcalToken },
      });
      await openIcal(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('https://api.buddy.test/ical/first.ics');

      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('https://api.buddy.test/ical/first.ics');

      pending.resolve(issuedToken({ subscriptionPath: '/ical/second.ics' }));
      await settle(fixture);
      expect(compiled.textContent).toContain('https://api.buddy.test/ical/second.ics');
    });

    it('forgets the feed URL and the create error once the panel is closed and reopened', async () => {
      const createIcalToken = vi
        .fn()
        .mockResolvedValueOnce(issuedToken({ subscriptionPath: '/ical/first.ics' }))
        .mockRejectedValueOnce(new Error('boom'));
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), createIcalToken },
      });
      await openIcal(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Close')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);
      expect(compiled.textContent).not.toContain('https://api.buddy.test/ical/first.ics');

      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to generate a subscription link.');
      findButtonByText(compiled, 'Close')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to generate a subscription link.');
    });

    it('disables the revoke button in flight, re-enables it after a failure and hides the error on retry', async () => {
      const pending = deferred<void>();
      const revokeIcalToken = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({
        calendars: {
          listMyCalendars: vi.fn(async () => [calendar()]),
          listIcalTokens: vi.fn(async () => [icalToken({ tokenId: 'token-a' })]),
          revokeIcalToken,
        },
      });
      await openIcal(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Revoke')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to revoke this link.');
      expect(findButtonByText(compiled, 'Revoke')!.disabled).toBe(false);

      findButtonByText(compiled, 'Revoke')!.click();
      await settle(fixture);

      expect(findButtonByText(compiled, 'Revoke')!.disabled).toBe(true);
      expect(compiled.textContent).not.toContain('Unable to revoke this link.');

      pending.resolve();
      await settle(fixture);
    });

    it('clears an earlier token load error when the panel reloads successfully', async () => {
      const listIcalTokens = vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]);
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]), listIcalTokens },
      });
      await openIcal(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to load subscription links.');

      findButtonByText(compiled, 'Close')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Unable to load subscription links.');
      expect(compiled.textContent).toContain('No subscription links yet.');
    });

    it('drops the copied confirmation when a later clipboard write fails', async () => {
      const writeText = vi
        .fn()
        .mockResolvedValueOnce(undefined)
        .mockRejectedValueOnce(new Error('denied'));
      Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await openIcal(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Generate new link')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Copy')!.click();
      await settle(fixture);
      expect(findButtonByText(compiled, 'Copied')).toBeTruthy();

      findButtonByText(compiled, 'Copied')!.click();
      await settle(fixture);

      expect(findButtonByText(compiled, 'Copy')).toBeTruthy();
      expect(findButtonByText(compiled, 'Copied')).toBeUndefined();
    });
  });

  describe('panel switching', () => {
    it('opening the move panel closes an open iCal panel', async () => {
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);
      expect(icalPanelOpen(compiled)).toBe(true);

      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);

      expect(icalPanelOpen(compiled)).toBe(false);
    });

    it('opening the change-icon panel closes an open move panel and an open iCal panel', async () => {
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);
      expect(moveSelect(compiled)).toBeNull();

      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);

      expect(icalPanelOpen(compiled)).toBe(false);
      expect(editIconInput(compiled)).toBeTruthy();
    });

    it('opening the iCal panel closes an open move panel', async () => {
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Move to group')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);

      expect(moveSelect(compiled)).toBeNull();
      expect(icalPanelOpen(compiled)).toBe(true);
    });

    it('requesting delete closes an open iCal panel and an open change-icon panel', async () => {
      const { fixture } = await setup({
        calendars: { listMyCalendars: vi.fn(async () => [calendar()]) },
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Subscribe')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Delete')!.click();
      await settle(fixture);
      expect(icalPanelOpen(compiled)).toBe(false);

      findButtonByText(compiled, 'Cancel')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Change icon')!.click();
      await settle(fixture);
      findButtonByText(compiled, 'Delete')!.click();
      await settle(fixture);

      expect(editIconInput(compiled)).toBeNull();
    });

    it.each(['Move to group', 'Change icon', 'Subscribe'])(
      "opening '%s' on another calendar closes a pending delete confirmation",
      async (action) => {
        const { fixture } = await setup({
          calendars: { listMyCalendars: vi.fn(async () => twoCalendars()) },
        });
        await settle(fixture);

        const compiled = fixture.nativeElement as HTMLElement;
        buttonsByText(compiled, 'Delete')[0].click();
        await settle(fixture);
        expect(compiled.textContent).toContain('Delete this calendar? This cannot be undone.');

        // cal-1's actions are replaced by the prompt, so the only remaining button is cal-2's.
        buttonsByText(compiled, action)[0].click();
        await settle(fixture);

        expect(compiled.textContent).not.toContain('Delete this calendar? This cannot be undone.');
      },
    );
  });
});
