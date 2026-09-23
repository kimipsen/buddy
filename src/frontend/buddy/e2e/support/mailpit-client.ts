// Mirrors BuddyApiFixture.GetMailpitMessagesToAsync / GetMailpitMessageTextAsync on the backend --
// same Mailpit HTTP API, used here to read invite/verification emails sent during e2e flows.
// Defaults to the devcontainer/CI compose stack's published Mailpit web port (see
// .devcontainer/docker-compose.yml); override MAILPIT_BASE_URL if that ever differs.
const MAILPIT_BASE_URL = process.env['MAILPIT_BASE_URL'] ?? 'http://localhost:9025';

export interface MailpitMessageSummary {
  ID: string;
  [key: string]: unknown;
}

export async function getMessagesTo(emailAddress: string): Promise<MailpitMessageSummary[]> {
  const response = await fetch(
    `${MAILPIT_BASE_URL}/api/v1/search?query=${encodeURIComponent(`to:${emailAddress}`)}`,
  );

  if (!response.ok) {
    throw new Error(
      `Mailpit search for '${emailAddress}' failed: ${response.status} ${response.statusText}`,
    );
  }

  const body = (await response.json()) as { messages: MailpitMessageSummary[] };
  return body.messages;
}

export async function getMessageText(messageId: string): Promise<string> {
  const response = await fetch(`${MAILPIT_BASE_URL}/api/v1/message/${messageId}`);

  if (!response.ok) {
    throw new Error(
      `Mailpit message fetch for '${messageId}' failed: ${response.status} ${response.statusText}`,
    );
  }

  const body = (await response.json()) as { Text: string };
  return body.Text;
}

// Polls Mailpit for a message to `emailAddress` whose body contains `textFragment`, rather than
// just grabbing the newest message to that address -- `fullyParallel` specs (and repeated local
// runs) can all be inviting the same seeded account's real email (e.g. bob@buddy.test, used as a
// stand-in "fresh guardian" below since accepting an invite requires an existing, email-verified
// Keycloak account -- see guardian-invite-accept.spec.ts) at close to the same time, so the
// newest message in that inbox isn't reliably *this* test's own. `textFragment` should be
// something only this test's own invite body would contain (e.g. a uniquely-generated child or
// group name). Also covers the invite email simply not having landed yet the instant the
// triggering request resolves.
export async function waitForMessageTextContaining(
  emailAddress: string,
  textFragment: string,
  timeoutMs = 15_000,
): Promise<string> {
  const deadline = Date.now() + timeoutMs;
  let lastError: unknown = null;

  while (Date.now() < deadline) {
    try {
      const summaries = await getMessagesTo(emailAddress);

      for (const summary of summaries) {
        const text = await getMessageText(summary.ID);

        if (text.includes(textFragment)) {
          return text;
        }
      }
    } catch (error) {
      lastError = error;
    }

    await new Promise((resolve) => setTimeout(resolve, 500));
  }

  const suffix = lastError instanceof Error ? ` (last error: ${lastError.message})` : '';
  throw new Error(`No message to '${emailAddress}' containing '${textFragment}' arrived within ${timeoutMs}ms${suffix}`);
}

// Pulls the invite token out of an invite email's plain-text body -- SmtpEmailSender.BuildLink
// (backend) writes the link as a plain "{FrontendBaseUrl}/{path}/{token}" line with no markup
// around it, so a simple path-scoped regex is enough. `path` distinguishes a group invite
// ("invite") from a guardian invite ("guardian-invite") -- see AcceptInvite/AcceptGuardianInvite's
// routes.
export function extractInviteToken(messageText: string, path: 'invite' | 'guardian-invite'): string {
  const match = new RegExp(String.raw`/${path}/(\S+)`).exec(messageText);

  if (!match) {
    throw new Error(`No '${path}' link found in message text:\n${messageText}`);
  }

  return decodeURIComponent(match[1]);
}
