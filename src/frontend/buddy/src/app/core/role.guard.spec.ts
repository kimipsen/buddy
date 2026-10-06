import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AccountService, AccountRole } from './account.service';
import { AuthService } from './auth.service';
import { storePendingGuardianInviteToken } from './pending-guardian-invite-token';
import { storePendingInviteToken } from './pending-invite-token';
import { storePendingReturnUrl } from './pending-return-url';
import { storePendingVerifyEmailToken } from './pending-verify-email-token';
import { roleRedirectGuard } from './role.guard';
import { UsersService } from './users.service';

describe('roleRedirectGuard', () => {
  interface Stubs {
    auth?: Partial<AuthService>;
    users?: Partial<UsersService>;
    account?: Partial<AccountService>;
  }

  beforeEach(() => {
    sessionStorage.clear();
  });

  function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = {
      completeLoginRedirect: vi.fn(async () => {}),
      getAccessToken: vi.fn(async () => 'access-token'),
      sessionExpired: signal(false).asReadonly(),
      ...stubs.auth,
    };
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => ({}) as never),
      ...stubs.users,
    };
    const accountStub: Partial<AccountService> = {
      resolveRole: vi.fn(async () => 'guardian' as AccountRole),
      ...stubs.account,
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: UsersService, useValue: usersStub },
        { provide: AccountService, useValue: accountStub },
      ],
    });

    const router = TestBed.inject(Router);

    return { authStub, usersStub, accountStub, router };
  }

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      roleRedirectGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  it('completes any pending login redirect before checking authentication', async () => {
    const calls: string[] = [];
    const authStub: Partial<AuthService> = {
      completeLoginRedirect: vi.fn(async () => {
        calls.push('completeLoginRedirect');
      }),
      getAccessToken: vi.fn(async () => {
        calls.push('getAccessToken');
        return 'access-token';
      }),
    };
    setup({ auth: authStub });

    await runGuard();

    expect(calls).toEqual(['completeLoginRedirect', 'getAccessToken']);
  });

  it('redirects to /login when the user never signed in', async () => {
    const { router, accountStub } = setup({
      auth: { getAccessToken: vi.fn(async () => null) },
    });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
    expect(accountStub.resolveRole).not.toHaveBeenCalled();
  });

  it('redirects an expired session to /login with the reason', async () => {
    const { router, accountStub } = setup({
      auth: { getAccessToken: vi.fn(async () => null), sessionExpired: signal(true).asReadonly() },
    });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/login?reason=session-expired');
    expect(accountStub.resolveRole).not.toHaveBeenCalled();
  });

  it('provisions the backend user once authenticated, before redirecting', async () => {
    const { usersStub } = setup();

    await runGuard();

    expect(usersStub.ensureCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('still resolves a role when provisioning the current user fails', async () => {
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => Promise.reject(new Error('boom'))),
    };
    const { router } = setup({
      users: usersStub,
      account: { resolveRole: vi.fn(async () => 'guardian' as AccountRole) },
    });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian');
  });

  it('redirects a guardian to /guardian', async () => {
    const { router } = setup({
      account: { resolveRole: vi.fn(async () => 'guardian' as AccountRole) },
    });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian');
  });

  it('redirects a child to /child', async () => {
    const { router } = setup({
      account: { resolveRole: vi.fn(async () => 'child' as AccountRole) },
    });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/child');
  });

  it('redirects to the pending group-invite route and consumes the token, without resolving a role', async () => {
    storePendingInviteToken('invite-token-1');
    const { router, accountStub } = setup();

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/invite/invite-token-1');
    expect(accountStub.resolveRole).not.toHaveBeenCalled();
    expect(sessionStorage.getItem('buddy_pending_invite_token')).toBeNull();
  });

  it('redirects to the pending guardian-invite route when there is no group-invite token', async () => {
    storePendingGuardianInviteToken('guardian-invite-token-1');
    const { router, accountStub } = setup();

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian-invite/guardian-invite-token-1');
    expect(accountStub.resolveRole).not.toHaveBeenCalled();
  });

  it('redirects to the pending verify-email route when there is no invite token of any kind', async () => {
    storePendingVerifyEmailToken('verify-token-1');
    const { router, accountStub } = setup();

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/verify-email/verify-token-1');
    expect(accountStub.resolveRole).not.toHaveBeenCalled();
  });

  it('prefers the group-invite token over a guardian-invite token when both are pending', async () => {
    storePendingInviteToken('invite-token-1');
    storePendingGuardianInviteToken('guardian-invite-token-1');
    const { router } = setup();

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/invite/invite-token-1');
  });

  it('prefers a guardian-invite token over a verify-email token when both are pending', async () => {
    storePendingGuardianInviteToken('guardian-invite-token-1');
    storePendingVerifyEmailToken('verify-token-1');
    const { router } = setup();

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian-invite/guardian-invite-token-1');
  });

  describe('pending return URL', () => {
    it('returns a guardian to the page their expired session interrupted', async () => {
      storePendingReturnUrl('/guardian/mealplan?week=2026-W40');
      const { router } = setup();

      const result = await runGuard();

      expect(router.serializeUrl(result as UrlTree)).toBe('/guardian/mealplan?week=2026-W40');
      expect(sessionStorage.getItem('buddy_pending_return_url')).toBeNull();
    });

    it('returns a child to a page in the child tree', async () => {
      storePendingReturnUrl('/child/calendar');
      const { router } = setup({
        account: { resolveRole: vi.fn(async () => 'child' as AccountRole) },
      });

      const result = await runGuard();

      expect(router.serializeUrl(result as UrlTree)).toBe('/child/calendar');
    });

    it('accepts the role home itself, with or without a query string', async () => {
      storePendingReturnUrl('/guardian?tab=today');
      const { router } = setup();

      const result = await runGuard();

      expect(router.serializeUrl(result as UrlTree)).toBe('/guardian?tab=today');
    });

    it("ignores a page outside the signed-in user's role tree", async () => {
      storePendingReturnUrl('/guardian/calendar');
      const { router } = setup({
        account: { resolveRole: vi.fn(async () => 'child' as AccountRole) },
      });

      const result = await runGuard();

      expect(router.serializeUrl(result as UrlTree)).toBe('/child');
    });

    it("doesn't mistake a path that merely starts with the role name for the role tree", async () => {
      storePendingReturnUrl('/guardian-invite/some-token');
      const { router } = setup();

      const result = await runGuard();

      expect(router.serializeUrl(result as UrlTree)).toBe('/guardian');
    });

    it('lets a pending invite win, and still clears the return URL', async () => {
      storePendingReturnUrl('/guardian/calendar');
      storePendingInviteToken('invite-token-1');
      const { router } = setup();

      const result = await runGuard();

      expect(router.serializeUrl(result as UrlTree)).toBe('/invite/invite-token-1');
      expect(sessionStorage.getItem('buddy_pending_return_url')).toBeNull();
    });
  });
});
