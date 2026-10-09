import { describe, expect, it } from 'vitest';

import { ChildCalendar } from './calendar/child-calendar';
import { CHILD_ROUTES } from './child.routes';
import { ChildHome } from './home/home';
import { ChildMealplan } from './mealplan/child-mealplan';

describe('child routes', () => {
  it('serves the child home, meal plan, and calendar pages', () => {
    expect(CHILD_ROUTES).toEqual([
      { path: '', component: ChildHome },
      // Behind featureGuard('mealplans'): feature-routes.spec.ts covers what it lets through.
      { path: 'mealplan', component: ChildMealplan, canActivate: [expect.any(Function)] },
      { path: 'calendar', component: ChildCalendar },
    ]);
  });
});
