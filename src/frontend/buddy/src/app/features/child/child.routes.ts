import { Routes } from '@angular/router';

import { featureGuard } from '../../core/feature.guard';
import { ChildCalendar } from './calendar/child-calendar';
import { ChildFocus } from './focus/child-focus';
import { ChildHome } from './home/home';
import { ChildMealplan } from './mealplan/child-mealplan';
import { ChildRewards } from './rewards/child-rewards';
import { ChildRules } from './rules/child-rules';
import { visualTimersGuard } from './visual-timers.guard';

export const CHILD_ROUTES: Routes = [
  {
    path: '',
    component: ChildHome,
  },
  {
    path: 'mealplan',
    component: ChildMealplan,
    canActivate: [featureGuard('mealplans')],
  },
  {
    path: 'calendar',
    component: ChildCalendar,
  },
  {
    path: 'rewards',
    component: ChildRewards,
    canActivate: [featureGuard('progress')],
  },
  {
    path: 'rules',
    component: ChildRules,
    canActivate: [featureGuard('houseRules')],
  },
  {
    // No feature flag of its own: without the task library there are no template runs to time.
    path: 'focus/:itemId',
    component: ChildFocus,
    canActivate: [featureGuard('taskLibrary'), visualTimersGuard],
  },
];
