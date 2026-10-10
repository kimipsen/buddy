import { Routes } from '@angular/router';

import { featureGuard } from '../../core/feature.guard';
import { ChildCalendar } from './calendar/child-calendar';
import { ChildHome } from './home/home';
import { ChildMealplan } from './mealplan/child-mealplan';
import { ChildRewards } from './rewards/child-rewards';
import { ChildRules } from './rules/child-rules';

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
];
