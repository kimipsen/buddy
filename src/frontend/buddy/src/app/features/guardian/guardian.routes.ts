import { Routes } from '@angular/router';

import { GuardianAdmin } from './admin/admin';
import { GuardianCalendar } from './calendar/calendar';
import { GuardianDashboard } from './dashboard';
import { GuardianMealplan } from './mealplan/mealplan';
import { GuardianMedicine } from './medicine/medicine';
import { GuardianPickup } from './pickup/pickup';
import { GuardianProgress } from './progress/progress';
import { GuardianShell } from './shell/guardian-shell';
import { GuardianTaskLibrary } from './task-library/task-library';

export const GUARDIAN_ROUTES: Routes = [
  {
    path: '',
    component: GuardianShell,
    children: [
      { path: '', component: GuardianDashboard },
      { path: 'mealplan', component: GuardianMealplan },
      { path: 'medicine', component: GuardianMedicine },
      { path: 'progress', component: GuardianProgress },
      { path: 'pickup', component: GuardianPickup },
      { path: 'calendar', component: GuardianCalendar },
      { path: 'task-library', component: GuardianTaskLibrary },
      { path: 'admin', component: GuardianAdmin }
    ]
  }
];
