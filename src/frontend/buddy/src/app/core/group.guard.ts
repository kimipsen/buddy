import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { BuddyGroup, CHILD_GROUP, GUARDIAN_GROUP } from './keycloak-groups';

// authGuard plus Keycloak group membership, for a whole role tree: '/guardian' needs
// buddy-guardian, '/child' needs buddy-child. Anyone else goes to the root, where
// roleRedirectGuard sends them to their own tree, or out through logout if they're in neither
// group. One guard rather than authGuard next to a group check: Angular runs a route's guards in
// parallel, and the group check needs the token authGuard may still be exchanging.
export function groupGuard(group: BuddyGroup): CanActivateFn {
  return async (route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    // Called before any await, while still in the injection context authGuard's inject()s need.
    const authenticated = await authGuard(route, state);

    if (authenticated !== true) {
      return authenticated;
    }

    return auth.groups().includes(group) ? true : router.createUrlTree(['/']);
  };
}

export const guardianGroupGuard = groupGuard(GUARDIAN_GROUP);
export const childGroupGuard = groupGuard(CHILD_GROUP);
