import { Injectable, inject, signal } from '@angular/core';

import { GuardiansService } from './guardians.service';

export type AccountRole = 'guardian' | 'child';

// Derives the account's role from the guardian-link domain model rather than a stored flag: a
// user with at least one active guardian is a child account, since guardians never themselves
// have a guardian in this single-realm model (see docs/backend/analysis/child-accounts-and-guardian-roles.md).
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly guardians = inject(GuardiansService);
  private readonly _role = signal<AccountRole | null>(null);
  private pendingResolution: Promise<AccountRole> | null = null;

  readonly role = this._role.asReadonly();

  // The settled promise is kept after a successful lookup, so it doubles as the memoized result --
  // later calls reuse it instead of re-fetching. Only a failure clears it, allowing a retry.
  async resolveRole(): Promise<AccountRole> {
    this.pendingResolution ??= this.loadRole().catch((error: unknown) => {
      this.pendingResolution = null;
      throw error;
    });

    return this.pendingResolution;
  }

  private async loadRole(): Promise<AccountRole> {
    const myGuardians = await this.guardians.listMyGuardians();
    const role: AccountRole = myGuardians.length > 0 ? 'child' : 'guardian';

    this._role.set(role);

    return role;
  }
}
