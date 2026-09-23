import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { uniqueName } from './support/guardian-data';

// Exercises app-my-profile on /guardian/admin: changing the guardian's given name and time zone,
// verifying the change is both echoed immediately and still there after a reload (i.e. actually
// persisted server-side, not just local component state). Uses carol (not alice/bob) to avoid
// colliding with the other e2e specs that lean more heavily on alice/bob -- but carol is *also* a
// long-lived shared fixture other specs depend on (pickup-assignment and calendar-item-crud, among
// others, hardcode her seeded given name "Carol" in their own assertions), so this test restores
// her original given name and time zone at the end rather than leaving the mutation in place --
// confirmed the hard way: an earlier version of this spec left her renamed, which made
// pickup-assignment.spec.ts fail (it expected a guardian option literally labeled "Carol").
test('guardian updates their given name and time zone from My profile, and it persists', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.carol);

  await page.goto('/guardian/admin');

  const section = page.locator('app-my-profile');
  await expect(section.getByRole('heading', { name: 'Your profile' })).toBeVisible();

  const givenNameInput = section.getByLabel('Given name');
  const originalGivenName = await givenNameInput.inputValue();

  const givenName = uniqueName('E2eCarol');
  await givenNameInput.fill(givenName);

  const saveNameButton = section.getByRole('button', { name: 'Save name' });
  await expect(saveNameButton).toBeEnabled();
  await saveNameButton.click();

  await expect(section.getByText('Name updated.')).toBeVisible();

  const timeZoneSelect = section.locator('select[name="timeZoneId"]');

  // Carol's time zone is a shared, persistent value across repeated runs of this spec against the
  // same backend -- picking a fixed target would leave the save button disabled (no-op) on any
  // run following one that already set it. Toggle between two distinct zones based on whatever is
  // currently selected, so every run actually changes it.
  const originalTimeZone = await timeZoneSelect.inputValue();
  const targetTimeZone = originalTimeZone === 'Europe/Copenhagen' ? 'America/New_York' : 'Europe/Copenhagen';
  await timeZoneSelect.selectOption(targetTimeZone);

  const saveTimeZoneButton = section.getByRole('button', { name: 'Save time zone' });
  await expect(saveTimeZoneButton).toBeEnabled();
  await saveTimeZoneButton.click();

  await expect(section.getByText('Time zone updated. Timestamps across the app now use it.')).toBeVisible();

  await page.reload();

  await expect(section.getByLabel('Given name')).toHaveValue(givenName);
  await expect(timeZoneSelect).toHaveValue(targetTimeZone);

  // Restore carol to how this test found her, so the mutation above doesn't outlive this test run
  // and break other specs that depend on her seeded given name/time zone.
  await givenNameInput.fill(originalGivenName);
  await saveNameButton.click();
  await expect(section.getByText('Name updated.')).toBeVisible();

  await timeZoneSelect.selectOption(originalTimeZone);
  await saveTimeZoneButton.click();
  await expect(section.getByText('Time zone updated. Timestamps across the app now use it.')).toBeVisible();
});
