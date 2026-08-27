import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  CalendarPermissionPolicy,
  GroupDetail,
  GroupInvite,
  GroupSummary,
  GroupsService,
  MealplanPermissionPolicy
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
      ...overrides
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
      ...overrides
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
      ...overrides
    };
  }

  interface Stubs {
    groups?: Partial<GroupsService>;
    guardians?: Partial<GuardiansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => [group()]),
      createGroup: vi.fn(async (request) => ({ id: 'group-new', name: request.name, role: 0 }) as GroupSummary),
      listInvites: vi.fn(async () => []),
      inviteToGroup: vi.fn(async (_groupId, request) => invite({ email: request.email, role: request.role })),
      revokeInvite: vi.fn(async () => undefined),
      addChildToGroup: vi.fn(async () => undefined),
      getGroup: vi.fn(async () => groupDetail()),
      updateCalendarPermissionPolicy: vi.fn(async () => undefined),
      updateMealplanPermissionPolicy: vi.fn(async () => undefined),
      ...stubs.groups
    };
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      ...stubs.guardians
    };

    await TestBed.configureTestingModule({
      imports: [ManageGroups],
      providers: [
        { provide: GroupsService, useValue: groupsStub },
        { provide: GuardiansService, useValue: guardiansStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageGroups);

    return { fixture, groups: groupsStub, guardians: guardiansStub };
  }

  // loadGroups/loadMyChildren/loadInvites/etc. each chain at least one await before the signals
  // driving the template settle, and some flows (createGroup -> loadGroups, sendInvite ->
  // loadInvites) chain two mocked service calls back to back -- mirrors tasks-today.spec.ts's
  // settle() since a single whenStable() flush isn't always enough for a stubbed service chain.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
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
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
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
    const index = Array.from(select.options).findIndex((option) => option.textContent?.trim() === label);
    expect(index, `option "${label}" not found`).toBeGreaterThanOrEqual(0);
    select.selectedIndex = index;
    select.dispatchEvent(new Event('change'));
  }

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
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load groups.');
  });

  it('renders each group with its name and role', async () => {
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [group({ id: 'g1', name: 'Home', role: 0 }), group({ id: 'g2', name: 'Weekend', role: 2 })]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Home');
    expect(compiled.textContent).toContain('Weekend');
    expect(compiled.textContent).toContain('Owner');
    expect(compiled.textContent).toContain('Member');
  });

  it('hides the manage buttons for a group where the caller is only a member', async () => {
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [group({ role: 2 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Invite')).toBeUndefined();
  });

  it('shows the manage buttons for a group where the caller is an admin', async () => {
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [group({ role: 1 })]) } });
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
    const { fixture } = await setup({ groups: { createGroup: vi.fn(async () => Promise.reject(new Error('boom'))) } });
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
    const { fixture, groups } = await setup({ groups: { listInvites: vi.fn(async () => invites) } });
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

  it('shows an error when loading invites fails', async () => {
    const { fixture } = await setup({ groups: { listInvites: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Invite')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to load invites.');
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

    expect(groups.inviteToGroup).toHaveBeenCalledWith('group-1', { email: 'friend@buddy.test', role: 1 });
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
    const { fixture } = await setup({ groups: { inviteToGroup: vi.fn(async () => Promise.reject(new Error('boom'))) } });
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
      groups: { listInvites: vi.fn(async () => [pendingInvite]), revokeInvite: vi.fn(async () => Promise.reject(new Error('boom'))) }
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
    const { fixture } = await setup({ groups: { listInvites: vi.fn(async () => [pendingInvite]), revokeInvite } });
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

  // ----- Children / members flow -----

  it('shows the empty-candidates message when every child is already a member', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: { getGroup: vi.fn(async () => groupDetail({ members: [{ userId: 'child-1', role: 2 }] })) }
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
          child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' } })
        ])
      },
      groups: { getGroup: vi.fn(async () => groupDetail({ members: [{ userId: 'child-1', role: 2 }] })) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a child')!.click();
    await settle(fixture);

    const options = Array.from(compiled.querySelectorAll<HTMLOptionElement>('select[name="selectedChild"] option'));
    const names = options.map((option) => option.textContent?.trim());
    expect(names).not.toContain('Sam Kid');
    expect(names).toContain('Ada Kid');
  });

  it('shows an error when loading members fails', async () => {
    const { fixture } = await setup({ groups: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a child')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to load group members.');
  });

  it('adds the selected child to the group and reloads members', async () => {
    const getGroup = vi.fn(async () => groupDetail({ members: [] }));
    const { fixture, groups } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: { getGroup }
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
  });

  it('shows an error when adding a child fails', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      groups: { getGroup: vi.fn(async () => groupDetail({ members: [] })), addChildToGroup: vi.fn(async () => Promise.reject(new Error('boom'))) }
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

  it('shows an error when loading the calendar policy fails', async () => {
    const { fixture } = await setup({ groups: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Calendar permissions')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to load calendar permissions.');
  });

  it('saves the calendar policy with the edited role value', async () => {
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

    expect(groups.updateCalendarPermissionPolicy).toHaveBeenCalledWith('group-1', { ...calendarPolicy, Owner: 2 });
  });

  it('shows an error when saving the calendar policy fails', async () => {
    const { fixture } = await setup({ groups: { updateCalendarPermissionPolicy: vi.fn(async () => Promise.reject(new Error('boom'))) } });
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

  it('saves the mealplan policy with the edited tier value', async () => {
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

    expect(groups.updateMealplanPermissionPolicy).toHaveBeenCalledWith('group-1', { ...mealplanPolicy, Owner: 2 });
  });

  it('shows an error when loading the mealplan policy fails', async () => {
    const { fixture } = await setup({ groups: { getGroup: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Meal plan permissions')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to load meal plan permissions.');
  });

  it('shows an error when saving the mealplan policy fails', async () => {
    const { fixture } = await setup({ groups: { updateMealplanPermissionPolicy: vi.fn(async () => Promise.reject(new Error('boom'))) } });
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
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Unable to load');
  });
});
