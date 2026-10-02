import { WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  CalendarPermissionPolicy,
  GroupDetail,
  GroupInvite,
  GroupMember,
  GroupSummary,
  GroupsService,
  MealplanPermissionPolicy,
} from '../../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { ManageGroups } from './manage-groups';

// TranslatePipe/TranslationService are used unstubbed throughout (the same pattern as the other
// component specs in this app), so assertions below check the real English copy from
// core/i18n/translations/en/admin.ts rather than raw translation keys.
describe('ManageGroups', () => {
  function group(overrides: Partial<GroupSummary> = {}): GroupSummary {
    return { id: 'group-1', name: 'Home', role: 0, ...overrides };
  }

  function invite(overrides: Partial<GroupInvite> = {}): GroupInvite {
    return {
      id: 'invite-1',
      email: 'a@b.test',
      role: 2,
      invitedAt: '2026-08-01T00:00:00Z',
      expiresAt: '2026-08-08T00:00:00Z',
      ...overrides,
    };
  }

  function member(overrides: Partial<GroupMember> = {}): GroupMember {
    return {
      userId: 'member-1',
      givenName: 'Sam',
      familyName: 'Kid',
      role: 2,
      isChild: false,
      ...overrides,
    };
  }

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Sam', familyName: 'Kid' },
      guardianLinkId: 'link-1',
      kind: 1,
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides,
    };
  }

  const calendarPolicy: CalendarPermissionPolicy = { Owner: 0, Admin: 1, Member: 2 };
  const mealplanPolicy: MealplanPermissionPolicy = { Owner: 0, Admin: 2, Member: 3 };

  function groupDetail(overrides: Partial<GroupDetail> = {}): GroupDetail {
    return {
      id: 'group-1',
      name: 'Home',
      members: [],
      calendarPermissionPolicy: calendarPolicy,
      mealplanPermissionPolicy: mealplanPolicy,
      ...overrides,
    };
  }

  interface Stubs {
    groups?: Partial<GroupsService>;
    guardians?: Partial<GuardiansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => [group()]),
      createGroup: vi.fn(
        async (request) => ({ id: 'group-new', name: request.name, role: 0 }) as GroupSummary,
      ),
      listInvites: vi.fn(async () => []),
      inviteToGroup: vi.fn(async (_groupId, request) =>
        invite({ email: request.email, role: request.role }),
      ),
      revokeInvite: vi.fn(async () => undefined),
      addChildToGroup: vi.fn(async () => undefined),
      getGroup: vi.fn(async () => groupDetail()),
      updateCalendarPermissionPolicy: vi.fn(async () => undefined),
      updateMealplanPermissionPolicy: vi.fn(async () => undefined),
      ...stubs.groups,
    };
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      ...stubs.guardians,
    };

    await TestBed.configureTestingModule({
      imports: [ManageGroups],
      providers: [
        { provide: GroupsService, useValue: groupsStub },
        { provide: GuardiansService, useValue: guardiansStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageGroups);

    return { fixture, groups: groupsStub, guardians: guardiansStub };
  }

  // loadGroups/loadMyChildren/loadInvites/etc. each chain at least one await before the signals
  // driving the template settle, and some flows (createGroup -> loadGroups, sendInvite ->
  // loadInvites) chain two mocked service calls back to back -- mirrors tasks-today.spec.ts's
  // settle() since a single whenStable() flush isn't always enough for a stubbed service chain.
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

  function groupNameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="groupName"]')!;
  }

  // The create-group form is always rendered last in the template (after the groups @for loop),
  // so it's reliably the last <form> in document order regardless of which per-group panels
  // (invite / children) happen to be expanded elsewhere in the tree.
  function createGroupForm(compiled: HTMLElement): HTMLFormElement {
    const forms = compiled.querySelectorAll('form');
    return forms[forms.length - 1];
  }

  function addGroupButton(compiled: HTMLElement): HTMLButtonElement {
    return createGroupForm(compiled).querySelector<HTMLButtonElement>('button[type="submit"]')!;
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

  // [ngValue] options render their DOM `value` attribute as an internal "index: value" token
  // (Angular's SelectControlValueAccessor bookkeeping for non-string bindings), so selecting by
  // the visible option label -- as the other ngValue-select specs in this app do -- is the only
  // reliable way to drive these selects from a test.
  function selectByLabel(select: HTMLSelectElement, label: string): void {
    const index = Array.from(select.options).findIndex(
      (option) => option.textContent?.trim() === label,
    );
    expect(index, `option "${label}" not found`).toBeGreaterThanOrEqual(0);
    select.selectedIndex = index;
    select.dispatchEvent(new Event('change'));
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

  // Panel headings (<h4>) share their copy with the toggle buttons ("Calendar permissions", "Add a
  // child", ...), so checking for the heading element is how these tests tell an open panel apart.
  function hasPanelHeading(compiled: HTMLElement, text: string): boolean {
    return Array.from(compiled.querySelectorAll('h4')).some(
      (heading) => heading.textContent?.trim() === text,
    );
  }

  function headingTexts(compiled: HTMLElement, selector: string): string[] {
    return Array.from(compiled.querySelectorAll(selector)).map(
      (heading) => heading.textContent?.trim() ?? '',
    );
  }

  async function openPanel(fixture: ComponentFixture<ManageGroups>, buttonText: string) {
    findButtonByText(fixture.nativeElement as HTMLElement, buttonText)!.click();
    await settle(fixture);
  }

  // ----- Panel loading errors -----
  // Each of these opens a panel whose data comes from a single rejected service call and asserts
  // the resulting error copy -- otherwise-independent panels (invites, members, add-child,
  // calendar policy, mealplan policy) that all follow the exact same open-panel-and-fail shape.
  it.each([
    {
      description: 'shows an error when loading invites fails',
      buttonText: 'Invite',
      override: { listInvites: vi.fn(async () => Promise.reject(new Error('boom'))) },
      message: 'Unable to load invites.',
    },
    {
      description: 'shows an error when loading the members panel fails',
      buttonText: 'Members',
      override: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
      message: 'Unable to load group members.',
    },
    {
      description: 'shows an error when loading members fails',
      buttonText: 'Add a child',
      override: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
      message: 'Unable to load group members.',
    },
    {
      description: 'shows an error when loading the calendar policy fails',
      buttonText: 'Calendar permissions',
      override: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
      message: 'Unable to load calendar permissions.',
    },
    {
      description: 'shows an error when loading the mealplan policy fails',
      buttonText: 'Meal plan permissions',
      override: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
      message: 'Unable to load meal plan permissions.',
    },
  ])('$description', async ({ buttonText, override, message }) => {
    const { fixture } = await setup({ groups: override });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, buttonText)!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(message);
  });

  // ----- Groups list: loading / empty / error -----

  it('shows a loading message before the groups list resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading groups');
  });

  it('shows the empty state once loading finishes with no groups', async () => {
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => []) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No groups yet. Create one below.');
  });

  it('shows an error message when loading groups fails', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load groups.');
  });

  it('renders each group with its name and role', async () => {
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [
          group({ id: 'g1', name: 'Home', role: 0 }),
          group({ id: 'g2', name: 'Weekend', role: 2 }),
        ]),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Home');
    expect(compiled.textContent).toContain('Weekend');
    expect(compiled.textContent).toContain('Owner');
    expect(compiled.textContent).toContain('Member');
  });

  it('hides the manage buttons for a group where the caller is only a member', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Invite')).toBeUndefined();
  });

  it('shows the manage buttons for a group where the caller is an admin', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ role: 1 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Invite')).toBeTruthy();
  });

  // ----- Create-group form -----

  it('keeps the add-group button disabled until a name is entered', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(addGroupButton(compiled).disabled).toBe(true);
  });

  it('keeps the add-group button disabled for a whitespace-only name', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(groupNameInput(compiled), '   ');
    await settle(fixture);

    expect(addGroupButton(compiled).disabled).toBe(true);
  });

  it('enables the add-group button once a name is entered', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(groupNameInput(compiled), 'New House');
    await settle(fixture);

    expect(addGroupButton(compiled).disabled).toBe(false);
  });

  it('submits the trimmed group name, reloads the list, and clears the input', async () => {
    const listMyGroups = vi.fn(async () => [group()]);
    const { fixture, groups } = await setup({ groups: { listMyGroups } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(groupNameInput(compiled), '  New House  ');
    await settle(fixture);

    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(groups.createGroup).toHaveBeenCalledWith({ name: 'New House' });
    expect(listMyGroups).toHaveBeenCalledTimes(2);
    expect(groupNameInput(compiled).value).toBe('');
  });

  it('does not call createGroup when the name is empty', async () => {
    const { fixture, groups } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(groups.createGroup).not.toHaveBeenCalled();
  });

  it('shows an error and keeps the typed name when creating a group fails', async () => {
    const { fixture } = await setup({
      groups: { createGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(groupNameInput(compiled), 'New House');
    await settle(fixture);

    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to create the group.');
    expect(groupNameInput(compiled).value).toBe('New House');
  });

  // ----- Invite flow -----

  it('loads and shows pending invites when the invite panel is opened', async () => {
    const invites = [invite({ email: 'pending@buddy.test' })];
    const { fixture, groups } = await setup({
      groups: { listInvites: vi.fn(async () => invites) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    expect(groups.listInvites).toHaveBeenCalledWith('group-1');
    expect(compiled.textContent).toContain('pending@buddy.test');
  });

  it('shows the pending-empty message when a group has no invites', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('No pending invites.');
  });

  it('collapses the invite panel on a second click without reloading invites', async () => {
    const listInvites = vi.fn(async () => []);
    const { fixture } = await setup({ groups: { listInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggle = findButtonByText(compiled, 'Invite')!;
    toggle.click();
    await settle(fixture);
    expect(listInvites).toHaveBeenCalledTimes(1);

    findButtonByText(compiled, 'Close')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('No pending invites.');
    expect(listInvites).toHaveBeenCalledTimes(1);
  });

  it('sends an invite with the entered email and selected role, then reloads invites and clears the email', async () => {
    const listInvites = vi.fn(async () => []);
    const { fixture, groups } = await setup({ groups: { listInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    const emailInput = compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!;
    setInputValue(emailInput, 'friend@buddy.test');
    const roleSelect = compiled.querySelector<HTMLSelectElement>('select[name="inviteRole"]')!;
    selectByLabel(roleSelect, 'Admin');
    await settle(fixture);

    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(groups.inviteToGroup).toHaveBeenCalledWith('group-1', {
      email: 'friend@buddy.test',
      role: 1,
    });
    expect(listInvites).toHaveBeenCalledTimes(2);
    expect(emailInput.value).toBe('');
  });

  it('only offers Admin and Member as invitable roles, never Owner', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    const roleSelect = compiled.querySelector<HTMLSelectElement>('select[name="inviteRole"]')!;
    const labels = Array.from(roleSelect.options).map((option) => option.textContent?.trim());
    expect(labels).toEqual(['Admin', 'Member']);
  });

  it('keeps the send-invite button disabled until an email is entered', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Send invite')?.disabled).toBe(true);
  });

  it('shows an error when sending an invite fails', async () => {
    const { fixture } = await setup({
      groups: { inviteToGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    const emailInput = compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!;
    setInputValue(emailInput, 'friend@buddy.test');
    await settle(fixture);

    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to send the invite.');
  });

  it('revokes an invite and reloads the invite list', async () => {
    const pendingInvite = invite({ id: 'invite-9', email: 'pending@buddy.test' });
    const listInvites = vi.fn(async () => [pendingInvite]);
    const { fixture, groups } = await setup({ groups: { listInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(groups.revokeInvite).toHaveBeenCalledWith('group-1', 'invite-9');
    expect(listInvites).toHaveBeenCalledTimes(2);
  });

  it('shows an error when revoking an invite fails', async () => {
    const pendingInvite = invite({ id: 'invite-9' });
    const { fixture } = await setup({
      groups: {
        listInvites: vi.fn(async () => [pendingInvite]),
        revokeInvite: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to cancel the invite.');
  });

  it('disables the revoke button for the invite currently being revoked', async () => {
    const pendingInvite = invite({ id: 'invite-9' });
    let resolveRevoke!: () => void;
    const revokeInvite = vi.fn(() => new Promise<void>((resolve) => (resolveRevoke = resolve)));
    const { fixture } = await setup({
      groups: { listInvites: vi.fn(async () => [pendingInvite]), revokeInvite },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Cancel')?.disabled).toBe(true);

    resolveRevoke();
    await settle(fixture);
  });

  // ----- Members panel -----

  it('is available to a plain member, not just owners/admins', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Members')).toBeTruthy();
  });

  it('shows the empty-members message when a group has no members', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Members')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('This group has no members yet.');
  });

  it('lists guardians and children under separate headings, each with name and role', async () => {
    const { fixture } = await setup({
      groups: {
        getGroup: vi.fn(async () =>
          groupDetail({
            members: [
              member({
                userId: 'owner-1',
                givenName: 'Jamie',
                familyName: 'Adult',
                role: 0,
                isChild: false,
              }),
              member({
                userId: 'child-1',
                givenName: 'Sam',
                familyName: 'Kid',
                role: 2,
                isChild: true,
              }),
            ],
          }),
        ),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Members')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Guardians');
    expect(compiled.textContent).toContain('Jamie Adult');
    expect(compiled.textContent).toContain('Children');
    expect(compiled.textContent).toContain('Sam Kid');
  });

  it('collapses the members panel on a second click without reloading', async () => {
    const getGroup = vi.fn(async () => groupDetail({ members: [member()] }));
    const { fixture } = await setup({ groups: { getGroup } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggle = findButtonByText(compiled, 'Members')!;
    toggle.click();
    await settle(fixture);
    expect(getGroup).toHaveBeenCalledTimes(1);
    expect(compiled.textContent).toContain('Sam Kid');

    findButtonByText(compiled, 'Close')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Sam Kid');
    expect(getGroup).toHaveBeenCalledTimes(1);
  });

  // ----- Children / members flow -----

  it('shows the empty-candidates message when every child is already a member', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: {
        getGroup: vi.fn(async () =>
          groupDetail({
            members: [
              member({ userId: 'child-1', givenName: 'Sam', familyName: 'Kid', isChild: true }),
            ],
          }),
        ),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a child')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('All of your children are already in this group.');
  });

  it('lists only children who are not already members as add-child candidates', async () => {
    const { fixture } = await setup({
      guardians: {
        listMyChildren: vi.fn(async () => [
          child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } }),
          child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' } }),
        ]),
      },
      groups: {
        getGroup: vi.fn(async () =>
          groupDetail({
            members: [
              member({ userId: 'child-1', givenName: 'Sam', familyName: 'Kid', isChild: true }),
            ],
          }),
        ),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a child')!.click();
    await settle(fixture);

    const options = Array.from(
      compiled.querySelectorAll<HTMLOptionElement>('select[name="selectedChild"] option'),
    );
    const names = options.map((option) => option.textContent?.trim());
    expect(names).not.toContain('Sam Kid');
    expect(names).toContain('Ada Kid');
  });

  // Each of these opens a panel, picks a new value from its first/only relevant select, submits,
  // and asserts the service call the submit is expected to trigger -- otherwise-independent panels
  // (add-child, calendar policy, mealplan policy) that all follow the same pick-and-submit shape.
  it.each([
    {
      description: 'adds the selected child to the group and reloads members',
      run: async () => {
        const getGroup = vi.fn(async () => groupDetail({ members: [] }));
        const { fixture, groups } = await setup({
          guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
          groups: { getGroup },
        });
        await settle(fixture);

        const compiled = fixture.nativeElement as HTMLElement;
        findButtonByText(compiled, 'Add a child')!.click();
        await settle(fixture);

        const select = compiled.querySelector<HTMLSelectElement>('select[name="selectedChild"]')!;
        selectByLabel(select, 'Sam Kid');
        await settle(fixture);

        compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
        await settle(fixture);

        expect(groups.addChildToGroup).toHaveBeenCalledWith('group-1', 'child-1');
        expect(getGroup).toHaveBeenCalledTimes(2);
      },
    },
    {
      description: 'saves the calendar policy with the edited role value',
      run: async () => {
        const { fixture, groups } = await setup();
        await settle(fixture);

        const compiled = fixture.nativeElement as HTMLElement;
        findButtonByText(compiled, 'Calendar permissions')!.click();
        await settle(fixture);

        // The first select in the policy grid is the Owner row (policyRows[0] = { key: 'Owner' }).
        const ownerSelect = compiled.querySelectorAll('select')[0];
        selectByLabel(ownerSelect, 'Viewer'); // calendarRoles = [0, 1, 2] -> Viewer is CalendarRole 2.
        await settle(fixture);

        findButtonByText(compiled, 'Save permissions')!.click();
        await settle(fixture);

        expect(groups.updateCalendarPermissionPolicy).toHaveBeenCalledWith('group-1', {
          ...calendarPolicy,
          Owner: 2,
        });
      },
    },
    {
      description: 'saves the mealplan policy with the edited tier value',
      run: async () => {
        const { fixture, groups } = await setup();
        await settle(fixture);

        const compiled = fixture.nativeElement as HTMLElement;
        findButtonByText(compiled, 'Meal plan permissions')!.click();
        await settle(fixture);

        const ownerSelect = compiled.querySelectorAll('select')[0];
        selectByLabel(ownerSelect, 'Full access'); // mealplanTiers = [0, 3, 2] -> "Full access" is tier 2 (Manage).
        await settle(fixture);

        findButtonByText(compiled, 'Save permissions')!.click();
        await settle(fixture);

        expect(groups.updateMealplanPermissionPolicy).toHaveBeenCalledWith('group-1', {
          ...mealplanPolicy,
          Owner: 2,
        });
      },
    },
  ])('$description', async ({ run }) => {
    await run();
  });

  it('shows an error when adding a child fails', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: {
        getGroup: vi.fn(async () => groupDetail({ members: [] })),
        addChildToGroup: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a child')!.click();
    await settle(fixture);

    const select = compiled.querySelector<HTMLSelectElement>('select[name="selectedChild"]')!;
    selectByLabel(select, 'Sam Kid');
    await settle(fixture);

    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to add this child to the group.');
  });

  // ----- Calendar permission policy -----

  it('loads the calendar policy draft when the policy panel is opened', async () => {
    const { fixture, groups } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Calendar permissions')!.click();
    await settle(fixture);

    expect(groups.getGroup).toHaveBeenCalledWith('group-1');
    // three role rows (Owner/Admin/Member), each with a select bound to the draft.
    expect(compiled.querySelectorAll('select').length).toBeGreaterThanOrEqual(3);
  });

  it('shows an error when saving the calendar policy fails', async () => {
    const { fixture } = await setup({
      groups: {
        updateCalendarPermissionPolicy: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Calendar permissions')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Save permissions')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save calendar permissions.');
  });

  it('clears the policy draft when the policy panel is collapsed', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggle = findButtonByText(compiled, 'Calendar permissions')!;
    toggle.click();
    await settle(fixture);
    expect(compiled.querySelectorAll('select').length).toBeGreaterThanOrEqual(3);

    findButtonByText(compiled, 'Close')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Save permissions')).toBeUndefined();
  });

  // ----- Mealplan permission policy -----

  it('loads the mealplan policy draft when its panel is opened', async () => {
    const { fixture, groups } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Meal plan permissions')!.click();
    await settle(fixture);

    expect(groups.getGroup).toHaveBeenCalledWith('group-1');
    expect(findButtonByText(compiled, 'Save permissions')).toBeTruthy();
  });

  it('only offers None, Manage, and View mealplan tiers, never Rate', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Meal plan permissions')!.click();
    await settle(fixture);

    const firstSelect = compiled.querySelectorAll('select')[0];
    // mealplanTiers = [0, 3, 2] -> None, View, Manage; Rate (1, the child's own tier) is excluded.
    const labels = Array.from(firstSelect.options).map((option) => option.textContent?.trim());
    expect(labels).toEqual(['No access', 'Read only', 'Full access']);
  });

  it('shows an error when saving the mealplan policy fails', async () => {
    const { fixture } = await setup({
      groups: {
        updateMealplanPermissionPolicy: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Meal plan permissions')!.click();
    await settle(fixture);

    findButtonByText(compiled, 'Save permissions')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save meal plan permissions.');
  });

  // ----- myChildren load failure is silent -----

  it('does not surface an error when loading the guardian’s own children fails', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Unable to load');
  });

  // ----- In-flight and retry states -----

  it('disables the add-group button while the group is being created', async () => {
    const pending = deferred<GroupSummary>();
    const { fixture } = await setup({ groups: { createGroup: vi.fn(() => pending.promise) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(groupNameInput(compiled), 'New House');
    await settle(fixture);
    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(addGroupButton(compiled).disabled).toBe(true);

    pending.resolve(group({ id: 'group-new', name: 'New House' }));
    await settle(fixture);
  });

  it('re-enables the add-group button after a failure and clears the error on retry', async () => {
    const pending = deferred<GroupSummary>();
    const createGroup = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockReturnValueOnce(pending.promise);
    const { fixture } = await setup({ groups: { createGroup } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(groupNameInput(compiled), 'New House');
    await settle(fixture);
    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to create the group.');
    expect(addGroupButton(compiled).disabled).toBe(false);

    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Unable to create the group.');

    pending.resolve(group({ id: 'group-new', name: 'New House' }));
    await settle(fixture);
  });

  it('clears the groups load error once the reload after creating a group succeeds', async () => {
    const listMyGroups = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue([group({ name: 'New House' })]);
    const { fixture } = await setup({ groups: { listMyGroups } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load groups.');

    setInputValue(groupNameInput(compiled), 'New House');
    await settle(fixture);
    createGroupForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Unable to load groups.');
    expect(compiled.querySelector('li')?.textContent).toContain('New House');
  });

  it('enables the send-invite button once an email is entered', async () => {
    const { fixture } = await setup();
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(
      compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!,
      'a@b.test',
    );
    await settle(fixture);

    expect(findButtonByText(compiled, 'Send invite')?.disabled).toBe(false);
  });

  it('sends the trimmed email with the default Member role', async () => {
    const { fixture, groups } = await setup();
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(
      compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!,
      '  friend@buddy.test  ',
    );
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(groups.inviteToGroup).toHaveBeenCalledExactlyOnceWith('group-1', {
      email: 'friend@buddy.test',
      role: 2,
    });
  });

  // jsdom strips surrounding whitespace from type="email" input values, so the test above can't
  // prove the trim in sendInvite itself; set the signal directly to reach it.
  interface ManageGroupsInternals {
    inviteEmail: WritableSignal<string>;
    sendInvite(groupId: string): Promise<void>;
  }

  it('trims the invite email itself before sending it', async () => {
    const { fixture, groups } = await setup();
    await settle(fixture);
    await openPanel(fixture, 'Invite');
    const internals = fixture.componentInstance as unknown as ManageGroupsInternals;

    internals.inviteEmail.set('  friend@buddy.test  ');
    await internals.sendInvite('group-1');

    expect(groups.inviteToGroup).toHaveBeenCalledExactlyOnceWith('group-1', {
      email: 'friend@buddy.test',
      role: 2,
    });
  });

  it('does not send an invite for a blank email', async () => {
    const { fixture, groups } = await setup();
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!, '   ');
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(groups.inviteToGroup).not.toHaveBeenCalled();
  });

  it('re-enables sending after a failure, then disables it and clears the error while retrying', async () => {
    const pending = deferred<GroupInvite>();
    const inviteToGroup = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockReturnValueOnce(pending.promise);
    const { fixture } = await setup({ groups: { inviteToGroup } });
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(
      compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!,
      'a@b.test',
    );
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to send the invite.');
    expect(findButtonByText(compiled, 'Send invite')?.disabled).toBe(false);

    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(findButtonByText(compiled, 'Send invite')?.disabled).toBe(true);
    expect(compiled.textContent).not.toContain('Unable to send the invite.');

    pending.resolve(invite());
    await settle(fixture);
  });

  it('resets the role and the send error when the invite panel is reopened', async () => {
    const inviteToGroup = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue(invite());
    const { fixture } = await setup({ groups: { inviteToGroup } });
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(
      compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!,
      'a@b.test',
    );
    selectByLabel(compiled.querySelector<HTMLSelectElement>('select[name="inviteRole"]')!, 'Admin');
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to send the invite.');

    await openPanel(fixture, 'Close');
    await openPanel(fixture, 'Invite');

    expect(compiled.textContent).not.toContain('Unable to send the invite.');

    setInputValue(
      compiled.querySelector<HTMLInputElement>('input[name="inviteEmail"]')!,
      'b@b.test',
    );
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(inviteToGroup).toHaveBeenLastCalledWith('group-1', { email: 'b@b.test', role: 2 });
  });

  it('shows the invites loading message while invites load', async () => {
    const pending = deferred<GroupInvite[]>();
    const { fixture } = await setup({ groups: { listInvites: vi.fn(() => pending.promise) } });
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading invites…');

    pending.resolve([]);
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Loading invites…');
    expect(compiled.textContent).toContain('No pending invites.');
  });

  it('clears the invites load error when the panel is reopened and the invites load', async () => {
    const listInvites = vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]);
    const { fixture } = await setup({ groups: { listInvites } });
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load invites.');

    await openPanel(fixture, 'Close');
    await openPanel(fixture, 'Invite');

    expect(compiled.textContent).not.toContain('Unable to load invites.');
    expect(compiled.textContent).toContain('No pending invites.');
  });

  it('re-enables cancel after a failed revoke and clears the error while retrying', async () => {
    const pending = deferred<void>();
    const revokeInvite = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockReturnValueOnce(pending.promise);
    const { fixture } = await setup({
      groups: { listInvites: vi.fn(async () => [invite({ id: 'invite-9' })]), revokeInvite },
    });
    await settle(fixture);
    await openPanel(fixture, 'Invite');

    const compiled = fixture.nativeElement as HTMLElement;
    await openPanel(fixture, 'Cancel');
    expect(compiled.textContent).toContain('Unable to cancel the invite.');
    expect(findButtonByText(compiled, 'Cancel')?.disabled).toBe(false);

    await openPanel(fixture, 'Cancel');

    expect(compiled.textContent).not.toContain('Unable to cancel the invite.');

    pending.resolve();
    await settle(fixture);
  });

  it('collapses the add-child panel on a second click without reloading members', async () => {
    const getGroup = vi.fn(async () => groupDetail());
    const { fixture } = await setup({ groups: { getGroup } });
    await settle(fixture);
    await openPanel(fixture, 'Add a child');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(hasPanelHeading(compiled, 'Add a child')).toBe(true);

    await openPanel(fixture, 'Close');

    expect(hasPanelHeading(compiled, 'Add a child')).toBe(false);
    expect(findButtonByText(compiled, 'Add a child')).toBeTruthy();
    expect(getGroup).toHaveBeenCalledTimes(1);
  });

  it('enables the add-child button only once a child is selected', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
    });
    await settle(fixture);
    await openPanel(fixture, 'Add a child');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Add to group')?.disabled).toBe(true);

    selectByLabel(
      compiled.querySelector<HTMLSelectElement>('select[name="selectedChild"]')!,
      'Sam Kid',
    );
    await settle(fixture);

    expect(findButtonByText(compiled, 'Add to group')?.disabled).toBe(false);
  });

  it('does not add a child when none is selected', async () => {
    const { fixture, groups } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
    });
    await settle(fixture);
    await openPanel(fixture, 'Add a child');

    const compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(groups.addChildToGroup).not.toHaveBeenCalled();
  });

  it('resets the child selection after a child is added', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
    });
    await settle(fixture);
    await openPanel(fixture, 'Add a child');

    const compiled = fixture.nativeElement as HTMLElement;
    selectByLabel(
      compiled.querySelector<HTMLSelectElement>('select[name="selectedChild"]')!,
      'Sam Kid',
    );
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(findButtonByText(compiled, 'Add to group')?.disabled).toBe(true);
  });

  it('re-enables adding after a failure, then disables it and clears the error while retrying', async () => {
    const pending = deferred<void>();
    const addChildToGroup = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockReturnValueOnce(pending.promise);
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: { addChildToGroup },
    });
    await settle(fixture);
    await openPanel(fixture, 'Add a child');

    const compiled = fixture.nativeElement as HTMLElement;
    selectByLabel(
      compiled.querySelector<HTMLSelectElement>('select[name="selectedChild"]')!,
      'Sam Kid',
    );
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to add this child to the group.');
    expect(findButtonByText(compiled, 'Add to group')?.disabled).toBe(false);

    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(findButtonByText(compiled, 'Add to group')?.disabled).toBe(true);
    expect(compiled.textContent).not.toContain('Unable to add this child to the group.');

    pending.resolve();
    await settle(fixture);
  });

  it('resets the selection and the add error when the add-child panel is reopened', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: { addChildToGroup: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);
    await openPanel(fixture, 'Add a child');

    const compiled = fixture.nativeElement as HTMLElement;
    selectByLabel(
      compiled.querySelector<HTMLSelectElement>('select[name="selectedChild"]')!,
      'Sam Kid',
    );
    await settle(fixture);
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to add this child to the group.');

    await openPanel(fixture, 'Close');
    await openPanel(fixture, 'Add a child');

    expect(compiled.textContent).not.toContain('Unable to add this child to the group.');
    expect(findButtonByText(compiled, 'Add to group')?.disabled).toBe(true);
  });

  it('shows the members loading message while members load', async () => {
    const pending = deferred<GroupDetail>();
    const { fixture } = await setup({ groups: { getGroup: vi.fn(() => pending.promise) } });
    await settle(fixture);
    await openPanel(fixture, 'Members');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading members…');

    pending.resolve(groupDetail());
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Loading members…');
    expect(compiled.textContent).toContain('This group has no members yet.');
  });

  it('clears the members load error when the panel is reopened and the members load', async () => {
    const getGroup = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue(groupDetail());
    const { fixture } = await setup({ groups: { getGroup } });
    await settle(fixture);
    await openPanel(fixture, 'Members');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load group members.');

    await openPanel(fixture, 'Close');
    await openPanel(fixture, 'Members');

    expect(compiled.textContent).not.toContain('Unable to load group members.');
    expect(compiled.textContent).toContain('This group has no members yet.');
  });

  it.each([
    { kind: 'only children', isChild: true, headings: ['Children'] },
    { kind: 'only guardians', isChild: false, headings: ['Guardians'] },
  ])('shows just the matching heading when a group has $kind', async ({ isChild, headings }) => {
    const { fixture } = await setup({
      groups: { getGroup: vi.fn(async () => groupDetail({ members: [member({ isChild })] })) },
    });
    await settle(fixture);
    await openPanel(fixture, 'Members');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(headingTexts(compiled, 'h5')).toEqual(headings);
    expect(compiled.querySelectorAll('li li')).toHaveLength(1);
  });

  it('labels the calendar policy options Owner, Contributor and Viewer', async () => {
    const { fixture } = await setup();
    await settle(fixture);
    await openPanel(fixture, 'Calendar permissions');

    const compiled = fixture.nativeElement as HTMLElement;
    const labels = Array.from(compiled.querySelectorAll('select')[0].options).map((option) =>
      option.textContent?.trim(),
    );
    expect(labels).toEqual(['Owner', 'Contributor', 'Viewer']);
  });

  // The calendar and mealplan policy panels share the same open/load/save/close shape.
  describe.each([
    {
      panel: 'Calendar permissions',
      loadError: 'Unable to load calendar permissions.',
      saveError: 'Unable to save calendar permissions.',
      update: 'updateCalendarPermissionPolicy' as const,
    },
    {
      panel: 'Meal plan permissions',
      loadError: 'Unable to load meal plan permissions.',
      saveError: 'Unable to save meal plan permissions.',
      update: 'updateMealplanPermissionPolicy' as const,
    },
  ])('$panel panel', ({ panel, loadError, saveError, update }) => {
    it('shows the loading message while the policy loads', async () => {
      const pending = deferred<GroupDetail>();
      const { fixture } = await setup({ groups: { getGroup: vi.fn(() => pending.promise) } });
      await settle(fixture);
      await openPanel(fixture, panel);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Loading permissions…');
      expect(findButtonByText(compiled, 'Save permissions')).toBeUndefined();

      pending.resolve(groupDetail());
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Loading permissions…');
      expect(findButtonByText(compiled, 'Save permissions')).toBeTruthy();
    });

    it('closes the panel on a second click', async () => {
      const { fixture } = await setup();
      await settle(fixture);
      await openPanel(fixture, panel);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(hasPanelHeading(compiled, panel)).toBe(true);

      await openPanel(fixture, 'Close');

      expect(hasPanelHeading(compiled, panel)).toBe(false);
      expect(findButtonByText(compiled, panel)).toBeTruthy();
    });

    it('clears the load error when the panel is reopened and the policy loads', async () => {
      const getGroup = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockResolvedValue(groupDetail());
      const { fixture } = await setup({ groups: { getGroup } });
      await settle(fixture);
      await openPanel(fixture, panel);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain(loadError);

      await openPanel(fixture, 'Close');
      await openPanel(fixture, panel);

      expect(compiled.textContent).not.toContain(loadError);
      expect(findButtonByText(compiled, 'Save permissions')).toBeTruthy();
    });

    it('clears the save error when the panel is reopened', async () => {
      const { fixture } = await setup({
        groups: { [update]: vi.fn(async () => Promise.reject(new Error('boom'))) },
      });
      await settle(fixture);
      await openPanel(fixture, panel);
      await openPanel(fixture, 'Save permissions');

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain(saveError);

      await openPanel(fixture, 'Close');
      await openPanel(fixture, panel);

      expect(compiled.textContent).not.toContain(saveError);
    });

    it('re-enables saving after a failure, then disables it and clears the error while retrying', async () => {
      const pending = deferred<void>();
      const save = vi
        .fn()
        .mockRejectedValueOnce(new Error('boom'))
        .mockReturnValueOnce(pending.promise);
      const { fixture } = await setup({ groups: { [update]: save } });
      await settle(fixture);
      await openPanel(fixture, panel);
      await openPanel(fixture, 'Save permissions');

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain(saveError);
      expect(findButtonByText(compiled, 'Save permissions')?.disabled).toBe(false);

      await openPanel(fixture, 'Save permissions');

      expect(findButtonByText(compiled, 'Save permissions')?.disabled).toBe(true);
      expect(compiled.textContent).not.toContain(saveError);

      pending.resolve();
      await settle(fixture);

      expect(findButtonByText(compiled, 'Save permissions')?.disabled).toBe(false);
    });
  });
});
