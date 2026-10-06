import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountService } from './account.service';
import { AuthService } from './auth.service';
import { takePendingGuardianInviteToken } from './pending-guardian-invite-token';
import { takePendingInviteToken } from './pending-invite-token';
import { takePendingReturnUrl } from './pending-return-url';
import { takePendingVerifyEmailToken } from './pending-verify-email-token';
import { SESSION_EXPIRED_QUERY_PARAMS } from './session-expired';
import { UsersService } from './users.service';

// Completes login and sends the user to the UI tree matching their role -- always redirects,
// never activates the route it's attached to.
//
// This subsumes authGuard's token-exchange step rather than running alongside it as a second
// canActivate guard: Angular evaluates multiple guards on the same route in parallel, not in
// sequence, so a separate role-lookup guard could fire its API call before the token exchange
// finished, sending the request unauthenticated and getting a 401.
export const roleRedirectGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const users = inject(UsersService);
  const account = inject(AccountService);
  const router = inject(Router);

  await auth.completeLoginRedirect();

  // Same freshness check as authGuard: a stored but dead session goes to /login, not on to a home
  // route whose API calls would all fail.
  if ((await auth.getAccessToken()) === null) {
    return auth.sessionExpired()
      ? router.createUrlTree(['/login'], { queryParams: SESSION_EXPIRED_QUERY_PARAMS })
      : router.createUrlTree(['/login']);
  }

  try {
    // Provisions the backend user on first login (memoized, so this is a no-op after the first
    // successful call). Best-effort: a failure here doesn't block navigation -- the page shows its
    // own load error (the API answers 403 user_not_provisioned) and the next navigation retries.
    await users.ensureCurrentUser();
  } catch {
    // Ignored -- see comment above.
  }

  // Taken (and so cleared) up front, so a pending invite that wins below doesn't leave it behind
  // for some later login.
  const returnUrl = takePendingReturnUrl();

  // A guardian who wasn't logged in yet when they opened a group-invite link gets sent through
  // login and would otherwise land on the normal role-based home route, losing the invite. See
  // pending-invite-token.ts for why this app needs its own narrow stand-in rather than a general
  // return-url mechanism.
  const pendingInviteToken = takePendingInviteToken();

  if (pendingInviteToken) {
    return router.createUrlTree(['/invite', pendingInviteToken]);
  }

  // Same stand-in as above, for a guardian-invite link followed while logged out.
  const pendingGuardianInviteToken = takePendingGuardianInviteToken();

  if (pendingGuardianInviteToken) {
    return router.createUrlTree(['/guardian-invite', pendingGuardianInviteToken]);
  }

  // Same stand-in as above, for a user who followed a "verify your email" link while logged out.
  const pendingVerifyEmailToken = takePendingVerifyEmailToken();

  if (pendingVerifyEmailToken) {
    return router.createUrlTree(['/verify-email', pendingVerifyEmailToken]);
  }

  const role = await account.resolveRole();
  const home = role === 'child' ? '/child' : '/guardian';

  // Back to the page an expired session interrupted (see pending-return-url.ts), but only inside
  // this user's own tree: whoever signs in next in this tab may not be who was signed in before.
  if (returnUrl && isWithin(returnUrl, home)) {
    return router.parseUrl(returnUrl);
  }

  return router.createUrlTree([home]);
};

// '/guardian-invite/...' starts with '/guardian' too, so match whole path segments only.
function isWithin(url: string, home: string): boolean {
  return url === home || url.startsWith(`${home}/`) || url.startsWith(`${home}?`);
}
