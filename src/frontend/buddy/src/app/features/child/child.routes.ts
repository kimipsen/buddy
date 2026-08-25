import { Routes } from '@angular/router';

import { ChildHome } from './home/home';
import { ChildMealplan } from './mealplan/child-mealplan';

export const CHILD_ROUTES: Routes = [
  {
    path: '',
    component: ChildHome
  },
  {
    path: 'mealplan',
    component: ChildMealplan
  }
];
