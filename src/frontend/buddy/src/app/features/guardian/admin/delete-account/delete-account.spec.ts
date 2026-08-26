import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../../core/auth.service';
import { UsersService } from '../../../../core/users.service';
import { DeleteAccount } from './delete-account';

describe('DeleteAccount', () => {
  interface Stubs {
    users?: Partial<UsersService>;
    auth?: Partial<AuthService>;
  }

  function setup(stubs: Stubs = {}) {
    const usersStub: Partial<UsersService> = { deleteCurrentUser: vi.fn(async () => undefined), ...stubs.users };
    const authStub: Partial<AuthService> = { logout: vi.fn(), ...stubs.auth };

    TestBed.configureTestingModule({
      imports: [DeleteAccount],
      providers: [
        { provide: UsersService, useValue: usersStub },
        { provide: AuthService, useValue: authStub }
      ]
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

  function backdrop(compiled: HTMLElement): HTMLElement | null {
    return compiled.querySelector('.fixed.inset-0');
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  it('shows the danger zone with no confirm dialog open', () => {
    const { fixture } = setup();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Danger zone');
    expect(compiled.textContent).toContain('Deleting your account removes your access permanently. This cannot be undone.');
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
    expect(compiled.textContent).toContain('This permanently deletes your account and cannot be undone. You’ll be signed out immediately.');
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

    const dialogPanel = compiled.querySelector<HTMLElement>('.fixed.inset-0 > div')!;
    dialogPanel.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeTruthy();

    backdrop(compiled)!.click();
    fixture.detectChanges();
    expect(backdrop(compiled)).toBeFalsy();
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
    const deleteCurrentUser = vi.fn(() => new Promise<void>((resolve) => (resolveDelete = resolve)));
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
});
