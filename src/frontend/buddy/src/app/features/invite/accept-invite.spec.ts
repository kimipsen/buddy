import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth.service';
import { GroupInvitePreview, GroupsService } from '../../core/groups.service';
import { AcceptInvite } from './accept-invite';

const STORAGE_KEY = 'buddy_pending_invite_token';

describe('AcceptInvite', () => {
  interface Stubs {
    auth?: Partial<AuthService>;
    groups?: Partial<GroupsService>;
    token?: string;
  }

  beforeEach(() => {
    sessionStorage.clear();
  });

  async function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = {
      isAuthenticated: signal(true).asReadonly(),
      login: vi.fn(async () => {}),
      ...stubs.auth
    };
    const groupsStub: Partial<GroupsService> = {
      previewInvite: vi.fn(async (): Promise<GroupInvitePreview> => ({ groupName: 'The Andersens' })),
      acceptInvite: vi.fn(async () => {}),
      ...stubs.groups
    };
    const token = stubs.token ?? 'invite-token-123';

    await TestBed.configureTestingModule({
      imports: [AcceptInvite],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: GroupsService, useValue: groupsStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ token }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AcceptInvite);
    const router = TestBed.inject(Router);

    return { fixture, auth: authStub, groups: groupsStub, router, token };
  }

  // Component tests are zoneless: fixture.whenStable() only tracks HttpClient-style pending
  // tasks, so it resolves immediately and does nothing for a plain Promise returned by a stubbed
  // service. A setTimeout macrotask flush reliably drains any depth of chained awaits instead --
  // see docs/testing.md.
  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function text(fixture: ComponentFixture<unknown>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function findButton(fixture: ComponentFixture<unknown>, label: string): HTMLButtonElement | undefined {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === label
    );
  }

  it('shows the loading state before the preview resolves', async () => {
    let resolvePreview!: (value: GroupInvitePreview) => void;
    const { fixture } = await setup({
      groups: { previewInvite: vi.fn(() => new Promise<GroupInvitePreview>((resolve) => (resolvePreview = resolve))) }
    });

    fixture.detectChanges();

    expect(text(fixture)).toContain('Loading invite…');

    resolvePreview({ groupName: 'The Andersens' });
    await settle(fixture);
  });

  it('previews the invite using the token from the route, exactly once', async () => {
    const { fixture, groups, token } = await setup({ token: 'a-specific-token' });
    await settle(fixture);

    expect(groups.previewInvite).toHaveBeenCalledWith('a-specific-token');
    expect(groups.previewInvite).toHaveBeenCalledTimes(1);
    expect(token).toBe('a-specific-token');
  });

  it('shows the group name once the preview resolves', async () => {
    const { fixture } = await setup({ groups: { previewInvite: vi.fn(async () => ({ groupName: 'The Andersens' })) } });
    await settle(fixture);

    expect(text(fixture)).toContain("You've been invited to join The Andersens.");
  });

  it('shows an error when the invite token is invalid or expired', async () => {
    const { fixture } = await setup({ groups: { previewInvite: vi.fn(async () => Promise.reject(new Error('not found'))) } });
    await settle(fixture);

    expect(text(fixture)).toContain('This invite link is invalid or has expired.');
    expect(text(fixture)).not.toContain("You've been invited");
  });

  it('does not show the accept button when the preview failed', async () => {
    const { fixture } = await setup({ groups: { previewInvite: vi.fn(async () => Promise.reject(new Error('not found'))) } });
    await settle(fixture);

    expect(findButton(fixture, 'Accept invite')).toBeUndefined();
  });

  it('shows the accept button and no log-in prompt for an authenticated user', async () => {
    const { fixture } = await setup({ auth: { isAuthenticated: signal(true).asReadonly() } });
    await settle(fixture);

    expect(findButton(fixture, 'Accept invite')).toBeTruthy();
    expect(text(fixture)).not.toContain('Log in with the account this invite was sent to');
  });

  it('shows the log-in prompt and no accept button for an unauthenticated user', async () => {
    const { fixture } = await setup({ auth: { isAuthenticated: signal(false).asReadonly() } });
    await settle(fixture);

    expect(findButton(fixture, 'Accept invite')).toBeUndefined();
    expect(findButton(fixture, 'Log in to accept')).toBeTruthy();
    expect(text(fixture)).toContain('Log in with the account this invite was sent to, to accept it.');
  });

  it('stores the exact pending invite token and starts login when logging in to accept', async () => {
    const { fixture, auth, token } = await setup({ auth: { isAuthenticated: signal(false).asReadonly() } });
    await settle(fixture);

    findButton(fixture, 'Log in to accept')!.click();

    expect(sessionStorage.getItem(STORAGE_KEY)).toBe(token);
    expect(auth.login).toHaveBeenCalledTimes(1);
  });

  it('calls acceptInvite with the exact token and shows the success screen on completion', async () => {
    const { fixture, groups, token } = await setup({
      groups: {
        previewInvite: vi.fn(async () => ({ groupName: 'The Andersens' })),
        acceptInvite: vi.fn(async () => {})
      }
    });
    await settle(fixture);

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    expect(groups.acceptInvite).toHaveBeenCalledWith(token);
    expect(groups.acceptInvite).toHaveBeenCalledTimes(1);
    expect(text(fixture)).toContain("You've joined The Andersens.");
    expect(findButton(fixture, 'Go to my groups')).toBeTruthy();
    expect(findButton(fixture, 'Accept invite')).toBeUndefined();
  });

  it('disables the accept button while acceptance is in flight, and re-enables scope is replaced by the success screen', async () => {
    let resolveAccept!: () => void;
    const { fixture } = await setup({
      groups: { acceptInvite: vi.fn(() => new Promise<void>((resolve) => (resolveAccept = resolve))) }
    });
    await settle(fixture);

    const button = findButton(fixture, 'Accept invite')!;
    button.click();
    fixture.detectChanges();

    expect(button.disabled).toBe(true);

    resolveAccept();
    await settle(fixture);

    expect(findButton(fixture, 'Accept invite')).toBeUndefined();
  });

  it('shows the wrong-account error when acceptInvite rejects with a 403', async () => {
    const forbidden = new HttpErrorResponse({ status: 403 });
    const { fixture } = await setup({ groups: { acceptInvite: vi.fn(async () => Promise.reject(forbidden)) } });
    await settle(fixture);

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    expect(text(fixture)).toContain("This invite was sent to a different account than the one you're logged in with.");
    expect(text(fixture)).not.toContain("You've joined");
  });

  it('shows a generic error when acceptInvite rejects with a non-403 error', async () => {
    const { fixture } = await setup({ groups: { acceptInvite: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    expect(text(fixture)).toContain('Unable to accept this invite. It may have expired or already been used.');
  });

  it('shows a generic error, not the wrong-account error, for a 401 HttpErrorResponse', async () => {
    const unauthorized = new HttpErrorResponse({ status: 401 });
    const { fixture } = await setup({ groups: { acceptInvite: vi.fn(async () => Promise.reject(unauthorized)) } });
    await settle(fixture);

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    expect(text(fixture)).toContain('Unable to accept this invite. It may have expired or already been used.');
    expect(text(fixture)).not.toContain("sent to a different account");
  });

  it('clears a previous accept error once a retry succeeds', async () => {
    const acceptInvite = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValueOnce(undefined);
    const { fixture } = await setup({ groups: { acceptInvite } });
    await settle(fixture);

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);
    expect(text(fixture)).toContain('Unable to accept this invite.');

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    expect(text(fixture)).not.toContain('Unable to accept this invite.');
    expect(text(fixture)).toContain("You've joined");
  });

  it('navigates to the guardian admin route when going to groups from the success screen', async () => {
    const { fixture, router } = await setup();
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await settle(fixture);

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    findButton(fixture, 'Go to my groups')!.click();

    expect(navigateSpy).toHaveBeenCalledWith(['/guardian/admin']);
  });

  it('renders the interpolated group name in the success title, distinct from the preview title', async () => {
    const { fixture } = await setup({ groups: { previewInvite: vi.fn(async () => ({ groupName: 'Camp Wonder' })) } });
    await settle(fixture);

    expect(text(fixture)).toContain("You've been invited to join Camp Wonder.");

    findButton(fixture, 'Accept invite')!.click();
    await settle(fixture);

    expect(text(fixture)).toContain("You've joined Camp Wonder.");
  });
});
