import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  provideRouter,
  Route,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { describe, expect, it } from 'vitest';

import { provideFeatures } from '../testing/features-fixture';
import { routes } from './app.routes';
import { FeatureName } from './core/features.service';
import { CHILD_ROUTES } from './features/child/child.routes';
import { GUARDIAN_ROUTES } from './features/guardian/guardian.routes';

// Every route of an optional feature sits behind featureGuard for that feature
// (docs/backend/analysis/feature-flags.md): on, the navigation goes through; off, it is redirected.
describe('feature routes', () => {
  const guardianChildren = GUARDIAN_ROUTES.flatMap((r) => r.children ?? []);

  const cases: [string, Route[], string, FeatureName][] = [
    ['app', routes, 'shared/sleep-diary/:token', 'sleepDiary'],
    ['guardian', GUARDIAN_ROUTES, 'print/sheet/:templateId', 'printing'],
    ['guardian', GUARDIAN_ROUTES, 'house-rules/print/children/:childId', 'houseRules'],
    ['guardian', GUARDIAN_ROUTES, 'house-rules/print/groups/:groupId', 'houseRules'],
    ['guardian', guardianChildren, 'mealplan', 'mealplans'],
    ['guardian', guardianChildren, 'mealplan/ai-assistant', 'mealplanAiAssistant'],
    ['guardian', guardianChildren, 'mealplan/import', 'mealplanImport'],
    ['guardian', guardianChildren, 'medicine', 'medicines'],
    ['guardian', guardianChildren, 'sleep-diary', 'sleepDiary'],
    ['guardian', guardianChildren, 'house-rules', 'houseRules'],
    ['guardian', guardianChildren, 'progress', 'progress'],
    ['guardian', guardianChildren, 'pickup', 'pickups'],
    ['guardian', guardianChildren, 'babysitters', 'babysitters'],
    ['guardian', guardianChildren, 'work-locations', 'workLocations'],
    ['guardian', guardianChildren, 'print', 'printing'],
    ['guardian', guardianChildren, 'print/templates/:templateId', 'printing'],
    ['guardian', guardianChildren, 'task-library', 'taskLibrary'],
    ['guardian', guardianChildren, 'help', 'help'],
    ['child', CHILD_ROUTES, 'mealplan', 'mealplans'],
    ['child', CHILD_ROUTES, 'rewards', 'progress'],
    ['child', CHILD_ROUTES, 'rules', 'houseRules'],
  ];

  function guard(list: Route[], path: string): CanActivateFn {
    const match = list.find((r) => r.path === path);
    expect(match?.canActivate).toHaveLength(1);
    return match!.canActivate![0] as CanActivateFn;
  }

  function run(canActivate: CanActivateFn, disabled: FeatureName[]) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideFeatures(disabled)] });
    return TestBed.runInInjectionContext(() =>
      canActivate({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  it.each(cases)('%s route %s needs %s', (_, list, path, feature) => {
    const canActivate = guard(list, path);

    expect(run(canActivate, [])).toBe(true);
    expect(run(canActivate, [feature])).toBeInstanceOf(UrlTree);
  });

  it.each([
    ['guardian', guardianChildren, 'calendar'],
    ['guardian', guardianChildren, 'admin'],
    ['child', CHILD_ROUTES, 'calendar'],
  ] as [string, Route[], string][])(
    '%s route %s is core and has no feature guard',
    (_, list, path) => {
      expect(list.find((r) => r.path === path)?.canActivate).toBeUndefined();
    },
  );
});
