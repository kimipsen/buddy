import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createGroup } from './support/guardian-data';
import { getAccessToken } from './support/keycloak-client';
import { extractInviteToken, waitForMessageTextContaining } from './support/mailpit-client';
import { readRuntimeConfig } from './support/runtime-config';

// Mirrors guardian-invite-accept.spec.ts's own reasoning: accepting a group invite
// (AcceptInviteHandler) requires the authenticated caller's verified email to match the invite's,
// so the real minimal path is inviting an already-seeded guardian by their real email and
// accepting from their own account -- there's no self-service "create an account from this
// invite" step for a truly new address.
test('guardian invites a member to a group by email, who accepts, then the owner updates the group\'s calendar sharing policy', async ({ page, loginAs }) => {
  const { apiBaseUrl } = readRuntimeConfig();

  // Look up carol's *actual current* backend email rather than assuming "carol@buddy.test": a
  // seeded account like this is shared and reused by other, unrelated e2e specs, any of which
  // could in principle mutate its backend User's email away from the Keycloak-seeded default on
  // this persistent dev database (confirmed happening to bob's account, via a change-email spec --
  // see guardian-invite-accept.spec.ts's identical comment). This call also JIT-provisions
  // carol's backend User if one doesn't exist yet (GetOrCreateUserHandler), which nothing on the
  // standalone /invite/:token route would otherwise trigger -- without it, the accept below 403s
  // even when the emails do match, because there'd be no backend User yet to compare against.
  const { accessToken: carolAccessToken } = await getAccessToken(SEEDED_USERS.carol.username, SEEDED_USERS.carol.password);
  const carolMeResponse = await page.request.get(`${apiBaseUrl}/users/me`, {
    headers: { Authorization: `Bearer ${carolAccessToken}` },
  });
  expect(carolMeResponse.ok()).toBe(true);
  const carolMe = (await carolMeResponse.json()) as { email: { value: string; isVerified: boolean } };
  expect(carolMe.email.isVerified).toBe(true);
  const inviteEmail = carolMe.email.value;

  await loginAs(SEEDED_USERS.alice);

  const groupName = await createGroup(page);

  const groupsSection = page.locator('app-manage-groups');
  const groupRow = groupsSection.locator('li', { hasText: groupName });

  await groupRow.getByRole('button', { name: 'Invite', exact: true }).click();

  await page.getByLabel('Email address').fill(inviteEmail);
  // Role defaults to Member already (ManageGroups.toggleInvitePanel resets it to 2) -- exactly
  // the role this spec's policy toggle targets below.
  await page.getByRole('button', { name: 'Send invite' }).click();

  await expect(page.getByText(inviteEmail, { exact: true })).toBeVisible();

  const messageText = await waitForMessageTextContaining(inviteEmail, groupName);
  const token = extractInviteToken(messageText, 'invite');

  await loginAs(SEEDED_USERS.carol);
  await page.goto(`/invite/${token}`);

  await expect(page.getByRole('heading').filter({ hasText: groupName })).toBeVisible();

  await page.getByRole('button', { name: 'Accept invite' }).click();

  // Distinct from the preview heading above ("...invited to join {groupName}."), so this also
  // confirms the accept actually succeeded rather than the preview text just still being there.
  await expect(page.getByRole('heading', { name: new RegExp(`joined ${groupName}`) })).toBeVisible();

  await page.getByRole('button', { name: 'Go to my groups' }).click();

  const carolGroupRow = page.locator('app-manage-groups').locator('li', { hasText: groupName });
  await expect(carolGroupRow).toBeVisible();
  await expect(carolGroupRow.getByText('Member', { exact: true })).toBeVisible();

  // Switch back to alice (the group's owner) to change the calendar-sharing policy for the
  // Member role she just added carol to.
  await loginAs(SEEDED_USERS.alice);
  await page.goto('/guardian/admin');

  const aliceGroupRow = page.locator('app-manage-groups').locator('li', { hasText: groupName });
  await aliceGroupRow.getByRole('button', { name: 'Calendar permissions' }).click();

  const memberPolicySelect = aliceGroupRow.locator('label', { hasText: 'Member' }).locator('select');
  const currentLabel = (await memberPolicySelect.locator('option:checked').textContent())?.trim();
  const targetLabel = currentLabel === 'Contributor' ? 'Viewer' : 'Contributor';

  await memberPolicySelect.selectOption({ label: targetLabel });

  const [saveResponse] = await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && res.url().includes('/calendar-permission-policy')),
    aliceGroupRow.getByRole('button', { name: 'Save permissions' }).click(),
  ]);
  expect(saveResponse.ok()).toBe(true);

  await page.reload();

  await aliceGroupRow.getByRole('button', { name: 'Calendar permissions' }).click();
  await expect(
    aliceGroupRow.locator('label', { hasText: 'Member' }).locator('select').locator('option:checked'),
  ).toHaveText(targetLabel);
});
