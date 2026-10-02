import { expect, test } from './support/auth-fixture';

// Exercises week plan printing end to end against the real API: create a template, fill it from
// the example, choose the paper, preview the sheet, and print it to PDF. The PDF must be exactly
// one landscape page of the chosen size -- the layout sizes rows in fr units precisely so the sheet
// always fills one page.
//
// A disposable guardian: templates are per-guardian state that would otherwise pile up across runs.
// With no children, calendars or work locations, the example is just its "Notes" row -- what each
// row kind prints is covered by the assembleWeekPlan and WeekPlanSheet unit specs, and the
// work-location flow by work-locations-pattern.spec.ts. Keeping this journey short keeps it well
// inside the test timeout when several workers hit a freshly started dev server at once.

// Landscape page size in PDF points (1/72 in), with a little tolerance for rounding.
const PAPER_POINTS = {
  A4: { width: 842, height: 595 },
  A3: { width: 1191, height: 842 },
} as const;

function pdfPages(pdf: Buffer): { width: number; height: number }[] {
  const text = pdf.toString('latin1');
  const pageCount = (text.match(/\/Type\s*\/Page(?!s)/g) ?? []).length;
  const box = /\/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]/.exec(text);
  const size = box ? { width: Number(box[1]), height: Number(box[2]) } : { width: 0, height: 0 };
  return Array.from({ length: pageCount }, () => size);
}

for (const paper of ['A4', 'A3'] as const) {
  test(`guardian prints a template as exactly one landscape ${paper} page`, async ({
    page,
    loginAs,
    newGuardian,
  }, testInfo) => {
    await loginAs(await newGuardian());

    await page.goto('/guardian/print');
    await expect(page.getByRole('heading', { name: 'Print a week plan.' })).toBeVisible();
    await page.getByLabel('Name').fill(`E2E ${paper} week`);
    await page.getByRole('button', { name: 'Create template' }).click();

    await expect(page.getByRole('heading', { name: 'Edit print template' })).toBeVisible();
    await page.getByRole('button', { name: 'Start from example' }).click();
    await expect(page.locator('input[id^="row-label-"]').first()).toHaveValue('Notes');
    if (paper === 'A3') {
      await page.getByRole('radio', { name: 'A3' }).click();
    }
    await page.getByRole('button', { name: 'Save', exact: true }).click();
    await expect(page.getByText('Template saved.')).toBeVisible();

    await page.goto('/guardian/print');
    await expect(page.getByLabel('Template')).toHaveValue(/.+/);
    await page.getByRole('button', { name: 'Preview' }).click();

    const sheet = page.getByRole('table');
    await expect(sheet).toBeVisible();
    await expect(sheet.getByRole('rowheader', { name: 'Notes' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Print' })).toBeFocused();

    await testInfo.attach(`week-plan-${paper}.png`, {
      body: await sheet.screenshot(),
      contentType: 'image/png',
    });

    const pages = pdfPages(await page.pdf({ preferCSSPageSize: true }));
    expect(pages).toHaveLength(1);
    expect(pages[0].width).toBeCloseTo(PAPER_POINTS[paper].width, -1);
    expect(pages[0].height).toBeCloseTo(PAPER_POINTS[paper].height, -1);
  });
}
