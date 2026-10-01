import { Route } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { routes } from './app.routes';
import { authGuard } from './core/auth.guard';
import { roleRedirectGuard } from './core/role.guard';
import { CHILD_ROUTES } from './features/child/child.routes';
import { GUARDIAN_ROUTES } from './features/guardian/guardian.routes';
import { AcceptGuardianInvite } from './features/invite/accept-guardian-invite';
import { AcceptInvite } from './features/invite/accept-invite';
import { Login } from './features/login/login';
import { VerifyEmail } from './features/verify-email/verify-email';

describe('app routes', () => {
  function route(path: string): Route {
    const match = routes.find((r) => r.path === path);

    if (!match) {
      throw new Error(`No route with path '${path}'`);
    }

    return match;
  }

  it.each([
    ['login', Login],
    ['invite/:token', AcceptInvite],
    ['guardian-invite/:token', AcceptGuardianInvite],
    ['verify-email/:token', VerifyEmail],
  ])('serves %s publicly, without an auth guard', (path, component) => {
    const r = route(path);

    expect(r.component).toBe(component);
    expect(r.canActivate).toBeUndefined();
  });

  it.each([
    ['guardian', GUARDIAN_ROUTES],
    ['child', CHILD_ROUTES],
  ])('lazy-loads the %s tree behind authGuard', async (path, children) => {
    const r = route(path);

    expect(r.canActivate).toEqual([authGuard]);
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
