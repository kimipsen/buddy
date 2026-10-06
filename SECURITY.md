# Security policy

Buddy stores data about children, including medicines and sleep diaries, so security reports are
taken seriously.

## Reporting a vulnerability

Please **don't open a public issue** for a security problem. Report it privately through GitHub
instead:

1. Open the repository's **Security** tab.
2. Choose **Report a vulnerability**.
3. Describe the problem, how to reproduce it, and what an attacker could do with it.

The report is visible only to the maintainer until an advisory is published.

## What to expect

Buddy is maintained by one person, so handling is best effort:

- You'll get an acknowledgement, and a fix or a plan, as soon as the maintainer can manage.
- You'll be told when the fix is deployed. You're credited in the advisory unless you'd rather
  not be.

## Supported versions

Only the latest version, `master` as deployed, gets security fixes.

## Scope

In scope: the API (`src/backend`), the frontend (`src/frontend`), the Keycloak theme and realm
setup, and the deployment configuration in `deploy/`.

Especially interesting:

- One family reading or changing another family's data.
- Getting around the guardian/child access rules.
- Leaks through share links or iCal feed tokens.
- Leaks of stored AI provider keys.

Out of scope:

- Vulnerabilities in third-party services (Keycloak, Postgres, AI providers) themselves. Report
  those to their maintainers.
- Denial of service by flooding.
- Findings that need an already compromised device or account.

Please test only against your own local instance (see the README), never against someone else's
data.
