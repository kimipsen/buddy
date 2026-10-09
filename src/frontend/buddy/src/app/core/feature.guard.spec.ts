import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { featureGuard } from './feature.guard';
import { FeatureName, FeaturesService } from './features.service';

describe('featureGuard', () => {
  function setup(disabled: FeatureName[]) {
    const enabled = vi.fn((name: FeatureName) => !disabled.includes(name));

    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: FeaturesService, useValue: { enabled } }],
    });

    const run = (name: FeatureName) =>
      TestBed.runInInjectionContext(() =>
        featureGuard(name)({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      );

    return { run, enabled, router: TestBed.inject(Router) };
  }

  it('lets the navigation through while the feature is on', () => {
    const { run, enabled } = setup([]);

    expect(run('medicines')).toBe(true);
    expect(enabled).toHaveBeenCalledWith('medicines');
  });

  it('sends a navigation to a disabled feature to the root, where the role redirect takes over', () => {
    const { run, router } = setup(['medicines']);

    const result = run('medicines');

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/');
  });
});
