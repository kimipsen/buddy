import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { GroupsService } from '../../../../core/groups.service';
import { GuardiansService } from '../../../../core/guardians.service';
import {
  buttonByText,
  child,
  settle,
  setupWith,
  submit,
  typeInto,
} from '../../../../../testing/onboarding-fixture';
import { AdultsStep } from './adults-step';

describe('AdultsStep', () => {
  async function setup(
    stubs: { guardians?: Partial<GuardiansService>; groups?: Partial<GroupsService> } = {},
  ) {
    const guardians: Partial<GuardiansService> = {
      inviteGuardian: vi.fn(async () => ({}) as never),
      ...stubs.guardians,
    };
    const groups: Partial<GroupsService> = {
      inviteToGroup: vi.fn(async () => ({}) as never),
      ...stubs.groups,
    };

    await TestBed.configureTestingModule({
      imports: [AdultsStep],
      providers: [
        { provide: GuardiansService, useValue: guardians },
        { provide: GroupsService, useValue: groups },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdultsStep);
    fixture.componentRef.setInput(
      'setup',
      setupWith({ children: [child('c1', 'Ada'), child('c2', 'Emil')] }),
    );
    const changed = vi.fn();
    const skip = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    fixture.componentInstance.skip.subscribe(skip);
    await settle(fixture);

    return {
      fixture,
      compiled: fixture.nativeElement as HTMLElement,
      guardians,
      groups,
      changed,
      skip,
    };
  }

  function radio(compiled: HTMLElement, label: string): HTMLButtonElement {
    return Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[role="radio"]')).find(
      (button) => button.textContent?.trim().startsWith(label),
    )!;
  }

  async function fill(fixture: Awaited<ReturnType<typeof setup>>['fixture'], role?: string) {
    const compiled = fixture.nativeElement as HTMLElement;
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingAdultEmail')!, 'aunt@buddy.test');
    if (role) {
      radio(compiled, role).click();
    }
    await settle(fixture);
  }

  it('has no preselected group role and requires one', async () => {
    const { fixture, compiled, groups } = await setup();

    expect(compiled.querySelectorAll('button[role="radio"][aria-checked="true"]').length).toBe(1); // only the relationship (Parent) has a default
    await fill(fixture);
    submit(compiled);
    await settle(fixture);

    expect(compiled.textContent).toContain('Choose the access this adult gets.');
    expect(groups.inviteToGroup).not.toHaveBeenCalled();
  });

  it('sends a guardian invitation per selected child and one group invitation', async () => {
    const { fixture, compiled, guardians, groups, changed } = await setup();

    await fill(fixture, 'Member');
    radio(compiled, 'Guardian').click();
    compiled.querySelector<HTMLButtonElement>('button[role="switch"][aria-label="Emil"]')!.click();
    await settle(fixture);
    submit(compiled);
    await settle(fixture);

    expect(guardians.inviteGuardian).toHaveBeenCalledTimes(1);
    expect(guardians.inviteGuardian).toHaveBeenCalledWith('c1', {
      email: 'aunt@buddy.test',
      kind: 1,
    });
    expect(groups.inviteToGroup).toHaveBeenCalledWith('group-1', {
      email: 'aunt@buddy.test',
      role: 2,
    });
    expect(compiled.textContent).toContain('Guardian invitation for Ada');
    expect(compiled.textContent).toContain('Sent');
    expect(changed).toHaveBeenCalled();
  });

  it('keeps successful invitations and retries only the failed one', async () => {
    const inviteGuardian = vi
      .fn()
      .mockResolvedValueOnce({})
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }))
      .mockResolvedValue({});
    const { fixture, compiled, groups } = await setup({ guardians: { inviteGuardian } });

    await fill(fixture, 'Admin');
    submit(compiled);
    await settle(fixture);

    expect(compiled.textContent).toContain('Failed');
    expect(groups.inviteToGroup).toHaveBeenCalledTimes(1);

    buttonByText(compiled, 'Retry failed invitations')!.click();
    await settle(fixture);

    expect(inviteGuardian).toHaveBeenCalledTimes(3);
    expect(inviteGuardian).toHaveBeenLastCalledWith('c2', { email: 'aunt@buddy.test', kind: 0 });
    expect(groups.inviteToGroup).toHaveBeenCalledTimes(1);
    expect(compiled.textContent).not.toContain('Failed');
  });

  it('keeps a child the guardian unticked unticked when the setup reloads', async () => {
    const { fixture, compiled } = await setup();
    const emil = () =>
      compiled.querySelector<HTMLButtonElement>('button[role="switch"][aria-label="Emil"]')!;

    emil().click();
    await settle(fixture);
    fixture.componentRef.setInput(
      'setup',
      setupWith({ children: [child('c1', 'Ada'), child('c2', 'Emil'), child('c3', 'Ida')] }),
    );
    await settle(fixture);

    expect(emil().getAttribute('aria-checked')).toBe('false');
    expect(
      compiled
        .querySelector('button[role="switch"][aria-label="Ida"]')!
        .getAttribute('aria-checked'),
    ).toBe('true');
  });

  it('lets the guardian skip the step', async () => {
    const { compiled, skip, groups } = await setup();

    buttonByText(compiled, 'Skip for now')!.click();

    expect(skip).toHaveBeenCalled();
    expect(groups.inviteToGroup).not.toHaveBeenCalled();
  });
});
