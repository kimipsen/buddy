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

import { AuthService } from './auth.service';
import { groupGuard } from './group.guard';
import { BuddyGroup } from './keycloak-groups';
import { UsersService } from './users.service';

describe('groupGuard', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  function setup(auth: Partial<AuthService> = {}) {
    const authStub: Partial<AuthService> = {
      completeLoginRedirect: vi.fn(async () => {}),
      getAccessToken: vi.fn(async () => 'access-token'),
      sessionExpired: signal(false).asReadonly(),
      groups: signal(['buddy-guardian']),
      ...auth,
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        {
          provide: UsersService,
          useValue: { ensureCurrentUser: vi.fn(async () => ({}) as never) },
        },
      ],
    });

    return { router: TestBed.inject(Router) };
  }

  function runGuard(group: BuddyGroup) {
    return TestBed.runInInjectionContext(() =>
      groupGuard(group)({} as ActivatedRouteSnapshot, { url: '/guardian' } as RouterStateSnapshot),
    );
  }

  it('lets a member of the group in', async () => {
    setup({ groups: signal(['buddy-child']) });

    await expect(runGuard('buddy-child')).resolves.toBe(true);
  });

  it('sends an account outside the group to the root, which picks its own tree', async () => {
    const { router } = setup({ groups: signal(['buddy-child']) });

    const result = await runGuard('buddy-guardian');

    expect(router.serializeUrl(result as UrlTree)).toBe('/');
  });

  it('sends an account in neither group to the root too', async () => {
    const { router } = setup({ groups: signal([]) });

    const result = await runGuard('buddy-child');

    expect(router.serializeUrl(result as UrlTree)).toBe('/');
  });

  it("returns authGuard's redirect when the user isn't signed in", async () => {
    const { router } = setup({ getAccessToken: vi.fn(async () => null), groups: signal([]) });

    const result = await runGuard('buddy-guardian');

    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });
});
