import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { EMPTY_SETUP, OnboardingSetup } from '../../../../core/onboarding.service';
import {
  buttonByText,
  settle,
  setupWith,
  submit,
  typeInto,
} from '../../../../../testing/onboarding-fixture';
import { GroupStep } from './group-step';

describe('GroupStep', () => {
  async function setup(setupState: OnboardingSetup, groups: Partial<GroupsService> = {}) {
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async (): Promise<GroupSummary[]> => []),
      createGroup: vi.fn(async () => ({ id: 'group-new', name: 'Home', role: 0 as const })),
      ...groups,
    };

    await TestBed.configureTestingModule({
      imports: [GroupStep],
      providers: [{ provide: GroupsService, useValue: groupsStub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(GroupStep);
    fixture.componentRef.setInput('setup', setupState);
    const chosen = vi.fn();
    fixture.componentInstance.groupChosen.subscribe(chosen);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, groups: groupsStub, chosen };
  }

  async function createNamed(fixture: Awaited<ReturnType<typeof setup>>['fixture'], name: string) {
    const compiled = fixture.nativeElement as HTMLElement;
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingGroupName')!, name);
    await settle(fixture);
    submit(compiled);
    await settle(fixture);
  }

  it('creates the group and hands its id on', async () => {
    const { fixture, groups, chosen } = await setup(EMPTY_SETUP);

    await createNamed(fixture, '  Home  ');

    expect(groups.createGroup).toHaveBeenCalledWith({ name: 'Home' });
    expect(chosen).toHaveBeenCalledWith('group-new');
  });

  it('re-sends the group it already created instead of creating a second one', async () => {
    const { fixture, groups, chosen } = await setup(EMPTY_SETUP);

    await createNamed(fixture, 'Home');
    submit(fixture.nativeElement);
    await settle(fixture);

    expect(groups.createGroup).toHaveBeenCalledTimes(1);
    expect(chosen).toHaveBeenCalledTimes(2);
    expect(chosen).toHaveBeenLastCalledWith('group-new');
  });

  it('shows an error when the group cannot be created', async () => {
    const { fixture, compiled, chosen } = await setup(EMPTY_SETUP, {
      createGroup: vi.fn(async () => {
        throw new HttpErrorResponse({ status: 500 });
      }),
    });

    await createNamed(fixture, 'Home');

    expect(compiled.textContent).toContain('Unable to create the group.');
    expect(chosen).not.toHaveBeenCalled();
  });

  it('offers only groups the guardian manages to continue with', async () => {
    const { compiled, chosen } = await setup(EMPTY_SETUP, {
      listMyGroups: vi.fn(async (): Promise<GroupSummary[]> => [
        { id: 'owned', name: 'Owned', role: 0 },
        { id: 'admin', name: 'Admined', role: 1 },
        { id: 'member', name: 'Joined', role: 2 },
      ]),
    });

    expect(buttonByText(compiled, 'Use Joined')).toBeUndefined();
    buttonByText(compiled, 'Use Admined')!.click();

    expect(chosen).toHaveBeenCalledWith('admin');
    expect(buttonByText(compiled, 'Use Owned')).toBeDefined();
  });

  it('explains a setup group that is gone', async () => {
    const { compiled } = await setup({ ...EMPTY_SETUP, groupMissing: true });

    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain('no longer available');
  });

  it('shows the chosen group instead of the form', async () => {
    const { compiled } = await setup(setupWith());

    expect(compiled.textContent).toContain('Your group: The Hansens');
    expect(compiled.querySelector('form')).toBeNull();
  });
});
