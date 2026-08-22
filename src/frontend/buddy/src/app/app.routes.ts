import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { roleRedirectGuard } from './core/role.guard';
import { Login } from './features/login/login';

export const routes: Routes = [
  {
    path: 'login',
    component: Login
  },
  {
    path: 'guardian',
    canActivate: [authGuard],
    loadChildren: () => import('./features/guardian/guardian.routes').then((m) => m.GUARDIAN_ROUTES)
  },
  {
    path: 'child',
    canActivate: [authGuard],
    loadChildren: () => import('./features/child/child.routes').then((m) => m.CHILD_ROUTES)
  },
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard, roleRedirectGuard],
    children: []
  },
  {
    path: '**',
    redirectTo: ''
  }
];
