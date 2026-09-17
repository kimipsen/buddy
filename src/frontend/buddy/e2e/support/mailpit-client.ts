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
