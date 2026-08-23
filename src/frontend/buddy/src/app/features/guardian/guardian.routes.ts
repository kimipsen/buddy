import { Routes } from '@angular/router';

import { GuardianAdmin } from './admin/admin';
import { GuardianDashboard } from './dashboard';
import { GuardianMealplan } from './mealplan/mealplan';
import { GuardianShell } from './shell/guardian-shell';

export const GUARDIAN_ROUTES: Routes = [
  {
    path: '',
    component: GuardianShell,
    children: [
      { path: '', component: GuardianDashboard },
      { path: 'mealplan', component: GuardianMealplan },
      { path: 'admin', component: GuardianAdmin }
    ]
  }
];
