import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { FeatureName, FeaturesService } from './features.service';

// On every route of a feature the installation can turn off. A bookmark or an old link to a
// disabled feature goes to '' instead, where roleRedirectGuard picks the right home.
export function featureGuard(name: FeatureName): CanActivateFn {
  // Stryker disable next-line ArrayDeclaration,StringLiteral: createUrlTree([]) and createUrlTree(['']) are the root URL too.
  return () => (inject(FeaturesService).enabled(name) ? true : inject(Router).createUrlTree(['/']));
}
