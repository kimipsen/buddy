import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { GroupsService } from '../../../../core/groups.service';
import { CreateChildResult, GuardiansService } from '../../../../core/guardians.service';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import {
  buttonByText,
  child,
  settle,
  setupWith,
  submit,
  typeInto,
} from '../../../../../testing/onboarding-fixture';
import { ChildrenStep } from './children-step';

describe('ChildrenStep', () => {
  const created: CreateChildResult = {
    ...child('child-new', 'Emil'),
    username: 'emil',
    temporaryPassword: 'one-time-123',
  };

  async function setup(
    setupState: OnboardingSetup = setupWith(),
    stubs: { guardians?: Partial<GuardiansService>; groups?: Partial<GroupsService> } = {},
  ) {
    const guardians: Partial<GuardiansService> = {
      createChild: vi.fn(async () => created),
      resetChildPassword: vi.fn(async () => ({ username: 'ada', temporaryPassword: 'fresh-456' })),
      ...stubs.guardians,
    };
    const groups: Partial<GroupsService> = {
      addChildToGroup: vi.fn(async () => {}),
      ...stubs.groups,
    };

    await TestBed.configureTestingModule({
      imports: [ChildrenStep],
      providers: [
        { provide: GuardiansService, useValue: guardians },
        { provide: GroupsService, useValue: groups },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildrenStep);
    fixture.componentRef.setInput('setup', setupState);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, guardians, groups, changed };
  }

  async function addChild(fixture: Awaited<ReturnType<typeof setup>>['fixture']) {
    const compiled = fixture.nativeElement as HTMLElement;
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingChildGivenName')!, 'Emil');
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingChildFamilyName')!, 'Hansen');
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingChildUsername')!, 'emil');
    await settle(fixture);
    submit(compiled);
    await settle(fixture);
  }

  it('creates the child, shows the one-time password and adds them to the group', async () => {
    const { fixture, compiled, guardians, groups, changed } = await setup();

    await addChild(fixture);

    expect(guardians.createChild).toHaveBeenCalledWith({
      givenName: 'Emil',
      familyName: 'Hansen',
      username: 'emil',
    });
    expect(groups.addChildToGroup).toHaveBeenCalledWith('group-1', 'child-new');
    expect(compiled.textContent).toContain('Sign-in for Emil');
    expect(compiled.textContent).toContain('one-time-123');
    expect(changed).toHaveBeenCalled();
  });

  it('says so when the username is taken', async () => {
    const { fixture, compiled, groups, changed } = await setup(setupWith(), {
      guardians: {
        createChild: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 409 });
        }),
      },
    });

    await addChild(fixture);

    expect(compiled.textContent).toContain('That username is taken.');
    expect(groups.addChildToGroup).not.toHaveBeenCalled();
    expect(changed).not.toHaveBeenCalled();
  });

  it('keeps the created child when joining the group fails, and reports it', async () => {
    const { fixture, compiled, changed } = await setup(setupWith(), {
      groups: {
        addChildToGroup: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 500 });
        }),
      },
    });

    await addChild(fixture);

    expect(compiled.textContent).toContain('one-time-123');
    expect(compiled.textContent).toContain('Unable to add the child to the group.');
    // The reload lists the child outside the group, where the retry lives.
    expect(changed).toHaveBeenCalled();
  });

  it('retries only the membership of a child outside the group', async () => {
    const { compiled, fixture, guardians, groups, changed } = await setup(
      setupWith({ childrenOutsideGroup: [child('child-2', 'Ida')] }),
    );

    expect(compiled.textContent).toContain('Not in the group yet');
    buttonByText(compiled, 'Add to group')!.click();
    await settle(fixture);

    expect(groups.addChildToGroup).toHaveBeenCalledWith('group-1', 'child-2');
    expect(guardians.createChild).not.toHaveBeenCalled();
    expect(changed).toHaveBeenCalled();
  });

  it('gives a child created before a reload a new one-time password', async () => {
    const { compiled, fixture, guardians } = await setup(setupWith({ children: [child()] }));

    buttonByText(compiled, 'New password')!.click();
    await settle(fixture);

    expect(guardians.resetChildPassword).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('Sign-in for Ada');
    expect(compiled.textContent).toContain('fresh-456');
  });
});
