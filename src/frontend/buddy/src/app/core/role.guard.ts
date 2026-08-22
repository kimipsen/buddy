import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountService } from './account.service';

// Sends an authenticated user to the UI tree matching their role -- always redirects, never
// activates the route it's attached to.
export const roleRedirectGuard: CanActivateFn = async () => {
  const account = inject(AccountService);
  const router = inject(Router);

  const role = await account.resolveRole();

  return router.createUrlTree([role === 'child' ? '/child' : '/guardian']);
};
