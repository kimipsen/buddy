import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { roleRedirectGuard } from './core/role.guard';
import { AcceptGuardianInvite } from './features/invite/accept-guardian-invite';
import { AcceptInvite } from './features/invite/accept-invite';
import { Login } from './features/login/login';
import { SharedSleepDiary } from './features/shared-sleep-diary/shared-sleep-diary';
import { VerifyEmail } from './features/verify-email/verify-email';

export const routes: Routes = [
  {
    path: 'login',
    component: Login,
  },
  {
    // Not behind authGuard -- reachable while logged out so the invite preview and "log in to
    // accept" prompt both work; see pending-invite-token.ts for how login then returns here.
    path: 'invite/:token',
    component: AcceptInvite,
  },
  {
    // Not behind authGuard, same reasoning as the group-invite route above; see
    // pending-guardian-invite-token.ts for how login then returns here.
    path: 'guardian-invite/:token',
    component: AcceptGuardianInvite,
  },
  {
    // Not behind authGuard -- reachable while logged out so the "log in to verify" prompt works;
    // see pending-verify-email-token.ts for how login then returns here.
    path: 'verify-email/:token',
    component: VerifyEmail,
  },
  {
    // Not behind authGuard -- a clinician opening a share link has no Buddy account; the token in
    // the URL is the only credential (see GetSharedSleepDiary on the backend).
    path: 'shared/sleep-diary/:token',
    component: SharedSleepDiary,
  },
  {
    path: 'guardian',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./features/guardian/guardian.routes').then((m) => m.GUARDIAN_ROUTES),
  },
  {
    path: 'child',
    canActivate: [authGuard],
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
