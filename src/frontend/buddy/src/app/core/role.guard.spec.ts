import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AccountService, AccountRole } from './account.service';
import { AuthService } from './auth.service';
import { storePendingGuardianInviteToken } from './pending-guardian-invite-token';
import { storePendingInviteToken } from './pending-invite-token';
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
      isAuthenticated: signal(true).asReadonly(),
      ...stubs.auth
    };
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => ({}) as never),
      ...stubs.users
    };
    const accountStub: Partial<AccountService> = {
      resolveRole: vi.fn(async () => 'guardian' as AccountRole),
      ...stubs.account
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: UsersService, useValue: usersStub },
        { provide: AccountService, useValue: accountStub }
      ]
    });

    const router = TestBed.inject(Router);

    return { authStub, usersStub, accountStub, router };
  }

  function runGuard() {
    return TestBed.runInInjectionContext(() => roleRedirectGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  }

  it('completes any pending login redirect before checking authentication', async () => {
    const calls: string[] = [];
    const authStub: Partial<AuthService> = {
      completeLoginRedirect: vi.fn(async () => {
        calls.push('completeLoginRedirect');
      }),
      isAuthenticated: computed(() => {
        calls.push('isAuthenticated');
        return true;
      })
    };
    setup({ auth: authStub });

    await runGuard();

    expect(calls).toEqual(['completeLoginRedirect', 'isAuthenticated']);
  });

  it('redirects to /login when the user is not authenticated', async () => {
    const { router, accountStub } = setup({ auth: { isAuthenticated: signal(false).asReadonly() } });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
    expect(accountStub.resolveRole).not.toHaveBeenCalled();
  });

  it('still resolves a role when provisioning the current user fails', async () => {
    const usersStub: Partial<UsersService> = { ensureCurrentUser: vi.fn(async () => Promise.reject(new Error('boom'))) };
    const { router } = setup({ users: usersStub, account: { resolveRole: vi.fn(async () => 'guardian' as AccountRole) } });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian');
  });

  it('redirects a guardian to /guardian', async () => {
    const { router } = setup({ account: { resolveRole: vi.fn(async () => 'guardian' as AccountRole) } });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/guardian');
  });

  it('redirects a child to /child', async () => {
    const { router } = setup({ account: { resolveRole: vi.fn(async () => 'child' as AccountRole) } });

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
});
