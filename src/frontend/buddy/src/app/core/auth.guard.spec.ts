import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { UsersService } from './users.service';

describe('authGuard', () => {
  interface Stubs {
    auth?: Partial<AuthService>;
    users?: Partial<UsersService>;
  }

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

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: authStub }, { provide: UsersService, useValue: usersStub }]
    });

    const router = TestBed.inject(Router);

    return { authStub, usersStub, router };
  }

  function runGuard() {
    return TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
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
    const { router, usersStub } = setup({ auth: { isAuthenticated: signal(false).asReadonly() } });

    const result = await runGuard();

    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
    // Provisioning is skipped entirely for an unauthenticated visitor.
    expect(usersStub.ensureCurrentUser).not.toHaveBeenCalled();
  });

  it('allows navigation and provisions the backend user when authenticated', async () => {
    const { usersStub } = setup();

    const result = await runGuard();

    expect(result).toBe(true);
    expect(usersStub.ensureCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('still allows navigation when provisioning the current user fails', async () => {
    const usersStub: Partial<UsersService> = { ensureCurrentUser: vi.fn(async () => Promise.reject(new Error('boom'))) };
    setup({ users: usersStub });

    const result = await runGuard();

    expect(result).toBe(true);
  });
});
