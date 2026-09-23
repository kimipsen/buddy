import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// The AI mealplan assistant (MealplanAiAssistant / ai-assistant.ts) has three server round-trips:
// StartAiSession, SendAiSessionMessage and DiscardAiSession/ApplyAiSessionDraft. Only
// SendAiSessionMessage ever calls a real external AI provider (it resolves the family's active
// provider via IAiProviderRegistry, decrypts the guardian's stored key, and calls
// Anthropic/OpenAI/Gemini's real HTTP API synchronously -- see SendAiSessionMessage.Handler.cs) --
// exactly the same "never invoke a live third-party model" constraint as
// ai-provider-settings.spec.ts. StartAiSession, DiscardAiSession and ApplyAiSessionDraft only ever
// append their own domain event; they never touch a provider. That means the slot-toggle UI,
// starting a session, and discarding one can all be driven for real against the real backend --
// only the actual chat turn (sendMessage) needs stubbing, done here with page.route() using the
// exact AiSessionView shape from ai-assistant.service.ts (not a guess).
test('guardian configures a provider, toggles slots, starts/discards a real session, and sends a stubbed chat message', async ({
  page,
  loginAs
}) => {
  await loginAs(SEEDED_USERS.bob);

  // hasChildren gates the whole page (MealplanAiAssistant.load), same as the plain mealplan page.
  await createChild(page);

  // --- No provider configured yet: real, unstubbed state for a family that has never added an
  // AI provider key (or, on a repeat local run, may already have one -- see below). ---
  await page.goto('/guardian/mealplan/ai-assistant');
  await expect(page.getByRole('heading', { name: 'Chat with your assistant to draft a plan.' })).toBeVisible();

  const noProviderMessage = page.getByText('Add an AI provider API key in Settings before starting a session.');
  const hasProvider = !(await noProviderMessage.isVisible().catch(() => false));

  if (!hasProvider) {
    await expect(page.getByRole('link', { name: 'Go to Settings' })).toHaveAttribute('href', '/guardian/admin');

    // Configure a real (fake-value) provider key via the admin UI -- this only ever validates,
    // encrypts and stores the key (SetProviderApiKeyHandler never calls a provider), so it's safe
    // to do for real rather than stubbing hasProviderConfigured() on the frontend. Bob accumulates
    // AI provider state across repeated local runs of this spec (nothing here is cleaned up
    // between runs, same as every other e2e helper), so this only adds a key if none is active yet.
    await page.goto('/guardian/admin');
    const aiSection = page.locator('app-ai-provider-settings');
    await expect(aiSection.getByRole('heading', { name: 'AI mealplan assistant' })).toBeVisible();

    const anthropicRow = aiSection.getByRole('listitem').filter({ hasText: 'Anthropic (Claude)' });
    await expect(anthropicRow).toBeVisible();

    if (await anthropicRow.getByRole('button', { name: 'Add key' }).isVisible()) {
      await anthropicRow.getByRole('button', { name: 'Add key' }).click();
      await aiSection.locator('input[name="apiKey"]').fill('fake-anthropic-key-e2e');
      await aiSection.getByRole('button', { name: 'Save' }).click();

      // Wait for the save round-trip to actually land (the edit form closes back to a plain row)
      // before deciding whether it also needs activating -- checking with a plain, non-retrying
      // isVisible() right after the click races the pending request and can wrongly conclude
      // "Make active" isn't needed (or isn't there yet) while the save is still in flight.
      await expect(aiSection.locator('input[name="apiKey"]')).toHaveCount(0);
    }

    const makeActiveButton = anthropicRow.getByRole('button', { name: 'Make active' });

    if (await makeActiveButton.isVisible().catch(() => false)) {
      await makeActiveButton.click();
    }

    await expect(anthropicRow.getByText('Active', { exact: true })).toBeVisible();

    await page.goto('/guardian/mealplan/ai-assistant');
  }

  await expect(page.getByRole('heading', { name: 'Start a new session' })).toBeVisible();

  // --- Slot toggles: Dinner is selected by default (MealplanAiAssistant.selectedSlots starts as
  // Set([2])). Toggle it off to reach zero slots selected (the "choose a slot" guard + disabled
  // start button), then select Breakfast and Lunch instead. ---
  const startButton = page.getByRole('button', { name: 'Start planning' });
  const noSlotsHint = page.getByText('Choose at least one meal slot.');

  await expect(noSlotsHint).not.toBeVisible();
  await expect(startButton).toBeEnabled();

  await page.getByRole('button', { name: 'Dinner' }).click();
  await expect(noSlotsHint).toBeVisible();
  await expect(startButton).toBeDisabled();

  await page.getByRole('button', { name: 'Breakfast' }).click();
  await page.getByRole('button', { name: 'Lunch' }).click();
  await expect(noSlotsHint).not.toBeVisible();
  await expect(startButton).toBeEnabled();

  // --- Start a real session (StartAiSession only appends an event -- no provider call). ---
  await startButton.click();

  await expect(page.getByText('Drafting', { exact: true })).toBeVisible();
  await expect(page.getByText("Say hello to get started — tell the assistant what you're looking for.")).toBeVisible();
  await expect(page.getByText('Nothing proposed yet.')).toBeVisible();

  // --- Discard it for real (DiscardAiSession only appends an event -- no provider call either) --
  // exercises the discard-draft flow end to end without any stubbing. ---
  await page.getByRole('button', { name: 'Discard' }).click();
  await expect(page.getByText('Discard this session? This cannot be undone.')).toBeVisible();
  await page.getByRole('button', { name: 'Confirm' }).click();

  await expect(page.getByText('Discarded', { exact: true })).toBeVisible();

  // "Start a new session" is a plain button in this outcome view (session.startNewButton), not
  // the start form's own heading (start.title happens to share the same English text) -- scope by
  // role so this can't accidentally match the wrong one once we're back on the start form.
  await page.getByRole('button', { name: 'Start a new session' }).click();
  await expect(page.getByRole('heading', { name: 'Start a new session' })).toBeVisible();

  // --- Start a second real session, then stub only the chat round-trip (SendAiSessionMessage),
  // which is the one call that would otherwise reach a real AI provider. The stubbed response
  // shape is exactly AiSessionView from ai-assistant.service.ts, not a guess. ---
  await startButton.click();
  await expect(page.getByText('Drafting', { exact: true })).toBeVisible();

  await page.route('**/ai/sessions/current/messages', async (route) => {
    await route.fulfill({
      json: {
        id: 'e2e-stub-session',
        from: new Date().toISOString().slice(0, 10),
        to: new Date().toISOString().slice(0, 10),
        requestedSlots: [0, 1],
        status: 0,
        transcript: [
          { role: 0, text: 'Plan something quick for breakfast.', occurredAt: new Date().toISOString() },
          { role: 1, text: "Sure -- here's a draft for breakfast and lunch.", occurredAt: new Date().toISOString() }
        ],
        draft: [{ date: new Date().toISOString().slice(0, 10), slot: 0, mealId: 'e2e-stub-meal', mealName: 'Stubbed Oatmeal' }]
      }
    });
  });

  await page.getByLabel('Type a message…').fill('Plan something quick for breakfast.');
  await page.getByRole('button', { name: 'Send' }).click();

  await expect(page.getByText('Plan something quick for breakfast.')).toBeVisible();
  await expect(page.getByText("Sure -- here's a draft for breakfast and lunch.")).toBeVisible();
  await expect(page.getByText('Stubbed Oatmeal')).toBeVisible();

  await page.unroute('**/ai/sessions/current/messages');

  // Clean up the (stub-fed, never actually persisted server-side) draft by discarding for real
  // rather than applying it -- ApplyAiSessionDraft would be operating on the real backend's own
  // (empty) draft state, not the fake one this test just rendered client-side.
  await page.getByRole('button', { name: 'Discard' }).click();
  await page.getByRole('button', { name: 'Confirm' }).click();
  await expect(page.getByText('Discarded', { exact: true })).toBeVisible();
});
