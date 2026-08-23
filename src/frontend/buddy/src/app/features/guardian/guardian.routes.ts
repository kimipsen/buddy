import { Routes } from '@angular/router';

import { GuardianAdmin } from './admin/admin';
import { GuardianDashboard } from './dashboard';
import { GuardianShell } from './shell/guardian-shell';

export const GUARDIAN_ROUTES: Routes = [
  {
    path: '',
    component: GuardianShell,
    children: [
      { path: '', component: GuardianDashboard },
      { path: 'admin', component: GuardianAdmin }
    ]
  }
];
