import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// Every AI provider settings call except "test connection" is 100% local on the backend --
// SetProviderApiKey/RemoveProviderApiKey/SetActiveProvider only validate, encrypt (Data
// Protection) and store the key; they never talk to Anthropic/OpenAI/Gemini (see
// SetProviderApiKey.Handler.cs -- no IAiChatClient dependency at all). So a garbage API key is
// safe to save for real. Only TestProviderConnectionHandler makes a genuine outbound HTTP call to
// the real provider host (api.anthropic.com et al) -- that call originates from the .NET backend
// itself, not the browser, so it can't be caught with page.route() at the frontend/backend
// boundary. Instead, this spec intercepts the browser's own request to *our* backend's
// test-connection endpoint and fulfills it directly, so that endpoint (and therefore any real
// egress) is never actually invoked.
test('guardian adds AI provider keys, switches the active one, tests a connection (stubbed), and removes a key', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.alice);

  await createChild(page);

  await page.goto('/guardian/admin');

  const section = page.locator('app-ai-provider-settings');
  await expect(section.getByRole('heading', { name: 'AI mealplan assistant' })).toBeVisible();

  const anthropicRow = section.getByRole('listitem').filter({ hasText: 'Anthropic (Claude)' });
  const openAiRow = section.getByRole('listitem').filter({ hasText: 'OpenAI (ChatGPT)' });
  const geminiRow = section.getByRole('listitem').filter({ hasText: 'Google (Gemini)' });

  // The section's heading renders even while its own list is still loading (only the list itself
  // is behind the @if), so wait for a row to actually be there before touching it.
  await expect(anthropicRow).toBeVisible();

  // AI provider credentials are resolved per *family* (MealFamilyResolution.
  // ResolveFamilyAiCredentialIdAsync), i.e. shared across every child of the same guardian -- not
  // scoped to the single child this run just created. Since alice's family is shared across
  // repeated runs of this spec against the same persistent backend, a previous run can leave keys
  // configured (and an active provider already set), which would make the "first key auto-
  // activates" assertion below unreliable. Reset to an empty slate first so this run behaves the
  // same regardless of what any earlier run left behind.
  // Every state-changing click below is paired with page.waitForResponse for the exact request it
  // triggers, rather than just polling the rendered text with a timeout: the component applies the
  // PUT/DELETE response's body directly (AiProviderSettingsComponent.applySettings), so once that
  // response is in hand the UI update is synchronous -- waiting on the network round trip itself is
  // more robust than waiting on a render that can only ever be as fast as that same round trip.
  for (const row of [anthropicRow, openAiRow, geminiRow]) {
    if (await row.getByRole('button', { name: 'Remove' }).isVisible()) {
      await row.getByRole('button', { name: 'Remove' }).click();
      await Promise.all([
        page.waitForResponse((res) => res.request().method() === 'DELETE' && res.url().includes('/ai/providers/') && res.url().endsWith('/key')),
        row.getByRole('button', { name: 'Confirm' }).click(),
      ]);
      await expect(row.getByRole('button', { name: 'Add key' })).toBeVisible();
    }
  }

  // Adding the very first key for the family auto-activates it (SetProviderApiKeyHandler: "the
  // family's very first key becomes the active provider automatically").
  await anthropicRow.getByRole('button', { name: 'Add key' }).click();
  await section.locator('input[name="apiKey"]').fill('fake-anthropic-key-1234');
  await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && res.url().includes('/ai/providers/') && res.url().endsWith('/key')),
    section.getByRole('button', { name: 'Save' }).click(),
  ]);

  await expect(anthropicRow.getByText('Active', { exact: true })).toBeVisible();
  await expect(anthropicRow.getByText('••••1234')).toBeVisible();

  // A second provider's key does NOT auto-activate (only the family's first key ever does).
  await openAiRow.getByRole('button', { name: 'Add key' }).click();
  await section.locator('input[name="apiKey"]').fill('fake-openai-key-5678');
  await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && res.url().includes('/ai/providers/') && res.url().endsWith('/key')),
    section.getByRole('button', { name: 'Save' }).click(),
  ]);

  await expect(openAiRow.getByText('••••5678')).toBeVisible();
  await expect(openAiRow.getByText('Active', { exact: true })).not.toBeVisible();
  await expect(anthropicRow.getByText('Active', { exact: true })).toBeVisible();

  // Switch the active provider to OpenAI.
  await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && res.url().includes('/ai/active-provider/')),
    openAiRow.getByRole('button', { name: 'Make active' }).click(),
  ]);
  await expect(openAiRow.getByText('Active', { exact: true })).toBeVisible();
  await expect(anthropicRow.getByText('Active', { exact: true })).not.toBeVisible();

  // Stub a successful test-connection response -- never lets the real request reach the backend
  // endpoint (and therefore never the real Anthropic API).
  await page.route('**/ai/providers/*/test-connection', async (route) => {
    await route.fulfill({ json: { isSuccessful: true, errorMessage: null } });
  });

  await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'POST' && res.url().includes('/test-connection')),
    anthropicRow.getByRole('button', { name: 'Test connection' }).click(),
  ]);
  await expect(anthropicRow.getByText('Connection succeeded.')).toBeVisible();

  // Now stub a failure response and re-test.
  await page.unroute('**/ai/providers/*/test-connection');
  await page.route('**/ai/providers/*/test-connection', async (route) => {
    await route.fulfill({ json: { isSuccessful: false, errorMessage: 'Incorrect API key provided.' } });
  });

  await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'POST' && res.url().includes('/test-connection')),
    anthropicRow.getByRole('button', { name: 'Test connection' }).click(),
  ]);
  await expect(anthropicRow.getByText('Connection failed.')).toBeVisible();
  await expect(anthropicRow.getByText('Incorrect API key provided.')).toBeVisible();

  await page.unroute('**/ai/providers/*/test-connection');

  // Remove the (now inactive) Anthropic key.
  await anthropicRow.getByRole('button', { name: 'Remove' }).click();
  await expect(anthropicRow.getByText('Remove this API key?')).toBeVisible();
  await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'DELETE' && res.url().includes('/ai/providers/') && res.url().endsWith('/key')),
    anthropicRow.getByRole('button', { name: 'Confirm' }).click(),
  ]);

  await expect(anthropicRow.getByRole('button', { name: 'Add key' })).toBeVisible();
  await expect(anthropicRow.getByText('••••1234')).not.toBeVisible();

  // OpenAI's key and active state are untouched by removing a different provider's key.
  await expect(openAiRow.getByText('Active', { exact: true })).toBeVisible();
  await expect(openAiRow.getByText('••••5678')).toBeVisible();
});
