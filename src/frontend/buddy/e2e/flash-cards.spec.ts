import type { Page } from '@playwright/test';

import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// Flash cards end to end (docs/backend/analysis/flash-cards.md), against the real API, Keycloak and
// Postgres (flash cards are on by default): a guardian makes
// a text set and a maths set and switches the maths one on for the child with two game lengths; the
// child plays through Homework, answers one card "Not yet", sees it once more at the end and gets
// stars; the guardian sees the finished game; playing the same length again earns nothing, and a
// longer one earns the difference.
//
// A disposable guardian because createChild is involved. Set names are unique per run. The stars
// are paid once per set and day, so the steps that depend on "today" stay clear of midnight in the
// two zones a child can have here (the profile zone createChild leaves is UTC or the device's).
const MIDNIGHT_MARGIN_MINUTES = 5;

function minutesOfDay(date: Date, timeZone?: string): number {
  const parts = new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
    timeZone,
  }).formatToParts(date);
  const value = (type: string): number => Number(parts.find((p) => p.type === type)?.value);

  return value('hour') * 60 + value('minute');
}

function nearMidnight(): boolean {
  const now = new Date();

  return [undefined, 'UTC'].some((zone) => {
    const minutes = minutesOfDay(now, zone);

    return minutes < MIDNIGHT_MARGIN_MINUTES || minutes > 24 * 60 - MIDNIGHT_MARGIN_MINUTES;
  });
}

// Plays the card on screen: flip it, then answer.
async function answerCard(page: Page, knewIt: boolean): Promise<void> {
  await page.getByRole('button', { name: 'Show the answer' }).click();
  await page.getByRole('button', { name: knewIt ? 'I knew it' : 'Not yet' }).click();
}

test('a guardian sets up flash cards, the child plays, and stars are paid once per set and day', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);

  const textSet = uniqueName('Capitals');
  const mathSet = uniqueName('Add to 10 ');

  // The guardian makes a text set with two cards.
  await page.goto('/guardian/flash-cards');
  await expect(page.getByRole('heading', { name: 'Flash cards', level: 2 })).toBeVisible();
  await page.getByRole('button', { name: 'New set' }).click();
  await page.getByLabel('Name', { exact: true }).fill(textSet);
  await page.getByRole('radio', { name: 'Text' }).click();
  await page.getByRole('button', { name: 'Save set' }).click();

  for (const [question, answer] of [
    ['Capital of Denmark?', 'Copenhagen'],
    ['Capital of Sweden?', 'Stockholm'],
  ]) {
    await page.getByRole('button', { name: 'Add card' }).click();
    await page.getByLabel('Question', { exact: true }).fill(question);
    await page.getByLabel('Answer', { exact: true }).fill(answer);
    await page.getByRole('button', { name: 'Save card' }).click();
    await expect(page.getByText(`${question} → ${answer}`)).toBeVisible();
  }

  // ...and a maths set: the cards come from the rule.
  await page.getByRole('button', { name: 'Cancel' }).first().click();
  await page.getByRole('button', { name: 'New set' }).click();
  await page.getByLabel('Name', { exact: true }).fill(mathSet);
  await page.getByRole('radio', { name: 'Maths' }).click();
  await page.getByRole('radio', { name: 'Addition' }).click();
  await page.getByLabel('Highest number').fill('10');
  await expect(page.getByTestId('math-example')).toBeVisible();
  await page.getByRole('button', { name: 'Save set' }).click();
  await expect(page.getByRole('listitem').filter({ hasText: mathSet })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: textSet })).toBeVisible();

  // The maths set is switched on for the child with two game lengths.
  await page.getByRole('radio', { name: 'Per child' }).click();
  const available = page.locator('[data-available-set]').filter({ hasText: mathSet });
  await available.getByLabel('Cards').fill('3');
  await available.getByLabel('Stars').fill('1');
  await available.getByRole('button', { name: 'Add a game length' }).click();
  await available.getByLabel('Cards').nth(1).fill('4');
  await available.getByLabel('Stars').nth(1).fill('3');
  await available.getByRole('button', { name: 'Switch on' }).click();
  await expect(page.locator('[data-enabled-set]').filter({ hasText: mathSet })).toBeVisible();
  // The text set stays off: the child can't see it.
  await expect(page.locator('[data-available-set]').filter({ hasText: textSet })).toBeVisible();

  // The child opens flash cards from Homework.
  await loginAs(await makeChildLoginUsable(child.username));
  await expect(page).toHaveURL(/\/child$/);
  await page.getByRole('link', { name: 'Homework' }).click();
  await page.getByTestId('homework-flash-cards').click();
  await expect(page).toHaveURL(/\/child\/homework\/flash-cards$/);

  const sameDaySteps = !nearMidnight();

  // First game: three cards, one answered "Not yet" (the first), so it comes back at the end.
  await page.getByRole('button', { name: '3 cards · 1 stars' }).click();
  await page.getByRole('button', { name: 'Start', exact: true }).click();
  await expect(page.getByTestId('flash-cards-progress')).toContainText('Card 1 of 3');
  await answerCard(page, false);
  await expect(page.getByTestId('flash-cards-progress')).toContainText('Card 2 of 3');
  await answerCard(page, true);
  await answerCard(page, true);
  await expect(page.getByTestId('flash-cards-repeat')).toBeVisible();
  await answerCard(page, true);

  await expect(page.getByTestId('flash-cards-known')).toHaveText(/2 of 3 first time/);
  await expect(page.getByTestId('flash-cards-stars')).toHaveText(/You earned 1 stars/);
  await expect(page.getByTestId('flash-cards-total')).toHaveText(/You have 1 stars in all/);

  // The guardian sees the finished game in the history.
  await loginAs(guardian);
  await page.goto('/guardian/flash-cards');
  await page.getByRole('radio', { name: 'Per child' }).click();
  const history = page.getByTestId('history');
  await expect(history.getByRole('row').filter({ hasText: mathSet })).toContainText(/3\s*2\s*1/);

  if (!sameDaySteps) {
    return;
  }

  // Same length again, the same day: nothing more is paid.
  await loginAs(await makeChildLoginUsable(child.username));
  await page.goto('/child/homework/flash-cards');
  await page.getByRole('button', { name: '3 cards · 1 stars' }).click();
  await page.getByRole('button', { name: 'Start', exact: true }).click();
  for (let card = 0; card < 3; card += 1) {
    await answerCard(page, true);
  }
  await expect(page.getByTestId('flash-cards-no-stars')).toBeVisible();
  await expect(page.getByTestId('flash-cards-total')).toHaveText(/You have 1 stars in all/);

  // The longer length pays the difference: 3 stars minus the 1 already paid.
  await page.getByRole('button', { name: 'Play again' }).click();
  await page.getByRole('button', { name: '4 cards · 3 stars' }).click();
  await page.getByRole('button', { name: 'Start', exact: true }).click();
  for (let card = 0; card < 4; card += 1) {
    await answerCard(page, true);
  }
  await expect(page.getByTestId('flash-cards-stars')).toHaveText(/You earned 2 stars/);
  await expect(page.getByTestId('flash-cards-total')).toHaveText(/You have 3 stars in all/);
});

