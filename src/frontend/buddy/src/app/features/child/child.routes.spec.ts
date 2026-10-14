import { describe, expect, it } from 'vitest';

import { ChildCalendar } from './calendar/child-calendar';
import { CHILD_ROUTES } from './child.routes';
import { ChildFocus } from './focus/child-focus';
import { ChildHome } from './home/home';
import { ChildHomework } from './homework/child-homework';
import { ChildMealplan } from './mealplan/child-mealplan';
import { ChildRewards } from './rewards/child-rewards';
import { ChildFirstThen } from './first-then/child-first-then';
import { ChildRules } from './rules/child-rules';
import { visualTimersGuard } from './visual-timers.guard';

describe('child routes', () => {
  it('serves the child home, meal plan, calendar, homework, rewards, rules, First – Then and focus pages', () => {
    expect(CHILD_ROUTES).toEqual([
      { path: '', component: ChildHome },
      // Behind featureGuard('mealplans') / featureGuard('progress') / featureGuard('houseRules'):
      // feature-routes.spec.ts covers what they let through.
      { path: 'mealplan', component: ChildMealplan, canActivate: [expect.any(Function)] },
      { path: 'calendar', component: ChildCalendar },
      { path: 'homework', component: ChildHomework, canActivate: [expect.any(Function)] },
      { path: 'rewards', component: ChildRewards, canActivate: [expect.any(Function)] },
      { path: 'rules', component: ChildRules, canActivate: [expect.any(Function)] },
      { path: 'first-then', component: ChildFirstThen, canActivate: [expect.any(Function)] },
      // featureGuard('taskLibrary') first, then the per-child visual timer switch.
      {
        path: 'focus/:itemId',
        component: ChildFocus,
        canActivate: [expect.any(Function), visualTimersGuard],
      },
    ]);
  });
});
