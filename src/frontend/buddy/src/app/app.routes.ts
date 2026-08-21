import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { Dashboard } from './features/dashboard/dashboard';
import { Login } from './features/login/login';

export const routes: Routes = [
  {
    path: 'login',
    component: Login
  },
  {
    path: 'dashboard',
    component: Dashboard,
    canActivate: [authGuard]
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard'
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
