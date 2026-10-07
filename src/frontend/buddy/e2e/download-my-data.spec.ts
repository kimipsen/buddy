import { readFile } from 'node:fs/promises';

import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

test('a guardian downloads their data, including the children they guard', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian: the export is limited to one per 10 minutes per user.
  await loginAs(await newGuardian());

  const child = await createChild(page, 'E2eExported');

  const download = page.waitForEvent('download');
  await page
    .locator('app-download-my-data')
    .getByRole('button', { name: 'Download my data' })
    .click();
  const file = await download;

  expect(file.suggestedFilename()).toMatch(/^buddy-export-\d{4}-\d{2}-\d{2}\.json$/);

  const exported = JSON.parse(await readFile(await file.path(), 'utf8')) as {
    sections: { children: { children: { name: { givenName: string } }[] } };
  };
  expect(exported.sections.children.children.map((c) => c.name.givenName)).toContain(
    child.givenName,
  );
});
