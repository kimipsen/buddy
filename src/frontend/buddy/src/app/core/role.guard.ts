import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountService } from './account.service';
import { AuthService } from './auth.service';
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

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  try {
    // Provisions the backend user on first login (memoized, so this is a no-op after the first
    // successful call). Best-effort: a failure here shouldn't block navigation, since read-only
    // pages work without it -- create actions will surface their own error if it's still missing.
    await users.ensureCurrentUser();
  } catch {
    // Ignored -- see comment above.
  }

  const role = await account.resolveRole();

  return router.createUrlTree([role === 'child' ? '/child' : '/guardian']);
};
