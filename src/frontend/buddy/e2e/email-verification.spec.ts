import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { uniqueName } from './support/guardian-data';
import { getMessageText, getMessagesTo } from './support/mailpit-client';

// Changing a guardian's email address is the real minimal path to a fresh verification email --
// UpdateEmailHandler on the backend only sends one when the new value actually differs from the
// current one (a no-op "change" to the same address is silently skipped, so alice/bob/carol's
// original signup-time verification state is never touched by this spec). Uses bob, matching
// guardian-dashboard.spec.ts/medicine-dose-status.spec.ts's existing choice, since profile-update
// already leans on carol.
test('guardian verifies a newly-changed email address via the emailed link', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.bob);

  const newEmail = `${uniqueName('e2everify').toLowerCase()}@buddy.test`;

  await page.goto('/guardian/admin');

  const profileSection = page.locator('app-my-profile');
  await expect(profileSection.getByRole('heading', { name: 'Your profile' })).toBeVisible();

  const emailInput = profileSection.getByLabel('Email');
  await emailInput.fill(newEmail);

  const saveEmailButton = profileSection.getByRole('button', { name: 'Save email' });
  await expect(saveEmailButton).toBeEnabled();
  await saveEmailButton.click();

  await expect(profileSection.getByText('Email updated. Check your inbox to verify it.')).toBeVisible();

  // The backend awaits the SMTP send before its PATCH response returns (SmtpEmailSender via
  // Mailpit), but Mailpit's own search index can lag slightly behind message delivery -- retry
  // briefly instead of reading it once.
  let token = '';
  await expect(async () => {
    const messages = await getMessagesTo(newEmail);
    expect(messages.length).toBeGreaterThan(0);

    const text = await getMessageText(messages[0]!.ID);
    const match = /verify-email\/([A-Za-z0-9_-]+)/.exec(text);
    expect(match).not.toBeNull();

    token = match![1]!;
  }).toPass({ timeout: 15_000 });

  await page.goto(`/verify-email/${token}`);
  await expect(page.getByRole('heading', { name: 'Verify your email address' })).toBeVisible();

  await page.getByRole('button', { name: 'Verify email' }).click();

  await expect(page.getByRole('heading', { name: 'Your email address is verified.' })).toBeVisible();

  // Confirm it actually persisted server-side, not just the verify-email page's own local state.
  await page.goto('/guardian/admin');
  await expect(profileSection.getByLabel('Email')).toHaveValue(newEmail);
  await expect(profileSection.getByText('Verified')).toBeVisible();
});
