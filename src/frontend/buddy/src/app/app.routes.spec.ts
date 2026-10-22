import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  provideRouter,
  Route,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { routes } from './app.routes';
import { FeaturesService } from './core/features.service';
import { childGroupGuard, guardianGroupGuard } from './core/group.guard';
import { roleRedirectGuard } from './core/role.guard';
import { CHILD_ROUTES } from './features/child/child.routes';
import { GUARDIAN_ROUTES } from './features/guardian/guardian.routes';
import { AcceptGuardianInvite } from './features/invite/accept-guardian-invite';
import { AcceptInvite } from './features/invite/accept-invite';
import { Login } from './features/login/login';
import { SharedSleepDiary } from './features/shared-sleep-diary/shared-sleep-diary';
import { VerifyEmail } from './features/verify-email/verify-email';

describe('app routes', () => {
  function route(path: string): Route {
    const match = routes.find((r) => r.path === path);

    if (!match) {
      throw new Error(`No route with path '${path}'`);
    }

    return match;
  }

  it('serves the login page from the initial bundle, without an auth guard', () => {
    const r = route('login');

    expect(r.component).toBe(Login);
    expect(r.canActivate).toBeUndefined();
  });

  it.each([
    ['invite/:token', AcceptInvite],
    ['guardian-invite/:token', AcceptGuardianInvite],
    ['verify-email/:token', VerifyEmail],
  ])('lazy-loads %s publicly, without an auth guard', async (path, component) => {
    const r = route(path);

    expect(r.component).toBeUndefined();
    await expect((r.loadComponent as () => Promise<unknown>)()).resolves.toBe(component);
    expect(r.canActivate).toBeUndefined();
  });

  it('lazy-loads the shared sleep diary behind its feature guard only', async () => {
    const r = route('shared/sleep-diary/:token');

    await expect((r.loadComponent as () => Promise<unknown>)()).resolves.toBe(SharedSleepDiary);
    expect(r.canActivate).toHaveLength(1);
  });

  it.each([
    ['shared/sleep-diary/:token', 'sleepDiary'],
    ['shared/feelings/:token', 'feelings'],
    ['shared/visit-summary/:token', 'visitSummaries'],
  ] as const)('guards %s with the %s feature flag', (path, feature) => {
    const enabled = vi.fn(() => false);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: FeaturesService, useValue: { enabled } }],
    });
    const guards = route(path).canActivate as CanActivateFn[];

    expect(guards).toHaveLength(1);
    const result = TestBed.runInInjectionContext(() =>
      guards[0]({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );

    expect(enabled).toHaveBeenCalledExactlyOnceWith(feature);
    expect(result).toBeInstanceOf(UrlTree);
  });

  it.each([
    ['guardian', GUARDIAN_ROUTES, guardianGroupGuard],
    ['child', CHILD_ROUTES, childGroupGuard],
  ])('lazy-loads the %s tree behind its group guard', async (path, children, guard) => {
    const r = route(path);

    expect(r.canActivate).toEqual([guard]);
    expect(r.canActivateChild).toEqual([guard]);
    await expect((r.loadChildren as () => Promise<unknown>)()).resolves.toBe(children);
  });

  it('sends the empty path through roleRedirectGuard', () => {
    const r = route('');

    expect(r.pathMatch).toBe('full');
    expect(r.canActivate).toEqual([roleRedirectGuard]);
    expect(r.children).toEqual([]);
  });

  it('redirects unknown paths to the root, as the last route', () => {
    expect(routes.at(-1)).toEqual({ path: '**', redirectTo: '' });
  });
});
