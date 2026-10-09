import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../../core/auth.service';
import { AccountDeletionPreview, UsersService } from '../../../../core/users.service';
import { DeleteAccount } from './delete-account';

describe('DeleteAccount', () => {
  interface Stubs {
    users?: Partial<UsersService>;
    auth?: Partial<AuthService>;
  }

  function setup(stubs: Stubs = {}) {
    const usersStub: Partial<UsersService> = {
      deleteCurrentUser: vi.fn(async () => undefined),
      getAccountDeletionPreview: vi.fn(async () => emptyPreview()),
      ...stubs.users,
    };
    const authStub: Partial<AuthService> = { logout: vi.fn(), ...stubs.auth };

    TestBed.configureTestingModule({
      imports: [DeleteAccount],
      providers: [
        { provide: UsersService, useValue: usersStub },
        { provide: AuthService, useValue: authStub },
      ],
    });

    const fixture = TestBed.createComponent(DeleteAccount);
    fixture.detectChanges();

    return { fixture, users: usersStub, auth: authStub };
  }

  // The service calls are stubbed directly rather than routed through HttpClient, so no
  // PendingTasks entry is registered and whenStable() resolves immediately without waiting for
  // them. A macrotask flush drains the mocked promise chain instead (see docs/testing.md).
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function emptyPreview(): AccountDeletionPreview {
    return { childrenErased: [], groupsHandedOver: [], groupsDeleted: [] };
  }

  describe('the deletion preview', () => {
    it('lists the children erased and the groups handed over or deleted', async () => {
      const { fixture, users } = setup({
        users: {
          getAccountDeletionPreview: vi.fn(async () => ({
            childrenErased: [{ id: 'child-1', givenName: 'Ida', familyName: 'Hansen' }],
            groupsHandedOver: [
              {
                id: 'group-1',
                name: 'Family',
                newOwner: { id: 'user-2', givenName: 'Ole', familyName: 'Hansen' },
              },
            ],
            groupsDeleted: [{ id: 'group-2', name: 'Book club' }],
          })),
        },
      });

      openDialog(fixture);
      await settle(fixture);

      const text = dialog(fixture.nativeElement)!.textContent;
      expect(users.getAccountDeletionPreview).toHaveBeenCalledTimes(1);
      expect(text).toContain('These children have no other guardian.');
      expect(text).toContain('Ida Hansen');
      expect(text).toContain('Family: Ole Hansen becomes the owner');
      expect(text).toContain('Groups nobody else is in will be deleted, with their calendars:');
      expect(text).toContain('Book club');
    });

    it('shows nothing extra when the deletion affects no one else', async () => {
      const { fixture } = setup();

      openDialog(fixture);
      await settle(fixture);

      const text = dialog(fixture.nativeElement)!.textContent;
      expect(text).not.toContain('These children');
      expect(text).not.toContain('Groups');
      expect(text).not.toContain('Checking what else will be deleted');
    });

    it('says it is checking while the preview loads', () => {
      const { fixture } = setup({
        users: { getAccountDeletionPreview: vi.fn(() => new Promise<never>(() => undefined)) },
      });

      openDialog(fixture);

      expect(dialog(fixture.nativeElement)!.textContent).toContain(
        'Checking what else will be deleted…',
      );
    });

    it('still allows the deletion when the preview fails', async () => {
      const { fixture, users } = setup({
        users: { getAccountDeletionPreview: vi.fn(async () => Promise.reject(new Error('down'))) },
      });

      openDialog(fixture);
      await settle(fixture);

      expect(dialog(fixture.nativeElement)!.textContent).toContain(
        'Couldn’t check what else will be deleted.',
      );

      findButtonByText(fixture.nativeElement, 'Yes, delete my account')!.click();
      await settle(fixture);
      expect(users.deleteCurrentUser).toHaveBeenCalledTimes(1);
    });

    it('fetches the preview again each time the dialog opens', async () => {
      const { fixture, users } = setup();

      openDialog(fixture);
      await settle(fixture);
      findButtonByText(fixture.nativeElement, 'Cancel')!.click();
      fixture.detectChanges();
      openDialog(fixture);
      await settle(fixture);

      expect(users.getAccountDeletionPreview).toHaveBeenCalledTimes(2);
    });
  });

  // The dim layer behind the panel: pointer-only, so it is hidden from assistive tech.
  function backdrop(compiled: HTMLElement): HTMLElement | null {
    return compiled.querySelector('.fixed.inset-0 > [aria-hidden="true"]');
  }

  function dialog(compiled: HTMLElement): HTMLElement | null {
    return compiled.querySelector('[role="dialog"]');
  }

  function openDialog(fixture: { detectChanges: () => void; nativeElement: HTMLElement }) {
    findButtonByText(fixture.nativeElement, 'Delete my account')!.click();
    fixture.detectChanges();
  }

  function pressTab(shiftKey = false): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, cancelable: true });
    document.dispatchEvent(event);
    return event;
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
  }

  it('shows the danger zone with no confirm dialog open', () => {
    const { fixture } = setup();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Danger zone');
    expect(compiled.textContent).toContain(
      'Deleting your account removes your access permanently. This cannot be undone.',
    );
    expect(findButtonByText(compiled, 'Delete my account')).toBeTruthy();
    expect(backdrop(compiled)).toBeFalsy();
  });

  it('opens the confirm dialog when the delete button is clicked', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();

    expect(backdrop(compiled)).toBeTruthy();
    expect(compiled.textContent).toContain('Delete your account?');
    expect(compiled.textContent).toContain(
      'This permanently deletes your account and cannot be undone. You’ll be signed out immediately.',
    );
    expect(findButtonByText(compiled, 'Cancel')).toBeTruthy();
    expect(findButtonByText(compiled, 'Yes, delete my account')).toBeTruthy();
  });

  it('closes the confirm dialog when cancel is clicked', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeTruthy();

    findButtonByText(compiled, 'Cancel')!.click();
    fixture.detectChanges();

    expect(backdrop(compiled)).toBeFalsy();
  });

  it('closes the confirm dialog when the backdrop is clicked, but not when the dialog content is clicked', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();

    // Neither the panel itself nor its text closes it: only the backdrop does.
    dialog(compiled)!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeTruthy();

    compiled.querySelector<HTMLElement>('#delete-account-confirm-description')!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeTruthy();

    backdrop(compiled)!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeFalsy();
  });

  it('closes the confirm dialog when Escape is pressed', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeTruthy();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(backdrop(compiled)).toBeFalsy();
  });

  it('keeps the confirm dialog open on Escape while the delete request is in flight', () => {
    const deleteCurrentUser = vi.fn(() => new Promise<void>(() => undefined));
    const { fixture } = setup({ users: { deleteCurrentUser } });
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    fixture.detectChanges();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(backdrop(compiled)).toBeTruthy();
  });

  it('deletes the current user with no arguments and logs out on success', async () => {
    const { fixture, users, auth } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    await settle(fixture);

    expect(users.deleteCurrentUser).toHaveBeenCalledTimes(1);
    expect(users.deleteCurrentUser).toHaveBeenCalledWith();
    expect(auth.logout).toHaveBeenCalledTimes(1);
    expect(auth.logout).toHaveBeenCalledWith();
  });

  it('disables both dialog buttons and shows a deleting label while the delete request is in flight', async () => {
    let resolveDelete!: () => void;
    const deleteCurrentUser = vi.fn(
      () => new Promise<void>((resolve) => (resolveDelete = resolve)),
    );
    const { fixture, auth } = setup({ users: { deleteCurrentUser } });
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    fixture.detectChanges();

    // The signal writes at the top of confirmDelete run synchronously before the awaited call
    // settles, so the disabled/label state should already reflect "deleting" here.
    expect(findButtonByText(compiled, 'Cancel')?.disabled).toBe(true);
    expect(findButtonByText(compiled, 'Deleting…')?.disabled).toBe(true);
    expect(findButtonByText(compiled, 'Yes, delete my account')).toBeFalsy();
    expect(auth.logout).not.toHaveBeenCalled();

    // Cancelling (e.g. via a backdrop click) is a no-op while a delete is in flight.
    backdrop(compiled)!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeTruthy();

    resolveDelete();
    await settle(fixture);

    expect(auth.logout).toHaveBeenCalledTimes(1);
  });

  it('shows a translated error, re-enables the dialog, and does not log out when deletion fails', async () => {
    const deleteCurrentUser = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture, auth } = setup({ users: { deleteCurrentUser } });
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to delete your account.');
    expect(auth.logout).not.toHaveBeenCalled();
    expect(backdrop(compiled)).toBeTruthy();
    expect(findButtonByText(compiled, 'Cancel')?.disabled).toBe(false);
    expect(findButtonByText(compiled, 'Yes, delete my account')?.disabled).toBe(false);
  });

  it('clears a previous error as soon as deletion is retried', async () => {
    const deleteCurrentUser = vi
      .fn<UsersService['deleteCurrentUser']>()
      .mockRejectedValueOnce(new Error('boom'))
      .mockImplementation(() => new Promise<void>(() => undefined));
    const { fixture } = setup({ users: { deleteCurrentUser } });
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to delete your account.');

    findButtonByText(compiled, 'Yes, delete my account')!.click();
    fixture.detectChanges();

    expect(compiled.textContent).not.toContain('Unable to delete your account.');
  });

  it('clears a previous error when the confirm dialog is reopened', async () => {
    const deleteCurrentUser = vi.fn(async () => Promise.reject(new Error('boom')));
    const { fixture } = setup({ users: { deleteCurrentUser } });
    const compiled = fixture.nativeElement as HTMLElement;

    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to delete your account.');

    findButtonByText(compiled, 'Cancel')!.click();
    fixture.detectChanges();
    findButtonByText(compiled, 'Delete my account')!.click();
    fixture.detectChanges();

    expect(compiled.textContent).not.toContain('Unable to delete your account.');
  });
  it('renders the confirm panel as a labelled modal dialog with an inert backdrop', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    openDialog(fixture);

    const panel = dialog(compiled)!;
    expect(panel.getAttribute('aria-modal')).toBe('true');
    expect(
      document.getElementById(panel.getAttribute('aria-labelledby')!)?.textContent?.trim(),
    ).toBe('Delete your account?');
    expect(
      document.getElementById(panel.getAttribute('aria-describedby')!)?.textContent?.trim(),
    ).toBe(
      'This permanently deletes your account and cannot be undone. You’ll be signed out immediately.',
    );
    expect(backdrop(compiled)!.contains(panel)).toBe(false);
  });

  // jsdom has no layout, so this checks the classes: the panel scrolls rather than running off a
  // short phone screen, and its overlay sits above the shell's sticky header (z-30).
  it('keeps the dialog within a short screen and above the sticky header', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    openDialog(fixture);

    const panel = dialog(compiled)!;
    expect(panel.classList).toContain('max-h-[90dvh]');
    expect(panel.classList).toContain('overflow-y-auto');
    expect(panel.parentElement!.classList).toContain('z-40');
  });

  it('moves focus to Cancel when the dialog opens', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    openDialog(fixture);

    expect(document.activeElement).toBe(findButtonByText(compiled, 'Cancel'));
  });

  it.each([
    ['Escape', () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))],
    ['a backdrop click', (compiled: HTMLElement) => backdrop(compiled)!.click()],
    ['Cancel', (compiled: HTMLElement) => findButtonByText(compiled, 'Cancel')!.click()],
  ])('returns focus to the delete button after closing via %s', (_, close) => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;

    openDialog(fixture);
    close(compiled);
    fixture.detectChanges();

    expect(dialog(compiled)).toBeFalsy();
    expect(document.activeElement).toBe(findButtonByText(compiled, 'Delete my account'));
  });

  it('wraps Tab from the last button to the first, and Shift+Tab from the first to the last', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;
    openDialog(fixture);
    const cancel = findButtonByText(compiled, 'Cancel')!;
    const confirm = findButtonByText(compiled, 'Yes, delete my account')!;

    confirm.focus();
    expect(pressTab().defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(cancel);

    expect(pressTab(true).defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(confirm);
  });

  it('leaves Tab alone between the dialog buttons and pulls stray focus back into the dialog', () => {
    const { fixture } = setup();
    const compiled = fixture.nativeElement as HTMLElement;
    openDialog(fixture);

    // Cancel -> Confirm is ordinary tab order inside the dialog, so the browser handles it.
    expect(pressTab().defaultPrevented).toBe(false);

    findButtonByText(compiled, 'Delete my account')!.focus();
    expect(pressTab().defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(findButtonByText(compiled, 'Cancel'));
  });

  it('keeps focus on the dialog panel when Tab is pressed while both buttons are disabled', () => {
    const deleteCurrentUser = vi.fn(() => new Promise<void>(() => undefined));
    const { fixture } = setup({ users: { deleteCurrentUser } });
    const compiled = fixture.nativeElement as HTMLElement;
    openDialog(fixture);
    findButtonByText(compiled, 'Yes, delete my account')!.click();
    fixture.detectChanges();

    expect(pressTab().defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(dialog(compiled));
  });

  it('ignores Tab while the dialog is closed', () => {
    setup();

    expect(pressTab().defaultPrevented).toBe(false);
  });
});
