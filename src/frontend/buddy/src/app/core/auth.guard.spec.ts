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

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { takePendingReturnUrl } from './pending-return-url';
import { UsersService } from './users.service';

describe('authGuard', () => {
  interface Stubs {
    auth?: Partial<AuthService>;
    users?: Partial<UsersService>;
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

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: UsersService, useValue: usersStub },
      ],
    });

    const router = TestBed.inject(Router);

    return { authStub, usersStub, router };
  }

  function runGuard(url = '/guardian/calendar') {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
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
    const { router, usersStub } = setup({ auth: { getAccessToken: vi.fn(async () => null) } });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
    // Provisioning is skipped entirely for an unauthenticated visitor.
    expect(usersStub.ensureCurrentUser).not.toHaveBeenCalled();
    // Only an expired session is sent back where it was.
    expect(takePendingReturnUrl()).toBeNull();
  });

  it('redirects an expired session to /login with the reason and remembers the target page', async () => {
    const { router, usersStub } = setup({
      auth: {
        getAccessToken: vi.fn(async () => null),
        sessionExpired: signal(true).asReadonly(),
      },
    });

    const result = await runGuard('/guardian/mealplan?week=2026-W40');

    expect(router.serializeUrl(result as UrlTree)).toBe('/login?reason=session-expired');
    expect(takePendingReturnUrl()).toBe('/guardian/mealplan?week=2026-W40');
    expect(usersStub.ensureCurrentUser).not.toHaveBeenCalled();
  });

  it('allows navigation and provisions the backend user when authenticated', async () => {
    const { usersStub } = setup();

    const result = await runGuard();

    expect(result).toBe(true);
    expect(usersStub.ensureCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('still allows navigation when provisioning the current user fails', async () => {
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => Promise.reject(new Error('boom'))),
    };
    setup({ users: usersStub });

    const result = await runGuard();

    expect(result).toBe(true);
  });
});
