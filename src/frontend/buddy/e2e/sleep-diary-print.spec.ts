import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// The A4 printout of the sleep diary for a period the guardian chooses (GitHub issue #97), on the
// real API: the page reads the diary through ListSleepDiaryEntries and the night duty windows
// through ListNights. A disposable guardian whose only child is this test's own; tonight is planned
// in night duty (with a note) and logged, so the printout has something to show. Print layout is
// checked with emulated print media and page.pdf(), the only way to see paper size and page count.

// The browser's local "YYYY-MM-DD": the form's "today" and the page's default period both use it.
function localIsoDate(daysFromToday: number): string {
  const date = new Date();
  date.setDate(date.getDate() + daysFromToday);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

test('a guardian prints the sleep diary for a chosen period, up to 92 nights', async ({
  page,
  loginAs,
  newGuardian,
}, testInfo) => {
  await loginAs(await newGuardian());
  const child = await createChild(page);

  // The night-duty week starts tonight, so its first tile is tonight.
  await page.goto('/guardian/night-duty');
  const tonightTile = page.locator('app-night-week [data-night]').first();
  await tonightTile.getByRole('button', { name: /^Change/ }).click();
  const dialog = page.getByRole('dialog');
  await dialog
    .getByLabel('Who is on duty')
    .selectOption({ label: `👤 ${DISPOSABLE_GUARDIAN_GIVEN_NAME}` });
  await dialog.getByLabel('Note (optional)').fill('Screen night');
  await dialog.getByRole('button', { name: 'Save night' }).click();
  await expect(dialog).toBeHidden();
  await expect(tonightTile).toContainText('Screen night');

  // Log tonight, then open the printout from the history's "Print" link. The shell's "Print week
  // plan" link also matches "Print", so the link is scoped to the history.
  await page.goto('/guardian/sleep-diary');
  const form = page.locator('app-sleep-entry-form');
  await form.getByLabel('Lies down to sleep').fill('20:15');
  await form.getByRole('button', { name: 'Save night' }).click();
  await expect(form.getByRole('status')).toHaveText('Saved.');
  await page.locator('app-sleep-history').getByRole('link', { name: 'Print', exact: true }).click();

  const today = localIsoDate(0);
  await expect(page).toHaveURL(
    new RegExp(`/guardian/sleep-diary/print/[^/?]+\\?from=${localIsoDate(-13)}&to=${today}$`),
  );
  await expect(
    page.getByRole('heading', { name: `Sleep diary for ${child.givenName} Testson` }),
  ).toBeVisible();
  const rows = page.locator('app-sleep-diary-sheet tbody tr');
  await expect(rows).toHaveCount(14);
  await expect(rows.filter({ hasText: '8:15' })).toHaveCount(1);
  const printButton = page.getByRole('button', { name: 'Print', exact: true });
  await expect(printButton).toBeVisible();

  // 92 nights is the limit (both ends included); tonight is logged, so its night duty window shows.
  const from = page.getByLabel('From', { exact: true });
  await from.fill(localIsoDate(-91));
  await expect(rows).toHaveCount(92);
  await expect(page.getByRole('columnheader', { name: 'On duty' })).toBeVisible();
  const cell = rows.filter({ hasText: '8:15' }).getByTestId('night-duty-cell');
  await expect(cell).toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);
  await expect(cell).toContainText('Screen night');

  // One night more is refused: a message, no table, no Print button.
  await from.fill(localIsoDate(-92));
  await expect(page.getByRole('alert').filter({ hasText: 'at most 92 nights' })).toBeVisible();
  await expect(rows).toHaveCount(0);
  await expect(printButton).toBeHidden();

  // 60 nights, as paper: no toolbar, no inner scrolling, rows kept whole, A4 landscape, 2+ pages.
  await from.fill(localIsoDate(-59));
  await expect(rows).toHaveCount(60);
  await page.emulateMedia({ media: 'print' });
  await expect(page.getByRole('link', { name: /Back to sleep diary/ })).toBeHidden();
  await expect(printButton).toBeHidden();
  const wrapper = page.locator('app-sleep-diary-sheet > div').first();
  await expect(wrapper).toHaveCSS('overflow-x', 'visible');
  await expect(wrapper).toHaveCSS('overflow-y', 'visible');
  await expect(rows.first()).toHaveCSS('break-inside', 'avoid');

  const pdf = await page.pdf({ preferCSSPageSize: true });
  await testInfo.attach('sleep-diary-60-nights.pdf', { body: pdf, contentType: 'application/pdf' });
  const text = pdf.toString('latin1');
  // A4 landscape is 842 x 595 pt (Chromium writes 841.92 x 594.96, so compare as numbers, as
  // print-week-plan.spec.ts does); the page tree's /Count is the number of pages.
  const box = /\/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]/.exec(text);
  expect(Number(box?.[1])).toBeCloseTo(842, -1);
  expect(Number(box?.[2])).toBeCloseTo(595, -1);
  const pageCount = Number(/\/Type\s*\/Pages[^>]*\/Count\s+(\d+)/.exec(text)?.[1] ?? 0);
  expect(pageCount).toBeGreaterThan(1);
});
