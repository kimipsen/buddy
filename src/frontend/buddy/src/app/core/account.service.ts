import { Injectable, computed, inject } from '@angular/core';

import { AuthService } from './auth.service';
import { CHILD_GROUP, GUARDIAN_GROUP } from './keycloak-groups';

export type AccountRole = 'guardian' | 'child';

// The account's role is its Keycloak group (keycloak-groups.ts), read from the access token:
// buddy-guardian opens the guardian tree, buddy-child the child tree, and an account in both is a
// guardian. Null for an account in neither, which can't use Buddy at all.
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly auth = inject(AuthService);

  readonly role = computed<AccountRole | null>(() => {
    const groups = this.auth.groups();

    if (groups.includes(GUARDIAN_GROUP)) {
      return 'guardian';
    }

    return groups.includes(CHILD_GROUP) ? 'child' : null;
  });
}
