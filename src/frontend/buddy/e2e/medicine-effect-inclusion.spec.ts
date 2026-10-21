import type { Page } from '@playwright/test';

import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';

// The browser's local "YYYY-MM-DD" (same helper as medicine-effect-notes.spec.ts).
function localIsoDate(daysFromToday: number): string {
  const date = new Date();
  date.setDate(date.getDate() + daysFromToday);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

async function addSchedule(page: Page, name: string, dosage: string, startDate: string) {
  await page.getByLabel('Medicine name').fill(name);
  await page.getByLabel('Dosage (e.g. 5ml)').fill(dosage);
  await page.getByLabel('Start date').fill(startDate);
  await page.getByRole('button', { name: 'Add schedule' }).click();
  await expect(page.locator('li', { hasText: name })).toBeVisible();
}

async function rateFocusGood(page: Page, date: string, regimenText: string) {
  const form = page.locator('app-effect-note-form');
  await form.getByLabel('Day').fill(date);
  await expect(form.getByText(regimenText)).toBeVisible();
  await form
    .getByRole('radiogroup', { name: 'Focus', exact: true })
    .getByRole('radio', { name: 'Good', exact: true })
    .click();
  await form.getByRole('button', { name: 'Save day' }).click();
  await expect(form.getByRole('status')).toHaveText('Saved.');
}

// docs/backend/analysis/medicine-effect-inclusion.md, end to end: starting vitamin D splits the
// Ritalin days into two dose periods, and switching vitamin D out of the summary merges them again.
// The vitamin starts today, so yesterday's regimen (resolved by the API on first rating) is Ritalin
// alone and today's is Ritalin + vitamin D.
test('guardian leaves a vitamin out of the effect summary and the dose periods merge', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());
  await createChild(page);

  const ritalin = uniqueName('E2E Ritalin');
  const vitamin = uniqueName('E2E Vitamin D');

  await page.goto('/guardian/medicine');
  await expect(
    page.getByRole('heading', { name: 'Medicine schedules', exact: true }),
  ).toBeVisible();
  await addSchedule(page, ritalin, '10 mg', localIsoDate(-1));
  await addSchedule(page, vitamin, '1 tablet', localIsoDate(0));

  await page.getByRole('link', { name: 'How did it go? →' }).click();
  await expect(page.getByRole('heading', { name: 'How is the medicine working?' })).toBeVisible();
  await rateFocusGood(page, localIsoDate(-1), `${ritalin} 10 mg`);
  await rateFocusGood(page, localIsoDate(0), `${vitamin} 1 tablet`);

  const periods = page.locator('app-effect-periods section');
  await expect(periods).toHaveCount(2);

  const vitaminSwitch = page.getByRole('switch', { name: `Include ${vitamin} in the summary` });
  await expect(vitaminSwitch).toHaveAttribute('aria-checked', 'true');
  await vitaminSwitch.click();

  await expect(periods).toHaveCount(1);
  await expect(periods.getByRole('heading')).toHaveText(`${ritalin} 10 mg`);
  await expect(periods).toContainText('Good 2');
  await expect(page.locator('app-effect-periods')).toContainText(`Not included: ${vitamin}.`);

  await page.reload();

  await expect(periods).toHaveCount(1);
  await expect(vitaminSwitch).toHaveAttribute('aria-checked', 'false');
});
