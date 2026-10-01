import { SEEDED_USERS, expect, test } from './support/auth-fixture';

// What this spec found in app.routes.ts, and why it tests what it tests:
//
// There is NO per-subtree role guard on '/guardian' or '/child' -- both are only gated by
// authGuard (must be logged in), which never checks role at all (see core/auth.guard.ts). The
// only role-based redirect anywhere in the app is roleRedirectGuard, attached solely to the bare
// root path ('' in app.routes.ts): it resolves the signed-in account's role
// (AccountService.resolveRole, derived from "does this account have any guardian links") and sends
// a guardian to '/guardian' or a child to '/child'. It never runs again once you're past that
// first redirect -- there's nothing stopping an already-authenticated guardian from typing
// '/child' straight into the address bar afterward.
//
// Confirmed empirically (not guessed): logging in as carol and navigating directly to '/child'
// renders ChildHome with no redirect at all. There is no authorization error, no redirect, nothing
// distinguishing it from a legitimate child session at the routing layer.
//
// So this spec covers exactly two things:
//  1. The guard that actually exists: a guardian landing on '/' is sent to '/guardian' (not
//     '/child'), proving the role resolution + redirect wiring works for the guardian side.
//  2. Documents, as a live regression check rather than a guess, that '/child' is NOT currently
//     blocked for a guardian session -- if a future change adds a real guard here, this assertion
//     will fail and should be updated (not treated as this spec being wrong).
//
// The reverse direction (an actual child session being redirected away from '/guardian') is not
// covered here. It would require a genuinely working child login, which this investigation found
// is not currently drivable through the real UI in this environment within reasonable time: a
// freshly created child's temporary password requires completing Keycloak's hosted UPDATE_PASSWORD
// required-action page (the same real-redirect approach login.spec.ts uses for a normal login), but
// testing showed that ANY account provisioned via Keycloak's Admin REST API -- including a
// permanent-password account with no required actions at all, ruling out the required-action step
// specifically -- fails Keycloak's own hosted login form with "Invalid username or password", even
// though the identical credentials succeed immediately via the direct-grant token endpoint at the
// same moment (proving the password itself is correct server-side). This reproduced across many
// freshly created accounts (temporary and permanent passwords, with and without email set, with a
// delay after creation to rule out cache propagation), while the realm-seeded accounts
// (alice/bob/carol) log in through the identical hosted form without issue every time. That's a
// genuine, reproducible Keycloak/environment quirk affecting any admin-API-provisioned account's
// ability to complete the hosted browser flow at all, independent of the required-action mechanics
// this spec cares about -- not something reasonably fixable from an e2e spec, so the child-side
// direction is skipped rather than faked with a stubbed/synthetic session.
test('a guardian landing on the root path is redirected to /guardian, not /child', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.carol);

  await page.goto('/');

  await expect(page).toHaveURL(/\/guardian$/);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
});

test('a guardian session is not currently blocked from directly reaching /child (no route guard covers this)', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.carol);

  await page.goto('/child');

  // No redirect happens -- the URL stays exactly where we navigated it.
  await expect(page).toHaveURL(/\/child$/);

  // ChildHome renders normally -- its "Today" heading is unconditional (home.html renders it
  // outside any @if), unlike the page's various empty-state messages, which depend on what data
  // happens to exist for carol's account by the time this runs (other specs sharing calendars,
  // pickups, etc. with carol as a guardian can make some of those sections non-empty). Either way,
  // there's no error or access-denied page -- just the normal child dashboard.
  await expect(page.getByRole('heading', { name: 'Today', level: 1 })).toBeVisible();
});
