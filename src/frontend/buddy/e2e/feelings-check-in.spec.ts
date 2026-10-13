import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// Feelings check-ins end to end (docs/backend/analysis/feelings-check-in.md), against the real API,
// Keycloak and Postgres: the child checks in "Wound up" with "Worried" on the home page; the
// guardian sees it in today's row of /guardian/feelings with the day's detail, switches the child to
// faces only, and creates a share link; a reader with no session opens the shared page and sees
// the check-in; the child's buttons then show faces only.
//
// A disposable guardian because createChild is involved. The child signs in by direct grant once
// makeChildLoginUsable has replaced its temporary password (as in transition-warnings.spec.ts).
// The child's profile zone is whatever createChild set; the guardian page lists by the device's
// "today", which can differ from the child's around midnight, so the guardian side checks the day
// detail of the date the API saved rather than assuming today.
test('a child checks in, the guardian sees it and shares it with a doctor', async ({
  page,
  browser,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);

  // The child checks in.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const card = page.getByTestId('feelings-check-in');
  await expect(card.getByRole('heading', { name: 'How are you feeling?' })).toBeVisible();
  await expect(card.getByText('Your grown-ups can see your check-ins.')).toBeVisible();

  await card
    .getByRole('button', { name: 'Wound up – worried, frustrated, silly or restless' })
    .click();
  await card.getByRole('button', { name: 'Worried', exact: true }).click();
  const [saved] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'POST' && res.url().endsWith('/feelings/me/check-ins'),
    ),
    card.getByRole('button', { name: 'Done' }).click(),
  ]);
  expect(saved.ok()).toBe(true);
  const raw = (await saved.json()) as { date: string; time: string };
  // The API answers "HH:mm:ss"; the pages show "HH:mm".
  const checkIn = { date: raw.date, time: raw.time.slice(0, 5) };
  await expect(card.getByText(`Thanks, ${child.givenName}!`)).toBeVisible();
  await expect(card.getByRole('button', { name: 'Undo' })).toBeVisible();

  // It persists: after a reload it's among today's chips.
  await page.reload();
  await expect(page.getByTestId('feelings-today-chips')).toContainText(checkIn.time);

  // The guardian sees it in the trend and the day's detail.
  await loginAs(guardian);
  await page.goto('/guardian/feelings');
  await expect(
    page.getByRole('heading', { name: 'How your child has been feeling.' }),
  ).toBeVisible();
  const row = page.locator(`tr[data-date="${checkIn.date}"]`);
  await expect(row).toContainText(`${checkIn.time}, Wound up (yellow): Worried, by the child`);
  await row.getByRole('button').click();
  const detail = page.getByTestId('feelings-day-detail');
  await expect(detail).toContainText('Wound up');
  await expect(detail).toContainText('Your child');
  // A self-report can be removed but not edited.
  await expect(detail.getByRole('button', { name: 'Edit' })).toHaveCount(0);

  // Faces only for the child.
  const facesOnly = page.getByRole('switch', { name: 'Faces only' });
  await expect(facesOnly).toHaveAttribute('aria-checked', 'false');
  await facesOnly.click();
  await expect(facesOnly).toHaveAttribute('aria-checked', 'true');

  // A share link for a doctor.
  const share = page.locator('app-feelings-share-links');
  await share.getByRole('button', { name: 'Create link' }).click();
  const link = share.locator('.font-mono');
  await expect(link).toContainText('/shared/feelings/');
  const sharedUrl = (await link.textContent())!.trim();

  // A reader with no session.
  const anonymous = await browser.newContext();
  const doctor = await anonymous.newPage();
  await doctor.goto(sharedUrl);
  await expect(
    doctor.getByRole('heading', { name: `Feelings check-ins for ${child.givenName}` }),
  ).toBeVisible();
  await expect(doctor.getByTestId('shared-feelings-table')).toContainText('Worried');
  await anonymous.close();

  // The child's buttons now show faces only.
  await loginAs(childLogin);
  await expect(page.getByRole('button', { name: 'Smiling face' })).toBeVisible();
  await expect(
    page.getByTestId('feelings-check-in').getByText('Ready', { exact: true }),
  ).toHaveCount(0);
});
