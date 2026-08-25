import { Routes } from '@angular/router';

import { GuardianAdmin } from './admin/admin';
import { GuardianCalendar } from './calendar/calendar';
import { GuardianDashboard } from './dashboard';
import { GuardianMealplan } from './mealplan/mealplan';
import { GuardianMedicine } from './medicine/medicine';
import { GuardianPickup } from './pickup/pickup';
import { GuardianShell } from './shell/guardian-shell';

export const GUARDIAN_ROUTES: Routes = [
  {
    path: '',
    component: GuardianShell,
    children: [
      { path: '', component: GuardianDashboard },
      { path: 'mealplan', component: GuardianMealplan },
      { path: 'medicine', component: GuardianMedicine },
      { path: 'pickup', component: GuardianPickup },
      { path: 'calendar', component: GuardianCalendar },
      { path: 'admin', component: GuardianAdmin }
    ]
  }
];