// Built-in sets (docs/backend/analysis/flash-cards.md, "Built-in sets"): the catalogue ships with the
// app, so there is nothing to create. The guardian switches two of them on for a child (the default
// game length, 5 cards for 1 star), and the child plays one game of each: a clock face (its
// accessible name describes the hands, the words are the back) and a flag whose back is the Danish
// country name. A fresh set means a fresh "today", but the star payout is once per set and day, so the
// stars are only asserted away from midnight, like the test above.
test('a guardian switches on built-in clock and country sets, and the child plays both', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);

  await page.goto('/guardian/flash-cards');
  await expect(page.getByRole('heading', { name: 'Flash cards', level: 2 })).toBeVisible();
  // The library lists the catalogue read-only, with an example card per set.
  await expect(page.locator('[data-built-in-key="clock:1"]')).toBeVisible();
  await expect(page.locator('[data-built-in-key="countries:1"]')).toBeVisible();

  await page.getByRole('radio', { name: 'Per child' }).click();
  for (const name of ['Clock: whole hours', 'Countries: the Nordics']) {
    const available = page.locator('[data-available-set]').filter({ hasText: name });
    await available.getByRole('button', { name: 'Switch on' }).click();
    await expect(page.locator('[data-enabled-set]').filter({ hasText: name })).toBeVisible();
  }

  await loginAs(await makeChildLoginUsable(child.username));
  await page.goto('/child/homework/flash-cards');

  const sameDaySteps = !nearMidnight();

  // The clock set: the face stands in for the question, the back is the answer.
  await page.getByRole('button', { name: 'Clock: whole hours' }).click();
  await page.getByRole('button', { name: '5 cards · 1 stars' }).click();
  await page.getByRole('button', { name: 'Start', exact: true }).click();
  await expect(page.getByTestId('flash-cards-clock').getByRole('img')).toHaveAttribute(
    'aria-label',
    /hour hand.*minute hand/,
  );
  await page.getByRole('button', { name: 'Show the answer' }).click();
  await expect(page.getByTestId('flash-cards-answer')).not.toBeEmpty();
  await page.getByRole('button', { name: 'I knew it' }).click();
  for (let card = 1; card < 5; card += 1) {
    await answerCard(page, true);
  }
  await expect(page.getByTestId('flash-cards-known')).toHaveText(/5 of 5 first time/);
  if (sameDaySteps) {
    await expect(page.getByTestId('flash-cards-stars')).toHaveText(/You earned 1 stars/);
  }

  // The country set: a flag on the front, a Danish name on the back.
  await page.getByRole('button', { name: 'Play again' }).click();
  await page.getByRole('button', { name: 'Countries: the Nordics' }).click();
  await page.getByRole('button', { name: '5 cards · 1 stars' }).click();
  await page.getByRole('button', { name: 'Start', exact: true }).click();
  await expect(page.getByTestId('flash-cards-question')).not.toBeEmpty();
  await page.getByRole('button', { name: 'Show the answer' }).click();
  // Level 1 holds Danmark, Sverige, Tyskland...: whichever is drawn, the name is a non-empty Danish word.
  // The element's text keeps the template's surrounding whitespace, so the pattern allows it.
  await expect(page.getByTestId('flash-cards-answer')).toHaveText(/^\s*\p{L}[\p{L} ]*\p{L}\s*$/u);
  await page.getByRole('button', { name: 'I knew it' }).click();
  for (let card = 1; card < 5; card += 1) {
    await answerCard(page, true);
  }
  await expect(page.getByTestId('flash-cards-known')).toHaveText(/5 of 5 first time/);
  if (sameDaySteps) {
    await expect(page.getByTestId('flash-cards-total')).toHaveText(/You have 2 stars in all/);
  }
});
