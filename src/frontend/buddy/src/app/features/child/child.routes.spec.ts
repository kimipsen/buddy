import { describe, expect, it } from 'vitest';

import { ChildCalendar } from './calendar/child-calendar';
import { CHILD_ROUTES } from './child.routes';
import { ChildHome } from './home/home';
import { ChildMealplan } from './mealplan/child-mealplan';
import { ChildRewards } from './rewards/child-rewards';
import { ChildRules } from './rules/child-rules';

describe('child routes', () => {
  it('serves the child home, meal plan, calendar, rewards and rules pages', () => {
    expect(CHILD_ROUTES).toEqual([
      { path: '', component: ChildHome },
      // Behind featureGuard('mealplans') / featureGuard('progress') / featureGuard('houseRules'):
      // feature-routes.spec.ts covers what they let through.
      { path: 'mealplan', component: ChildMealplan, canActivate: [expect.any(Function)] },
      { path: 'calendar', component: ChildCalendar },
      { path: 'rewards', component: ChildRewards, canActivate: [expect.any(Function)] },
      { path: 'rules', component: ChildRules, canActivate: [expect.any(Function)] },
    ]);
  });
});
