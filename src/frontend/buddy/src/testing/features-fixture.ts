import { Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { FeatureName, FeaturesService } from '../app/core/features.service';

// A FeaturesService with the given features turned off and every other one on, for specs of the
// screens that hide a disabled feature (docs/backend/analysis/feature-flags.md).
export function provideFeatures(disabled: readonly FeatureName[] = []): Provider {
  return { provide: FeaturesService, useValue: featuresStub(disabled) };
}

// The same, for a spec whose setup() configures the module itself: call it before setup(), and
// the override applies when the module compiles. Specs that don't call it get the real service,
// which has every feature on until it loads.
export function disableFeatures(...disabled: FeatureName[]): void {
  TestBed.overrideProvider(FeaturesService, { useValue: featuresStub(disabled) });
}

function featuresStub(
  disabled: readonly FeatureName[],
): Pick<FeaturesService, 'enabled' | 'offers'> {
  const enabled = (name: FeatureName) => !disabled.includes(name);
  return { enabled, offers: (item) => item.feature === undefined || enabled(item.feature) };
}
