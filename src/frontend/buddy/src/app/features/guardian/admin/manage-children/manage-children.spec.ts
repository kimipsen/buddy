import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, CreateChildResult, GuardianInvite, GuardiansService } from '../../../../core/guardians.service';
import { ManageChildren } from './manage-children';

describe('ManageChildren', () => {
  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Sam', familyName: 'Kid' },
      guardianLinkId: 'link-1',
      kind: 0,
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides
    };
  }

  function invite(overrides: Partial<GuardianInvite> = {}): GuardianInvite {
    return {
      id: 'invite-1',
      email: 'co-parent@buddy.test',
      kind: 1,
      invitedAt: '2026-08-01T00:00:00Z',
      expiresAt: '2026-08-08T00:00:00Z',
      ...overrides
    };
  }

  function createdChild(overrides: Partial<CreateChildResult> = {}): CreateChildResult {
    return {
      id: 'child-new',
      name: { givenName: 'Ada', familyName: 'Kid' },
      guardianLinkId: 'link-new',
      kind: 0,
      language: 'en',
      timeZoneId: 'UTC',
      username: 'ada.kid',
      temporaryPassword: 'temp-pass-123',
      ...overrides
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      createChild: vi.fn(async () => createdChild()),
      revokeChild: vi.fn(async () => undefined),
      updateChildLanguage: vi.fn(async (childId: string, language: string) => child({ id: childId, language })),
      updateChildTimeZone: vi.fn(async (childId: string, timeZoneId: string) => child({ id: childId, timeZoneId })),
      listGuardianInvites: vi.fn(async () => []),
      inviteGuardian: vi.fn(async () => invite()),
      revokeGuardianInvite: vi.fn(async () => undefined),
      ...stubs.guardians
    };

    await TestBed.configureTestingModule({
      imports: [ManageChildren],
      providers: [{ provide: GuardiansService, useValue: guardiansStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageChildren);

    return { fixture, guardians: guardiansStub };
  }

  // The app runs zoneless, and the stubbed GuardiansService never registers a PendingTasks entry,
  // so fixture.whenStable() resolves immediately without waiting for it. A macrotask flush lets
  // every already-scheduled microtask in the mocked promise chains drain first -- see
  // docs/testing.md and home.spec.ts. This reliably settles chains of two sequential awaited
  // service calls (e.g. addChild's createChild -> loadChildren) since nothing in those chains
  // schedules a further macrotask itself.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void; reject: (reason?: unknown) => void } {
    let resolve!: (value: T) => void;
    let reject!: (reason?: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  function findButtonByText(scope: ParentNode, text: string): HTMLButtonElement | undefined {
    return Array.from(scope.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  // Locates the child's primary <li> (name, language select, remove/invite-toggle buttons) by the
  // full name shown in its first <span>. The invite panel for that child renders as a *separate*
  // sibling <li>, not nested inside this one.
  function childLanguageSelect(compiled: HTMLElement, fullName: string): HTMLSelectElement {
    return childRow(compiled, fullName).querySelectorAll<HTMLSelectElement>('select')[0];
  }

  function childTimeZoneSelect(compiled: HTMLElement, fullName: string): HTMLSelectElement {
    return childRow(compiled, fullName).querySelectorAll<HTMLSelectElement>('select')[1];
  }

  function childRow(compiled: HTMLElement, fullName: string): HTMLElement {
    const row = Array.from(compiled.querySelectorAll('li')).find((li) => li.querySelector('span')?.textContent?.trim() === fullName);
    if (!row) {
      throw new Error(`child row not found for "${fullName}"`);
    }
    return row;
  }

  // Locates a pending invite's <li> inside the (single, always-unique-when-open) invite panel by
  // the email shown in its first <span>.
  function inviteRow(compiled: HTMLElement, email: string): HTMLElement {
    const row = Array.from(compiled.querySelectorAll('li')).find((li) => li.querySelector('span')?.textContent?.trim() === email);
    if (!row) {
      throw new Error(`invite row not found for "${email}"`);
    }
    return row;
  }

  function childGivenNameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="childGivenName"]')!;
  }

  function childFamilyNameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="childFamilyName"]')!;
  }

  function childUsernameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="childUsername"]')!;
  }

  function addChildForm(compiled: HTMLElement): HTMLFormElement {
    return childGivenNameInput(compiled).closest('form')!;
  }

  function addChildButton(compiled: HTMLElement): HTMLButtonElement {
    return addChildForm(compiled).querySelector<HTMLButtonElement>('button[type="submit"]')!;
  }

  function fillAddChildForm(compiled: HTMLElement, given: string, family: string, username: string): void {
    setInputValue(childGivenNameInput(compiled), given);
    setInputValue(childFamilyNameInput(compiled), family);
    setInputValue(childUsernameInput(compiled), username);
  }

  function inviteEmailInput(compiled: HTMLElement): HTMLInputElement | null {
    return compiled.querySelector<HTMLInputElement>('input[name="guardianInviteEmail"]');
  }

  function inviteKindSelect(compiled: HTMLElement): HTMLSelectElement | null {
    return compiled.querySelector<HTMLSelectElement>('select[name="guardianInviteKind"]');
  }

  function inviteForm(compiled: HTMLElement): HTMLFormElement | null {
    return inviteEmailInput(compiled)?.closest('form') ?? null;
  }

  function inviteSendButton(compiled: HTMLElement): HTMLButtonElement | null {
    return inviteForm(compiled)?.querySelector<HTMLButtonElement>('button[type="submit"]') ?? null;
  }

  // ----- Children list: loading / empty / error / rendering -----

  it('shows a loading message before the children list resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading children…');
  });

  it('shows the empty state once loading finishes with no children', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No children linked yet. Add one below.');
  });

  it('shows an error message when loading children fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load children.');
  });

  it('renders each child with its name and current language selected', async () => {
    const children = [child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, language: 'en' }), child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' }, language: 'da' })];
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => children) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Sam Kid');
    expect(compiled.textContent).toContain('Ada Kid');

    const samSelect = childLanguageSelect(compiled, 'Sam Kid');
    const adaSelect = childLanguageSelect(compiled, 'Ada Kid');
    expect(samSelect.value).toBe('en');
    expect(adaSelect.value).toBe('da');
  });

  // ----- Create-child form -----

  it('keeps the add-child button disabled until every field is filled', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(addChildButton(compiled).disabled).toBe(true);

    setInputValue(childGivenNameInput(compiled), 'Ada');
    await settle(fixture);
    expect(addChildButton(compiled).disabled).toBe(true);

    setInputValue(childFamilyNameInput(compiled), 'Kid');
    await settle(fixture);
    expect(addChildButton(compiled).disabled).toBe(true);

    setInputValue(childUsernameInput(compiled), 'ada.kid');
    await settle(fixture);
    expect(addChildButton(compiled).disabled).toBe(false);
  });

  it('keeps the add-child button disabled when every field is whitespace only', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, '   ', '   ', '   ');
    await settle(fixture);

    expect(addChildButton(compiled).disabled).toBe(true);
  });

  it('does not call createChild when the form is submitted with blank fields', async () => {
    const { fixture, guardians } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(guardians.createChild).not.toHaveBeenCalled();
  });

  it('submits the trimmed field values, reloads the list, and clears the form', async () => {
    const listMyChildren = vi.fn(async () => [child()]);
    const { fixture, guardians } = await setup({ guardians: { listMyChildren } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, '  Ada  ', '  Kid  ', '  ada.kid  ');
    await settle(fixture);

    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(guardians.createChild).toHaveBeenCalledWith({ givenName: 'Ada', familyName: 'Kid', username: 'ada.kid' });
    expect(listMyChildren).toHaveBeenCalledTimes(2);
    expect(childGivenNameInput(compiled).value).toBe('');
    expect(childFamilyNameInput(compiled).value).toBe('');
    expect(childUsernameInput(compiled).value).toBe('');
  });

  it('shows the created child\'s name and temporary password after a successful create', async () => {
    const { fixture } = await setup({
      guardians: { createChild: vi.fn(async () => createdChild({ name: { givenName: 'Ada', familyName: 'Kid' }, temporaryPassword: 'p4ssw0rd!' })) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Ada was created.');
    expect(compiled.textContent).toContain('p4ssw0rd!');
    expect(findButtonByText(compiled, 'Copy')).toBeTruthy();
  });

  it('disables the add-child button while the create request is in flight', async () => {
    const { promise, resolve } = deferred<CreateChildResult>();
    const { fixture } = await setup({ guardians: { createChild: vi.fn(() => promise) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(addChildButton(compiled).disabled).toBe(true);

    resolve(createdChild());
    await settle(fixture);

    // The button re-disables itself once the request finishes -- not because addingChild() is
    // still true, but because a successful create clears the three name/username fields, and the
    // button is separately gated on all three being non-blank.
    expect(addChildButton(compiled).disabled).toBe(true);
    expect(childGivenNameInput(compiled).value).toBe('');
  });

  it('shows a username-taken error and keeps the typed values on a 409 response', async () => {
    const createChild = vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 409, statusText: 'Conflict' })));
    const { fixture } = await setup({ guardians: { createChild } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('That username is already in use. Choose another one.');
    expect(childGivenNameInput(compiled).value).toBe('Ada');
    expect(childUsernameInput(compiled).value).toBe('ada.kid');
  });

  it('shows a generic create error on any other failure', async () => {
    const createChild = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture } = await setup({ guardians: { createChild } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to create the child account.');
  });

  it('shows a generic create error on a non-409 HTTP error', async () => {
    const createChild = vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 500, statusText: 'Server Error' })));
    const { fixture } = await setup({ guardians: { createChild } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to create the child account.');
    expect(compiled.textContent).not.toContain('already in use');
  });

  // ----- Revoke-child flow -----

  it('shows a confirmation prompt instead of the remove button when remove is requested', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Remove')!.click();
    fixture.detectChanges();

    const row = childRow(compiled, 'Sam Kid');
    expect(row.textContent).toContain('Remove this child?');
    expect(findButtonByText(row, 'Confirm')).toBeTruthy();
    expect(findButtonByText(row, 'Remove')).toBeUndefined();
  });

  it('cancels the confirmation without calling revokeChild', async () => {
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Remove')!.click();
    fixture.detectChanges();

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Cancel')!.click();
    fixture.detectChanges();

    expect(guardians.revokeChild).not.toHaveBeenCalled();
    expect(findButtonByText(childRow(compiled, 'Sam Kid'), 'Remove')).toBeTruthy();
  });

  it('revokes the child on confirm and reloads the list', async () => {
    const listMyChildren = vi.fn(async () => [child()]);
    const { fixture, guardians } = await setup({ guardians: { listMyChildren } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Remove')!.click();
    fixture.detectChanges();

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Confirm')!.click();
    await settle(fixture);

    expect(guardians.revokeChild).toHaveBeenCalledWith('child-1');
    expect(listMyChildren).toHaveBeenCalledTimes(2);
  });

  it('disables the confirm and cancel buttons while the revoke request is in flight', async () => {
    const { promise, resolve } = deferred<void>();
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), revokeChild: vi.fn(() => promise) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Remove')!.click();
    fixture.detectChanges();

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Confirm')!.click();
    fixture.detectChanges();

    const row = childRow(compiled, 'Sam Kid');
    expect(findButtonByText(row, 'Confirm')!.disabled).toBe(true);
    expect(findButtonByText(row, 'Cancel')!.disabled).toBe(true);

    resolve(undefined);
    await settle(fixture);
  });

  // The component only clears confirmingRevokeChildId on a *successful* revoke -- on failure the
  // confirm/cancel prompt is left exactly as-is (see manage-children.ts confirmRevoke's catch
  // block). Pinning this rather than silently assuming it resets, since it means a guardian must
  // click Cancel themselves to dismiss the prompt after a failed attempt.
  it('keeps the confirmation prompt open and shows an error when revoking fails', async () => {
    const revokeChild = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), revokeChild } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Remove')!.click();
    fixture.detectChanges();

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Confirm')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to remove this child.');
    expect(findButtonByText(childRow(compiled, 'Sam Kid'), 'Confirm')).toBeTruthy();
  });

  // ----- Language change flow -----

  it('changes a child\'s language and reflects the updated value', async () => {
    const updateChildLanguage = vi.fn(async () => child({ language: 'da' }));
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), updateChildLanguage } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = childLanguageSelect(compiled, 'Sam Kid');
    select.value = 'da';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(guardians.updateChildLanguage).toHaveBeenCalledWith('child-1', 'da');
    expect(childLanguageSelect(compiled, 'Sam Kid').value).toBe('da');
  });

  it('shows a per-child language error without affecting other children', async () => {
    const children = [child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } }), child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' } })];
    const updateChildLanguage = vi.fn(async (childId: string) => {
      if (childId === 'child-1') {
        return Promise.reject(new Error('boom'));
      }
      return child({ id: childId, language: 'da' });
    });
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => children), updateChildLanguage } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const samSelect = childLanguageSelect(compiled, 'Sam Kid');
    samSelect.value = 'da';
    samSelect.dispatchEvent(new Event('change'));
    await settle(fixture);

    const samRow = childRow(compiled, 'Sam Kid');
    const adaRow = childRow(compiled, 'Ada Kid');
    expect(samRow.textContent).toContain('Unable to update this child\'s language.');
    expect(adaRow.textContent).not.toContain('Unable to update this child\'s language.');
  });

  it('clears a previous language error on a successful retry', async () => {
    const updateChildLanguage = vi
      .fn()
      .mockImplementationOnce(async () => Promise.reject(new Error('boom')))
      .mockImplementationOnce(async () => child({ language: 'da' }));
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), updateChildLanguage } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = childLanguageSelect(compiled, 'Sam Kid');
    select.value = 'da';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);
    expect(childRow(compiled, 'Sam Kid').textContent).toContain('Unable to update this child\'s language.');

    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(childRow(compiled, 'Sam Kid').textContent).not.toContain('Unable to update this child\'s language.');
  });

  // manage-children.html previously bound [disabled] on this <select> alongside [ngModel], which
  // Angular's SelectControlValueAccessor silently overrode back to enabled on every change-detection
  // pass (a documented ngModel+[disabled] quirk on native <select>s). Switched to [attr.disabled],
  // which sets the DOM attribute directly rather than going through the value accessor's disabled
  // resync, so it now actually reflects savingLanguageChildId().
  it('disables the language select while the change is in flight, and re-enables it once it settles', async () => {
    const { promise, resolve } = deferred<ChildSummary>();
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), updateChildLanguage: vi.fn(() => promise) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = childLanguageSelect(compiled, 'Sam Kid');
    select.value = 'da';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    fixture.detectChanges();

    expect(select.disabled).toBe(true);

    resolve(child({ language: 'da' }));
    await settle(fixture);

    expect(select.disabled).toBe(false);
  });

  // ----- Time zone change flow -----

  it('renders each child with its current time zone selected', async () => {
    const children = [
      child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, timeZoneId: 'America/New_York' }),
      child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' }, timeZoneId: 'Europe/Copenhagen' })
    ];
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => children) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(childTimeZoneSelect(compiled, 'Sam Kid').value).toBe('America/New_York');
    expect(childTimeZoneSelect(compiled, 'Ada Kid').value).toBe('Europe/Copenhagen');
  });

  it('changes a child\'s time zone and reflects the updated value', async () => {
    const updateChildTimeZone = vi.fn(async () => child({ timeZoneId: 'Europe/Copenhagen' }));
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), updateChildTimeZone } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = childTimeZoneSelect(compiled, 'Sam Kid');
    select.value = 'Europe/Copenhagen';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(guardians.updateChildTimeZone).toHaveBeenCalledWith('child-1', 'Europe/Copenhagen');
    expect(childTimeZoneSelect(compiled, 'Sam Kid').value).toBe('Europe/Copenhagen');
  });

  it('shows a per-child time zone error without affecting other children', async () => {
    const children = [child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } }), child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' } })];
    const updateChildTimeZone = vi.fn(async (childId: string) => {
      if (childId === 'child-1') {
        return Promise.reject(new Error('boom'));
      }
      return child({ id: childId, timeZoneId: 'Europe/Copenhagen' });
    });
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => children), updateChildTimeZone } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const samSelect = childTimeZoneSelect(compiled, 'Sam Kid');
    samSelect.value = 'Europe/Copenhagen';
    samSelect.dispatchEvent(new Event('change'));
    await settle(fixture);

    const samRow = childRow(compiled, 'Sam Kid');
    const adaRow = childRow(compiled, 'Ada Kid');
    expect(samRow.textContent).toContain('Unable to update this child\'s time zone.');
    expect(adaRow.textContent).not.toContain('Unable to update this child\'s time zone.');
  });

  it('disables the time zone select while the change is in flight, and re-enables it once it settles', async () => {
    const { promise, resolve } = deferred<ChildSummary>();
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), updateChildTimeZone: vi.fn(() => promise) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const select = childTimeZoneSelect(compiled, 'Sam Kid');
    select.value = 'Europe/Copenhagen';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    fixture.detectChanges();

    expect(select.disabled).toBe(true);

    resolve(child({ timeZoneId: 'Europe/Copenhagen' }));
    await settle(fixture);

    expect(select.disabled).toBe(false);
  });

  // ----- Guardian invite panel -----

  it('loads and shows pending invites when the invite panel is opened', async () => {
    const invites = [invite({ email: 'pending@buddy.test' })];
    const listGuardianInvites = vi.fn(async () => invites);
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), listGuardianInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    expect(guardians.listGuardianInvites).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('pending@buddy.test');
    expect(compiled.textContent).toContain('Parent');
  });

  it('shows the pending-empty message when a child has no invites', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('No pending invites.');
  });

  it('shows an error when loading invites fails', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child()]), listGuardianInvites: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to load invites.');
  });

  it('collapses the invite panel on a second click without reloading invites', async () => {
    const listGuardianInvites = vi.fn(async () => []);
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), listGuardianInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);
    expect(listGuardianInvites).toHaveBeenCalledTimes(1);

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Close')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('No pending invites.');
    expect(listGuardianInvites).toHaveBeenCalledTimes(1);
    expect(findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')).toBeTruthy();
  });

  it('switches the invite panel between children, loading the newly selected child\'s invites', async () => {
    const children = [child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } }), child({ id: 'child-2', name: { givenName: 'Ada', familyName: 'Kid' } })];
    const listGuardianInvites = vi.fn(async (childId: string) => (childId === 'child-1' ? [invite({ email: 'sam-invite@buddy.test' })] : [invite({ email: 'ada-invite@buddy.test' })]));
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => children), listGuardianInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('sam-invite@buddy.test');

    findButtonByText(childRow(compiled, 'Ada Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    expect(guardians.listGuardianInvites).toHaveBeenLastCalledWith('child-2');
    expect(compiled.textContent).not.toContain('sam-invite@buddy.test');
    expect(compiled.textContent).toContain('ada-invite@buddy.test');
  });

  it('resets the typed email, kind, and error when the invite panel is reopened', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    setInputValue(inviteEmailInput(compiled)!, 'typed@buddy.test');
    await settle(fixture);
    expect(inviteEmailInput(compiled)!.value).toBe('typed@buddy.test');

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Close')!.click();
    await settle(fixture);

    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    expect(inviteEmailInput(compiled)!.value).toBe('');
  });

  // ----- Send-invite flow -----

  it('keeps the send-invite button disabled until an email is entered', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    expect(inviteSendButton(compiled)!.disabled).toBe(true);

    setInputValue(inviteEmailInput(compiled)!, '   ');
    await settle(fixture);
    expect(inviteSendButton(compiled)!.disabled).toBe(true);

    setInputValue(inviteEmailInput(compiled)!, 'friend@buddy.test');
    await settle(fixture);
    expect(inviteSendButton(compiled)!.disabled).toBe(false);
  });

  it('sends an invite with the trimmed email and default Parent kind, then reloads and clears the email', async () => {
    const listGuardianInvites = vi.fn(async () => []);
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), listGuardianInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    setInputValue(inviteEmailInput(compiled)!, '  friend@buddy.test  ');
    await settle(fixture);

    inviteForm(compiled)!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(guardians.inviteGuardian).toHaveBeenCalledWith('child-1', { email: 'friend@buddy.test', kind: 0 });
    expect(listGuardianInvites).toHaveBeenCalledTimes(2);
    expect(inviteEmailInput(compiled)!.value).toBe('');
  });

  it('sends an invite with the Guardian kind when selected', async () => {
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    setInputValue(inviteEmailInput(compiled)!, 'friend@buddy.test');
    const kindSelect = inviteKindSelect(compiled)!;
    kindSelect.value = kindSelect.querySelectorAll('option')[1].value;
    kindSelect.dispatchEvent(new Event('change'));
    await settle(fixture);

    inviteForm(compiled)!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(guardians.inviteGuardian).toHaveBeenCalledWith('child-1', { email: 'friend@buddy.test', kind: 1 });
  });

  it('only offers Parent and Guardian as invitable kinds', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    const labels = Array.from(inviteKindSelect(compiled)!.querySelectorAll('option')).map((option) => option.textContent?.trim());
    expect(labels).toEqual(['Parent', 'Guardian']);
  });

  it('shows a send-invite error and keeps the typed email', async () => {
    const inviteGuardian = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), inviteGuardian } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    setInputValue(inviteEmailInput(compiled)!, 'friend@buddy.test');
    await settle(fixture);

    inviteForm(compiled)!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to send the invite. An invite was already sent to this address recently.');
    expect(inviteEmailInput(compiled)!.value).toBe('friend@buddy.test');
  });

  it('disables the send-invite button while the request is in flight', async () => {
    const { promise, resolve } = deferred<GuardianInvite>();
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), inviteGuardian: vi.fn(() => promise) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    setInputValue(inviteEmailInput(compiled)!, 'friend@buddy.test');
    await settle(fixture);

    inviteForm(compiled)!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(inviteSendButton(compiled)!.disabled).toBe(true);

    resolve(invite());
    await settle(fixture);
  });

  // ----- Revoke-invite flow -----

  it('revokes a pending invite and reloads the invite list', async () => {
    const pendingInvite = invite({ id: 'invite-9', email: 'pending@buddy.test' });
    const listGuardianInvites = vi.fn(async () => [pendingInvite]);
    const { fixture, guardians } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]), listGuardianInvites } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    findButtonByText(inviteRow(compiled, 'pending@buddy.test'), 'Cancel')!.click();
    await settle(fixture);

    expect(guardians.revokeGuardianInvite).toHaveBeenCalledWith('child-1', 'invite-9');
    expect(listGuardianInvites).toHaveBeenCalledTimes(2);
  });

  it('shows an error when revoking an invite fails', async () => {
    const pendingInvite = invite({ id: 'invite-9', email: 'pending@buddy.test' });
    const { fixture } = await setup({
      guardians: {
        listMyChildren: vi.fn(async () => [child()]),
        listGuardianInvites: vi.fn(async () => [pendingInvite]),
        revokeGuardianInvite: vi.fn(async () => Promise.reject(new Error('boom')))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    findButtonByText(inviteRow(compiled, 'pending@buddy.test'), 'Cancel')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to cancel the invite.');
  });

  it('disables only the targeted invite\'s cancel button while it is being revoked', async () => {
    const invites = [invite({ id: 'invite-9', email: 'pending@buddy.test' }), invite({ id: 'invite-10', email: 'other@buddy.test' })];
    const { promise, resolve } = deferred<void>();
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child()]), listGuardianInvites: vi.fn(async () => invites), revokeGuardianInvite: vi.fn(() => promise) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(childRow(compiled, 'Sam Kid'), 'Invite a co-guardian')!.click();
    await settle(fixture);

    findButtonByText(inviteRow(compiled, 'pending@buddy.test'), 'Cancel')!.click();
    fixture.detectChanges();

    expect(findButtonByText(inviteRow(compiled, 'pending@buddy.test'), 'Cancel')!.disabled).toBe(true);
    expect(findButtonByText(inviteRow(compiled, 'other@buddy.test'), 'Cancel')!.disabled).toBe(false);

    resolve(undefined);
    await settle(fixture);
  });

  // ----- Copy temporary password -----

  it('copies the temporary password to the clipboard and shows "Copied!"', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const { fixture } = await setup({ guardians: { createChild: vi.fn(async () => createdChild({ temporaryPassword: 'p4ssw0rd!' })) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    findButtonByText(compiled, 'Copy')!.click();
    await settle(fixture);

    expect(writeText).toHaveBeenCalledWith('p4ssw0rd!');
    expect(compiled.textContent).toContain('Copied!');
  });

  it('keeps showing "Copy" when writing to the clipboard fails', async () => {
    const writeText = vi.fn(async () => Promise.reject(new Error('denied')));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    fillAddChildForm(compiled, 'Ada', 'Kid', 'ada.kid');
    addChildForm(compiled).dispatchEvent(new Event('submit'));
    await settle(fixture);

    findButtonByText(compiled, 'Copy')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Copy');
    expect(compiled.textContent).not.toContain('Copied!');
  });
});
