import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { UsersService } from './users.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const users = inject(UsersService);
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

  return true;
};
