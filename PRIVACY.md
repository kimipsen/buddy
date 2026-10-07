# Privacy and GDPR

Buddy stores data about children: their medicines, their sleep, their days. In the EU that is
special-category data under GDPR Article 9, about people who need extra protection. This page is
for anyone who runs their own Buddy, and for anyone who wants to help get this right.

It covers three things:

1. What Buddy already does in the code.
2. What you have to do yourself when you run it. The code can't do this part for you.
3. What is still open, and how to tell us what you think.

This page isn't legal advice. Whoever runs a Buddy instance is the data controller for it and
decides what the law requires of them. The design and the reasons behind it are in
[GDPR: erasure, export and data minimization](docs/backend/analysis/gdpr-data-protection.md).

## What Buddy holds

- **Accounts:** guardians' names, email addresses, time zone and language, and children's names
  and usernames. Sign-in itself happens in Keycloak, which runs alongside Buddy.
- **Family life:** calendars, tasks and progress, meal plans and ratings, pickups, babysitters,
  work locations and print templates.
- **Health data:** medicine schedules and dose status, and the sleep diary.
- **AI assistant:** the family's own provider API keys (encrypted), and each session's notes and
  chat.
- **Logs:** user and child ids (never names, emails or health data) in audit logs. See
  [observability.md](docs/backend/observability.md).

## What Buddy already does

- **Deleting an account erases the person.** It covers every feature, including Keycloak and
  copies in shared streams, and survives a restore from backup through the erasure ledger.
- **A sole guardian can delete a child.** The app shows what will be deleted before anything is.
- **Download my data:** every guardian can export what Buddy holds about them and their children,
  as JSON.
- **The AI assistant sends less.**
  - Children appear as "child 1", "child 2".
  - Calendar titles are only sent for the family's own items that concern the child (or nobody).
    Everything else is sent as "busy".
- **AI conversations are deleted after 30 days.** A session's notes and chat are wiped 30 days
  after the session ends.
- **A guardian has to acknowledge the AI notice first.** No AI session starts until a guardian
  has read what is sent to the family's chosen provider and acknowledged it.
- **Every read of health data is logged:** who read a child's medicines or sleep diary, and
  through which route (as guardian, as the child, or through a group share).
- **Secrets stay out of logs and storage:** tokens are kept out of telemetry, and stored AI keys
  and saved API responses are encrypted.

## What you have to do when you run Buddy

Work through this list before real families use your instance.

- [ ] **Privacy notice.** Tell guardians, in words a child's parent understands:
  - what you collect (the list above);
  - why, and on what legal basis — health data needs an Article 9 condition, usually explicit
    consent;
  - how long you keep it (see backups, logs and AI below);
  - who receives it: your hosting provider, your email provider, and the AI provider the family
    picks;
  - how to use their rights. Deleting the account and "Download my data" are in the app; say how
    to reach you for anything else.
- [ ] **Record of processing activities** (Article 30): what you process, why, for whom, where,
  and for how long.
- [ ] **Data protection impact assessment (DPIA)** (Article 35). Children's health data processed
  at scale, plus AI, will almost certainly need one.
- [ ] **Data processing agreements** (Article 28) with everyone who handles the data for you:
  - your hosting provider (Oracle Cloud for the Docker setup, or Azure);
  - your email provider (the deploy scripts use Brevo);
  - any log or monitoring service you add.
- [ ] **Back up for no more than 30 days.** Backups still contain people who have since deleted
  their account.
  - **Docker:** [deploy/README.md](deploy/README.md), section 7, has the dump commands and the
    restore steps. The restore steps put the erasure ledger back so erased people are erased
    again.
  - **Azure:** the managed database keeps its own point-in-time backups. Check their retention.
    The ledger steps for an Azure restore aren't written yet (see below).
- [ ] **Decide where the logs go, and for how long.** The health-data audit logs only help if they
  are kept somewhere. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to a log backend
  ([observability.md](docs/backend/observability.md)). Then decide who can read the logs and when
  they are deleted, and put that in your notice. Without a backend the logs stay wherever your
  container logs go.
- [ ] **Check the AI notice against your privacy notice.** The text guardians acknowledge is a
  starting point. It lives in `src/frontend/buddy/src/app/core/i18n/translations/*/mealplan.ts`,
  under `aiAssistant.dataSharing`.
  - Families bring their own key, so the AI provider is their choice. Buddy still assembles the
    data and sends it there.
  - If the provider is outside the EU/EEA, that is a transfer your notice should cover.

## Still open

These are known and not decided yet. Opinions welcome.

- **Free text that names someone.** "Pick up Emil" in a shared calendar's item title stays when
  Emil's account is deleted. It's the family's shared plan and the other members can edit it.
  The current leaning is to leave it that way.
- **Viewing access logs in the app.** Who has looked at my child's data is only in the logs for
  now. A screen for guardians is deferred: it needs its own retention decision.
- **Azure restore.** The erasure-ledger steps for restoring an Azure point-in-time backup aren't
  documented yet.

## Tell us what you think

- **A question, a disagreement, or something this page gets wrong:** open an issue in the
  repository. Changes to this page are welcome as a pull request.
- **Something that exposes people's data:** don't open a public issue. Report it privately as
  [SECURITY.md](SECURITY.md) describes.
