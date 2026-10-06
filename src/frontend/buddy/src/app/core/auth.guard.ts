import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { storePendingReturnUrl } from './pending-return-url';
import { SESSION_EXPIRED_QUERY_PARAMS } from './session-expired';
import { UsersService } from './users.service';

// Used as both canActivate and canActivateChild on the guardian and child trees, so it runs on
// every navigation inside them, not just on first entry.
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const users = inject(UsersService);
  const router = inject(Router);

  await auth.completeLoginRedirect();

  // isAuthenticated() only says a token set exists; getAccessToken() also refreshes it, so an
  // ended Keycloak session lands on /login before the page renders instead of failing its API
  // calls.
  if ((await auth.getAccessToken()) === null) {
    if (!auth.sessionExpired()) {
      return router.createUrlTree(['/login']);
    }

    storePendingReturnUrl(state.url);
    return router.createUrlTree(['/login'], { queryParams: SESSION_EXPIRED_QUERY_PARAMS });
  }

  try {
    // Provisions the backend user on first login (memoized, so this is a no-op after the first
    // successful call). Best-effort: a failure here doesn't block navigation -- the page shows its
    // own load error (the API answers 403 user_not_provisioned) and the next navigation retries.
    await users.ensureCurrentUser();
  } catch {
    // Ignored -- see comment above.
  }

  return true;
};
