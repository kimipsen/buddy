import type { Page } from '@playwright/test';

import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';

// The browser's local "YYYY-MM-DD", which is what the form's date input and the API's
// "not after the child's today" rule both work in (the disposable child gets the browser's zone).
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
  await expect(page.locator('li', { hasText: `(${dosage})` })).toBeVisible();
}

async function rate(page: Page, area: string, option: string) {
  await page
    .locator('app-effect-note-form')
    .getByRole('radiogroup', { name: area, exact: true })
    .getByRole('radio', { name: option, exact: true })
    .click();
}

// The real path end to end: the regimen is resolved by the API from the child's active schedules
// when a day is first rated, so the two days land in two dose periods. The UI has no "edit dosage"
// action, so the dosage change is a stop of the 10 mg schedule and a new 15 mg one, which is how a
// guardian does it today.
test('guardian rates two days across a dosage change and sees two dose periods', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());
  await createChild(page);

  const medicine = uniqueName('E2E Ritalin');
  const yesterday = localIsoDate(-1);

  await page.goto('/guardian/medicine');
  await expect(
    page.getByRole('heading', { name: 'Medicine schedules', exact: true }),
  ).toBeVisible();
  await addSchedule(page, medicine, '10 mg', yesterday);

  await page.getByRole('link', { name: 'How did it go? →' }).click();
  await expect(page.getByRole('heading', { name: 'How is the medicine working?' })).toBeVisible();

  const form = page.locator('app-effect-note-form');
  await form.getByLabel('Day').fill(yesterday);
  await expect(form.getByText(`${medicine} 10 mg`)).toBeVisible();
  await rate(page, 'Focus', 'Good');
  await rate(page, 'Less appetite', 'Moderate');
  await form.getByRole('button', { name: 'Save day' }).click();
  await expect(form.getByRole('status')).toHaveText('Saved.');

  await page.getByRole('link', { name: 'Back to medicine' }).click();
  const oldSchedule = page.locator('li', { hasText: '(10 mg)' });
  await oldSchedule.getByRole('button', { name: 'Stop' }).click();
  await oldSchedule.getByRole('button', { name: 'Confirm' }).click();
  await expect(oldSchedule).toHaveCount(0);
  await addSchedule(page, medicine, '15 mg', localIsoDate(0));

  await page.getByRole('link', { name: 'How did it go? →' }).click();
  await expect(form.getByText(`${medicine} 15 mg`)).toBeVisible();
  await rate(page, 'Focus', 'Okay');
  await rate(page, 'Less appetite', 'Mild');
  await form.getByRole('button', { name: 'Save day' }).click();
  await expect(form.getByRole('status')).toHaveText('Saved.');

  await page.reload();

  const periods = page.locator('app-effect-periods section');
  await expect(periods).toHaveCount(2);
  // Newest first: today's 15 mg, then yesterday's 10 mg, each with its own counts.
  await expect(periods.nth(0).getByRole('heading')).toHaveText(`${medicine} 15 mg`);
  await expect(periods.nth(0)).toContainText('Okay 1');
  await expect(periods.nth(0)).toContainText('Mild 1');
  await expect(periods.nth(1).getByRole('heading')).toHaveText(`${medicine} 10 mg`);
  await expect(periods.nth(1)).toContainText('Good 1');
  await expect(periods.nth(1)).toContainText('Moderate 1');
});
