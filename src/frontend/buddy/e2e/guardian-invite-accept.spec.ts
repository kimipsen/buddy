import { expect, test } from './support/auth-fixture';
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
// this spec exercises instead is inviting a second guardian who already has an account, by their
// real email, and accepting while logged in as them -- exactly what a returning co-parent with
// their own existing account would do, and the only path the backend actually allows.
//
// Both guardians are disposable (newGuardian), not alice/bob: in a parallel run
// email-verification.spec.ts used to change bob's email between this spec reading it and bob
// accepting, so the accept 403'd ("sent to a different account"); and linking a child to two seeded
// guardians merged their families for every other spec running as them at the same time.
test('guardian invites a co-guardian by email, who accepts the invite from their own account', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const { apiBaseUrl } = readRuntimeConfig();
  const inviter = await newGuardian();
  const invitee = await newGuardian();

  // GET /users/me JIT-provisions the invitee's backend User (GetOrCreateUserHandler), which
  // nothing on the standalone /guardian-invite/:token route (outside the authenticated shell) would
  // otherwise trigger -- without it, the accept below 403s ("sent to a different account") even
  // when the emails match, because there'd be no backend User yet to compare against. It also
  // gives the email the backend actually holds for them.
  const { accessToken: inviteeAccessToken } = await getAccessToken(
    invitee.username,
    invitee.password,
  );
  const inviteeMeResponse = await page.request.get(`${apiBaseUrl}/users/me`, {
    headers: { Authorization: `Bearer ${inviteeAccessToken}` },
  });
  expect(inviteeMeResponse.ok()).toBe(true);
  const inviteeMe = (await inviteeMeResponse.json()) as {
    email: { value: string; isVerified: boolean };
  };
  expect(inviteeMe.email.isVerified).toBe(true);
  const inviteEmail = inviteeMe.email.value;

  await loginAs(inviter);

  const child = await createChild(page);

  const section = page.locator('app-manage-children');
  const childRow = section.locator('li', { hasText: child.givenName });

  await childRow.getByRole('button', { name: 'Invite a co-guardian' }).click();

  await page.getByLabel('Email address').fill(inviteEmail);
  await page.getByRole('button', { name: 'Send invite' }).click();

  await expect(page.getByText(inviteEmail, { exact: true })).toBeVisible();

  // The invite email's body contains the child's (unique) given name -- see
  // waitForMessageTextContaining's own comment.
  const messageText = await waitForMessageTextContaining(inviteEmail, child.givenName);
  const token = extractInviteToken(messageText, 'guardian-invite');

  // Switch identity to the invited co-guardian and accept from their own account.
  await loginAs(invitee);
  await page.goto(`/guardian-invite/${token}`);

  await expect(page.getByRole('heading').filter({ hasText: child.givenName })).toBeVisible();

  await page.getByRole('button', { name: 'Accept invite' }).click();

  await expect(
    page.getByRole('heading', { name: new RegExp(`now a guardian for ${child.givenName}`) }),
  ).toBeVisible();

  await page.getByRole('button', { name: 'Go to my children' }).click();

  // ManageChildren has no "co-guardians" list on the child it already shows -- the way this app
  // surfaces a successful accept is that the child now shows up in *the other guardian's own*
  // children list too. Seeing it here, under the invitee's account (who a moment ago had no link to
  // this child at all), is the real evidence of the now-shared guardianship.
  await expect(
    page.locator('app-manage-children').locator('li', { hasText: child.givenName }),
  ).toBeVisible();
});
