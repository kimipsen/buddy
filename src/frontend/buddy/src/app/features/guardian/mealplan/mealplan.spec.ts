import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GroupDetail, GroupSummary, GroupsService } from '../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { Meal, MealplansService } from '../../../core/mealplans.service';
import { GuardianMealplan } from './mealplan';

describe('GuardianMealplan', () => {
  const child: ChildSummary = {
    id: 'child-1',
    name: { givenName: 'Kim', familyName: 'Kid' },
    guardianLinkId: 'link-1',
    kind: 0,
    language: 'en',
    timeZoneId: 'UTC'
  };

  function groupSummary(overrides: Partial<GroupSummary> = {}): GroupSummary {
    return { id: 'group-1', name: 'Group One', role: 0, ...overrides };
  }

  function groupDetail(overrides: Partial<GroupDetail> = {}): GroupDetail {
    return {
      id: 'group-1',
      name: 'Group One',
      members: [],
      calendarPermissionPolicy: { Owner: 0, Admin: 0, Member: 0 },
      mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 },
      ...overrides
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    groups?: Partial<GroupsService>;
    mealplans?: Partial<MealplansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = { listMyChildren: vi.fn(async () => [child]), ...stubs.guardians };
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => []),
      getGroup: vi.fn(async () => Promise.reject(new Error('no group detail stubbed'))),
      ...stubs.groups
    };
    const mealplansStub: Partial<MealplansService> = {
      // Real service exposes this as a readonly signal; the real child components rendered here
      // (ManageMeals, AssignMealplan) read it directly via a computed, so it must behave like one.
      meals: signal<Meal[]>([]).asReadonly(),
      listMeals: vi.fn(async () => []),
      listMealPlan: vi.fn(async () => []),
      getSharedGroup: vi.fn(async () => null),
      shareWithGroup: vi.fn(async () => undefined),
      unshareFromGroup: vi.fn(async () => undefined),
      ...stubs.mealplans
    };

    await TestBed.configureTestingModule({
      imports: [GuardianMealplan],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: GroupsService, useValue: groupsStub },
        { provide: MealplansService, useValue: mealplansStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianMealplan);

    return { fixture, guardians: guardiansStub, groups: groupsStub, mealplans: mealplansStub };
  }

  // load() chains guardians.listMyChildren, then a Promise.all of listMyGroups/getSharedGroup,
  // then a further Promise.all of per-group getGroup calls -- more await-depth than a single
  // macrotask flush reliably drains, especially once the real child components' own effects (each
  // with their own chained loads against the same mocked service) are added on top.
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

  it('shows the loading message before the initial load resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading the meal plan…');
  });

  it('shows the no-children message when the guardian has no linked children', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings before planning meals.');
    expect(compiled.querySelector('app-manage-meals')).toBeFalsy();
    expect(compiled.querySelector('app-assign-mealplan')).toBeFalsy();
  });

  it('shows a translated error and still treats the guardian as having children when the initial load fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load the meal plan.');
    // hasChildren defaults to true and is never flipped to false on this failure path (only the
    // empty-children branch inside load() does that) -- so the no-children message must not
    // appear alongside the error.
    expect(compiled.textContent).not.toContain('Link a child from Settings before planning meals.');
    expect(compiled.querySelector('app-manage-meals')).toBeFalsy();
  });

  it('hides the scope toggle when the guardian has no qualifying group scopes', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'My family')).toBeFalsy();
  });

  it('shows the family scope plus qualifying group scopes, badging a View-tier group as read-only', async () => {
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [
          groupSummary({ id: 'group-manage', name: 'Manage Co', role: 0 }),
          groupSummary({ id: 'group-view', name: 'View Co', role: 1 })
        ]),
        getGroup: vi.fn(async (groupId: string) =>
          groupId === 'group-view'
            ? groupDetail({ id: 'group-view', mealplanPermissionPolicy: { Owner: 2, Admin: 3, Member: 0 } })
            : groupDetail({ id: 'group-manage', mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 } })
        )
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const familyButton = findButtonByText(compiled, 'My family');
    expect(familyButton).toBeTruthy();
    expect(familyButton?.classList.contains('bg-slate-950')).toBe(true);

    const manageButton = findButtonByText(compiled, 'Manage Co');
    expect(manageButton).toBeTruthy();

    const viewButton = Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.includes('View Co'));
    expect(viewButton?.textContent).toContain('read only');
    expect(manageButton?.textContent).not.toContain('read only');
  });

  it('switches the selected scope when a group toggle is clicked, and passes the new scope to the child components', async () => {
    const { fixture, mealplans } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-1', name: 'Family Group', role: 0 })]),
        getGroup: vi.fn(async () => groupDetail({ mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 } }))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const familyButton = findButtonByText(compiled, 'My family')!;
    const groupButton = findButtonByText(compiled, 'Family Group')!;
    expect(familyButton.classList.contains('bg-slate-950')).toBe(true);
    expect(groupButton.classList.contains('bg-slate-950')).toBe(false);

    groupButton.click();
    await settle(fixture);

    expect(familyButton.classList.contains('bg-slate-950')).toBe(false);
    expect(groupButton.classList.contains('bg-slate-950')).toBe(true);
    expect(mealplans.listMeals).toHaveBeenCalledWith({ kind: 'group', groupId: 'group-1', groupName: 'Family Group', accessTier: 2 });
  });

  it('excludes a group whose resolved access tier is None or Rate from the scope toggle', async () => {
    const { fixture } = await setup({
      groups: {
        // Member maps to tier 0 (None) in this policy -- below View/Manage, so it should not
        // become a selectable scope even though the guardian belongs to the group.
        listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-member', name: 'Member Co', role: 2 })]),
        getGroup: vi.fn(async () => groupDetail({ mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 } }))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Member Co')).toBeFalsy();
    expect(findButtonByText(compiled, 'My family')).toBeFalsy();
  });

  it('excludes a group whose detail lookup fails, without surfacing an error', async () => {
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-broken', name: 'Broken Co', role: 0 })]),
        getGroup: vi.fn(async () => Promise.reject(new Error('boom')))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Broken Co')).toBeFalsy();
    expect(compiled.textContent).not.toContain('Unable to load the meal plan.');
  });

  it('renders the child components for the default family scope once loaded', async () => {
    const { fixture, mealplans } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-manage-meals')).toBeTruthy();
    expect(compiled.querySelector('app-assign-mealplan')).toBeTruthy();
    expect(mealplans.listMeals).toHaveBeenCalledWith({ kind: 'family', childId: 'child-1' });
  });

  it('offers only groups the guardian owns or administers as share targets, never Member', async () => {
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [
          groupSummary({ id: 'owner-group', name: 'Owner Co', role: 0 }),
          groupSummary({ id: 'admin-group', name: 'Admin Co', role: 1 }),
          groupSummary({ id: 'member-group', name: 'Member Co', role: 2 })
        ])
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const options = Array.from(compiled.querySelectorAll('select[name="shareTargetGroupId"] option')).map((option) =>
      option.textContent?.trim()
    );
    expect(options).toContain('Owner Co');
    expect(options).toContain('Admin Co');
    expect(options).not.toContain('Member Co');
  });

  it('shows the no-manageable-groups message when the guardian owns or administers no group', async () => {
    const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [groupSummary({ role: 2 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Create a group, or become an admin of one, to share this meal plan.');
  });

  it('shares the family plan with the selected group and shows the confirmation', async () => {
    const shareWithGroup = vi.fn(async () => undefined);
    const { fixture, mealplans } = await setup({
      groups: { listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-share', name: 'Sharable Co', role: 0 })]) },
      mealplans: { shareWithGroup }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = compiled.querySelector<HTMLSelectElement>('select[name="shareTargetGroupId"]')!;
    const shareButtonBefore = findButtonByText(compiled, 'Share')!;
    expect(shareButtonBefore.disabled).toBe(true);

    select.value = 'group-share';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    const shareButton = findButtonByText(compiled, 'Share')!;
    expect(shareButton.disabled).toBe(false);
    shareButton.click();
    await settle(fixture);

    expect(shareWithGroup).toHaveBeenCalledWith('child-1', 'group-share');
    expect(mealplans.getSharedGroup).toHaveBeenCalled();
    expect(compiled.textContent).toContain('Shared with Sharable Co.');
    expect(compiled.querySelector('select[name="shareTargetGroupId"]')).toBeFalsy();
  });

  it('shows a translated error and re-enables the share button when sharing fails', async () => {
    const shareWithGroup = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture } = await setup({
      groups: { listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-share', name: 'Sharable Co', role: 0 })]) },
      mealplans: { shareWithGroup }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = compiled.querySelector<HTMLSelectElement>('select[name="shareTargetGroupId"]')!;
    select.value = 'group-share';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    findButtonByText(compiled, 'Share')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to share the meal plan with that group.');
    // Failure doesn't clear the selected target, so the button becomes enabled again once
    // `sharing` resets, without the user having to re-pick a group.
    expect(findButtonByText(compiled, 'Share')?.disabled).toBe(false);
    expect(compiled.querySelector('select[name="shareTargetGroupId"]')).toBeTruthy();
  });

  it('shows the currently shared group and an unshare control', async () => {
    const { fixture } = await setup({
      mealplans: { getSharedGroup: vi.fn(async () => ({ groupId: 'group-1', groupName: 'Family Group' })) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Shared with Family Group.');
    expect(findButtonByText(compiled, 'Stop sharing')).toBeTruthy();
    expect(compiled.querySelector('select[name="shareTargetGroupId"]')).toBeFalsy();
  });

  it('unshares and reverts to the family scope when the unshared group was the selected scope', async () => {
    const unshareFromGroup = vi.fn(async () => undefined);
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-1', name: 'Family Group', role: 0 })]),
        getGroup: vi.fn(async () => groupDetail({ mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 } }))
      },
      mealplans: { getSharedGroup: vi.fn(async () => ({ groupId: 'group-1', groupName: 'Family Group' })), unshareFromGroup }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Family Group')!.click();
    await settle(fixture);
    expect(findButtonByText(compiled, 'Family Group')?.classList.contains('bg-slate-950')).toBe(true);

    findButtonByText(compiled, 'Stop sharing')!.click();
    await settle(fixture);

    expect(unshareFromGroup).toHaveBeenCalledWith('child-1', 'group-1');
    expect(compiled.textContent).not.toContain('Shared with Family Group.');
    // The scope toggle itself survives (group scopes are keyed off permission tier, not share
    // status -- see the groupScopes comment in mealplan.ts), but the selection falls back to the
    // family scope since the group scope that was selected is the one just unshared.
    expect(findButtonByText(compiled, 'Family Group')).toBeTruthy();
    expect(findButtonByText(compiled, 'My family')?.classList.contains('bg-slate-950')).toBe(true);
  });

  it('does not change the selected scope when unsharing a group that was not selected', async () => {
    const unshareFromGroup = vi.fn(async () => undefined);
    const { fixture } = await setup({
      groups: {
        listMyGroups: vi.fn(async () => [groupSummary({ id: 'group-1', name: 'Family Group', role: 0 })]),
        getGroup: vi.fn(async () => groupDetail({ mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 } }))
      },
      mealplans: { getSharedGroup: vi.fn(async () => ({ groupId: 'group-1', groupName: 'Family Group' })), unshareFromGroup }
    });
    await settle(fixture);

    // Selection stays on the default family scope (never clicked the group toggle) while
    // unsharing group-1.
    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Stop sharing')!.click();
    await settle(fixture);

    expect(unshareFromGroup).toHaveBeenCalledWith('child-1', 'group-1');
    expect(findButtonByText(compiled, 'My family')?.classList.contains('bg-slate-950')).toBe(true);
  });

  it('shows a translated error when unsharing fails', async () => {
    const unshareFromGroup = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture } = await setup({
      mealplans: { getSharedGroup: vi.fn(async () => ({ groupId: 'group-1', groupName: 'Family Group' })), unshareFromGroup }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Stop sharing')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to stop sharing the meal plan.');
    // Still marked as shared since the unshare didn't actually succeed.
    expect(compiled.textContent).toContain('Shared with Family Group.');
  });
});
