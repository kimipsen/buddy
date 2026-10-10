import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { featureGuard } from './core/feature.guard';
import { roleRedirectGuard } from './core/role.guard';
import { Login } from './features/login/login';

// The login page is the usual first page, so it ships in the initial bundle. The other public
// pages are opened from a link in an email or a share link, rarely, so each is its own lazy chunk.
export const routes: Routes = [
  {
    path: 'login',
    component: Login,
  },
  {
    // Not behind authGuard -- reachable while logged out so the invite preview and "log in to
    // accept" prompt both work; see pending-invite-token.ts for how login then returns here.
    path: 'invite/:token',
    loadComponent: () => import('./features/invite/accept-invite').then((m) => m.AcceptInvite),
  },
  {
    // Not behind authGuard, same reasoning as the group-invite route above; see
    // pending-guardian-invite-token.ts for how login then returns here.
    path: 'guardian-invite/:token',
    loadComponent: () =>
      import('./features/invite/accept-guardian-invite').then((m) => m.AcceptGuardianInvite),
  },
  {
    // Not behind authGuard -- reachable while logged out so the "log in to verify" prompt works;
    // see pending-verify-email-token.ts for how login then returns here.
    path: 'verify-email/:token',
    loadComponent: () => import('./features/verify-email/verify-email').then((m) => m.VerifyEmail),
  },
  {
    // Not behind authGuard -- a clinician opening a share link has no Buddy account; the token in
    // the URL is the only credential (see GetSharedSleepDiary on the backend).
    path: 'shared/sleep-diary/:token',
    loadComponent: () =>
      import('./features/shared-sleep-diary/shared-sleep-diary').then((m) => m.SharedSleepDiary),
    canActivate: [featureGuard('sleepDiary')],
  },
  {
    path: 'guardian',
    // canActivateChild too, so the session is checked on every navigation inside the tree, not
    // only when it's first entered (see auth.guard.ts).
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    loadChildren: () =>
      import('./features/guardian/guardian.routes').then((m) => m.GUARDIAN_ROUTES),
  },
  {
    path: 'child',
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    loadChildren: () => import('./features/child/child.routes').then((m) => m.CHILD_ROUTES),
  },
  {
    path: '',
    pathMatch: 'full',
    canActivate: [roleRedirectGuard],
    children: [],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
