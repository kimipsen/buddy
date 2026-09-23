import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { getAccessToken } from './support/keycloak-client';
import { extractInviteToken, waitForMessageTextContaining } from './support/mailpit-client';
import { readRuntimeConfig } from './support/runtime-config';

// Accepting a guardian invite (AcceptGuardianInviteHandler) requires the *currently authenticated*
// caller's own verified email to match the invite's -- there's no separate "create an account from
// this invite" step; it only links whichever already-authenticated user matches (see
// AcceptGuardianInvite.Handler.cs). So a truly brand-new guardian (an email with no Keycloak
// account at all) has no real "accept" path through this UI at all -- they'd bounce at
// AuthService.login()'s hosted Keycloak login form with nowhere to register. The real minimal path
// this spec exercises instead is inviting a second *seeded* guardian by their real, current email
// and accepting while logged in as them -- exactly what a returning co-parent with their own
// existing account would do, and the only path the backend actually allows.
test('guardian invites a co-guardian by email, who accepts the invite from their own account', async ({ page, loginAs }) => {
  const { apiBaseUrl } = readRuntimeConfig();

  // Look up bob's *actual current* backend email rather than assuming "bob@buddy.test": bob's
  // seeded account is shared and reused by other, unrelated e2e specs (e.g. an
  // email-verification/change-email spec), which can permanently mutate his backend User's email
  // away from the Keycloak-seeded default on this persistent dev database -- confirmed by
  // inspecting the event store directly. This same GET /users/me call also JIT-provisions his
  // backend User if one doesn't exist yet (GetOrCreateUserHandler), which nothing on the
  // standalone /guardian-invite/:token route (outside the authenticated shell) would otherwise
  // trigger -- without it, the accept below 403s ("sent to a different account") even when the
  // emails do match, because there'd be no backend User yet to compare against.
  const { accessToken: bobAccessToken } = await getAccessToken(SEEDED_USERS.bob.username, SEEDED_USERS.bob.password);
  const bobMeResponse = await page.request.get(`${apiBaseUrl}/users/me`, {
    headers: { Authorization: `Bearer ${bobAccessToken}` },
  });
  expect(bobMeResponse.ok()).toBe(true);
  const bobMe = (await bobMeResponse.json()) as { email: { value: string; isVerified: boolean } };
  expect(bobMe.email.isVerified).toBe(true);
  const inviteEmail = bobMe.email.value;

  await loginAs(SEEDED_USERS.alice);

  const child = await createChild(page);

  const section = page.locator('app-manage-children');
  const childRow = section.locator('li', { hasText: child.givenName });

  await childRow.getByRole('button', { name: 'Invite a co-guardian' }).click();

  await page.getByLabel('Email address').fill(inviteEmail);
  await page.getByRole('button', { name: 'Send invite' }).click();

  await expect(page.getByText(inviteEmail, { exact: true })).toBeVisible();

  // The invite email's body contains the child's (unique) given name, which disambiguates it from
  // any other test running in parallel that also happens to invite this same address around the
  // same time -- see waitForMessageTextContaining's own comment.
  const messageText = await waitForMessageTextContaining(inviteEmail, child.givenName);
  const token = extractInviteToken(messageText, 'guardian-invite');

  // Switch identity to bob -- the invited co-guardian -- and accept from his own account.
  await loginAs(SEEDED_USERS.bob);
  await page.goto(`/guardian-invite/${token}`);

  await expect(page.getByRole('heading').filter({ hasText: child.givenName })).toBeVisible();

  await page.getByRole('button', { name: 'Accept invite' }).click();

  await expect(page.getByRole('heading', { name: new RegExp(`now a guardian for ${child.givenName}`) })).toBeVisible();

  await page.getByRole('button', { name: 'Go to my children' }).click();

  // ManageChildren has no "co-guardians" list on the child it already shows -- the way this app
  // surfaces a successful accept is that the child now shows up in *the other guardian's own*
  // children list too. Seeing it here, under bob's account (who a moment ago had no link to this
  // child at all), is the real evidence of the now-shared guardianship.
  await expect(page.locator('app-manage-children').locator('li', { hasText: child.givenName })).toBeVisible();
});
