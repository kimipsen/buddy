import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountService } from './account.service';
import { OnboardingService } from './onboarding.service';

// On the guardian home route, so both the post-login redirect (which lands on /guardian) and a
// direct visit pass through it. Only the home redirects: invitations, settings and every other
// guardian page stay reachable while the guide is active. A child account never enters the guide,
// and a failed lookup shows the home rather than guessing the account is new -- the dashboard's
// own widgets then show their load errors.
export const onboardingEntryGuard: CanActivateFn = async () => {
  const account = inject(AccountService);
  const onboarding = inject(OnboardingService);
  const router = inject(Router);

  try {
    if ((await account.resolveRole()) !== 'guardian') {
      return true;
    }

    return (await onboarding.shouldEnterGuide())
      ? router.createUrlTree(['/guardian/onboarding'])
      : true;
  } catch {
    return true;
  }
};
