import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { UsersService } from '../../core/users.service';

// On the focus view: a child whose guardian turned visual timers off (or a bookmark from before
// that) lands on the child home instead. If the child can't be resolved, home is the safe place
// too.
export const visualTimersGuard: CanActivateFn = async () => {
  const router = inject(Router);
  const users = inject(UsersService);

  try {
    const me = await users.ensureCurrentUser();
    return me.showVisualTimers === false ? router.createUrlTree(['/child']) : true;
  } catch {
    return router.createUrlTree(['/child']);
  }
};
