# Setting up privacy for your Buddy

You run a Buddy instance, so you are its data controller. Buddy stores data about children,
including health data. [PRIVACY.md](../PRIVACY.md) lists what the code already does and what it
leaves to you. This guide walks through the part left to you, in the order that makes the work
easiest:

1. [Decide whether this applies to you](#1-decide-whether-this-applies-to-you)
2. [Turn off what you don't use](#2-turn-off-what-you-dont-use)
3. [Sign data processing agreements](#3-sign-data-processing-agreements)
4. [Keep backups for no more than 30 days](#4-keep-backups-for-no-more-than-30-days)
5. [Decide where the logs go](#5-decide-where-the-logs-go)
6. [Write the record of processing](#6-write-the-record-of-processing)
7. [Do a DPIA](#7-do-a-dpia)
8. [Write the privacy notice](#8-write-the-privacy-notice)
9. [Keep it up to date](#9-keep-it-up-to-date)

Plan on an evening or two. Steps 6 to 8 build on each other, so most of what you write in step 6
gets reused in steps 7 and 8.

This guide isn't legal advice. If you are unsure about something, ask your national data
protection authority. In Denmark that is [Datatilsynet](https://www.datatilsynet.dk), which
publishes templates and guidance in Danish.

## 1. Decide whether this applies to you

GDPR doesn't cover processing "in the course of a purely personal or household activity" (Article
2(2)(c)). How much of this guide you need depends on who uses your instance:

| Who uses your Buddy | What that likely means |
|---|---|
| Only your own household: you, a co-parent and your children | Probably the household exemption. Still do steps 2, 4 and 5. They protect your own children's data, and they cost little |
| Also other families, such as a group shared with another household, babysitters, or grandparents outside the home | The exemption gets doubtful. Do the whole guide |
| Families who aren't yours: you run Buddy for friends, a school or an association | GDPR applies in full. Do the whole guide |

If in doubt, do the whole guide. Health data about children is the kind of data where regulators
expect care.

## 2. Turn off what you don't use

Every feature is on by default. Data you never collect is data you don't have to protect, explain
or delete. Before you write anything else, turn off the features this family won't use (see
"Turning features off" in [deploy/README.md](../deploy/README.md#3-configure-secrets)).

The features that matter most for privacy:

| Flag | What turning it off removes |
|---|---|
| `Features__Medicines=false` | Medicine schedules and dose status (health data) |
| `Features__SleepDiary=false` | The sleep diary (health data) |
| `Features__MealplanAiAssistant=false` | Everything sent to an AI provider, and the stored AI keys |
| `Features__WorkLocations=false` | Guardians' work locations |

If you turn off both `Medicines` and `SleepDiary`, Buddy holds no health data and the DPIA (step
7) gets much smaller. If you turn off the AI assistant, no data leaves your instance for an AI
provider, and you can leave AI out of steps 3, 6, 7 and 8.

Note that turning a feature off hides it but keeps any data it already has. To get rid of that
data, the people involved have to delete their accounts, or you have to delete the data in the
database yourself.

## 3. Sign data processing agreements

Anyone who stores or handles the data for you is a processor and needs a data processing agreement
(DPA, Article 28). For the large providers, the DPA is usually part of the terms you accepted when
you created the account. Your job is to find it, check it, and keep a copy or a link.

| Processor | When | What to check |
|---|---|---|
| **Oracle Cloud** | The Docker setup ([deploy/README.md](../deploy/README.md)) | The Oracle Services DPA, which the Oracle Cloud Services agreement includes. Check that your home region is in the EU/EEA |
| **Microsoft Azure** | The Azure setup ([deploy/README-azure.md](../deploy/README-azure.md)) | The Microsoft Products and Services DPA, which the Product Terms include. The deploy script defaults to `LOCATION=swedencentral` (EU) |
| **Brevo** | If you send email through Brevo, as the Azure setup does | Brevo's DPA in its terms. It receives guardians' email addresses and the invitation and verification emails |
| **A log or monitoring service** | If you set `OTEL_EXPORTER_OTLP_ENDPOINT` (step 5) | Its DPA and where it stores data |
| **Off-site backup storage** | If you copy backups off the VM (step 4) | The storage provider's DPA and region |
| **Your DNS or CDN provider** | If it proxies traffic, as Cloudflare's proxy mode does | Whether it sees request contents. Plain DNS doesn't need a DPA |

**AI providers** (Anthropic, OpenAI, Google Gemini) are a special case. Each family enters its
own API key, so the agreement is between that family and the provider. Your part is to tell the
family, in the privacy notice and before they enter a key, that:

- they should use an account whose terms say inputs aren't used for training. Free tiers often
  allow training; check the provider's current terms;
- the provider may be outside the EU/EEA, and that is a transfer.

Write down what you found, including links, dates and regions. You need it in step 6.

## 4. Keep backups for no more than 30 days

Backups still contain people who have since deleted their account. The erasure ledger erases them
again after a restore, but only if the backup is restored the documented way. Old backups also
keep data longer than your privacy notice says. So delete backups after 30 days.

### Docker on the Oracle VM

[deploy/README.md section 7](../deploy/README.md#7-backups-and-restore) has the dump and restore
commands. To run them every night and delete old dumps, save this as `~/buddy-backup.sh` on the
VM. Adjust `DEPLOY_DIR` if you cloned the repo somewhere else.

```sh
#!/bin/sh
# Nightly Buddy backup: dumps both databases and deletes dumps older than 30 days.
set -eu

DEPLOY_DIR="$HOME/buddy/deploy"
BACKUP_DIR="$HOME/buddy-backups"
KEEP_DAYS=30

mkdir -p "$BACKUP_DIR"
cd "$DEPLOY_DIR"
ts=$(date +%Y%m%d-%H%M%S)

docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB"' > "$BACKUP_DIR/app-$ts.dump"
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_dump -U "$POSTGRES_USER" -Fc keycloak' > "$BACKUP_DIR/keycloak-$ts.dump"

# Fail loudly on a truncated dump instead of rotating good backups away.
for f in "$BACKUP_DIR/app-$ts.dump" "$BACKUP_DIR/keycloak-$ts.dump"; do
  docker compose -f docker-compose.prod.yml exec -T db pg_restore --list < "$f" > /dev/null
done

find "$BACKUP_DIR" -name '*.dump' -mtime +$((KEEP_DAYS - 1)) -delete
```

Then make it executable and schedule it:

```sh
chmod +x ~/buddy-backup.sh
crontab -e
# add this line: every night at 03:15
15 3 * * * $HOME/buddy-backup.sh >> $HOME/buddy-backup.log 2>&1
```

Check the next morning that two new `.dump` files are in `~/buddy-backups` and that
`~/buddy-backup.log` shows no errors.

**Copies off the VM need the same 30 days.** A backup on the VM's own disk doesn't survive losing
the VM, so copy the dumps somewhere else. Wherever they go, set up deletion there too:

- **Object storage** (OCI Object Storage, S3 and similar): add a lifecycle rule that deletes
  objects after 30 days.
- **Another machine or a NAS:** run the same `find ... -mtime +29 -delete` there.
- **A laptop or USB disk:** avoid it. Nobody remembers to delete those.

**Other copies of the disk count too.** If you turned on boot-volume or block-volume backups in
the Oracle console, they contain the database. Keep their retention at 30 days or less, or turn
them off and rely on the dumps.

### Azure

Azure Database for PostgreSQL Flexible Server takes its own backups. The default retention is 7
days, and it can be set from 7 to 35. Check that yours is 30 or less:

```sh
az postgres flexible-server show --resource-group <group> --name <server> \
  --query backup.backupRetentionDays
```

If you take extra `pg_dump` backups on top, rotate them as described for Docker.

The erasure-ledger steps for an Azure point-in-time restore aren't written yet (see "Still open"
in [PRIVACY.md](../PRIVACY.md#still-open)). Until they are, save the ledger before a restore and
put it back afterwards, as the Docker steps 0 and 4 do, against the restored server.

## 5. Decide where the logs go

Buddy logs who read a child's medicines or sleep diary. Those logs contain user and child ids,
never names or health data, but they are still personal data. Decide three things and write them
down:

1. **Where they go.** Set `OTEL_EXPORTER_OTLP_ENDPOINT` to a log backend
   ([observability.md](backend/observability.md)), or accept that they stay in the container logs.
2. **How long you keep them.** Pick a number, such as 90 days or a year. Long enough to answer
   "who looked at my child's medicines?", and no longer.
3. **Who can read them.** Usually just you.

On the Docker setup without a log backend, Docker's default log driver keeps container logs until
the container is removed. Set a size limit in `/etc/docker/daemon.json` (`"log-opts":
{"max-size": "10m", "max-file": "5"}`) so they don't grow forever, and know that this means logs
disappear after a size, not after a time.

## 6. Write the record of processing

The record of processing activities (Article 30) is an internal document. Nobody outside sees it
unless a regulator asks. One page is enough for a Buddy instance. Use your authority's template if
it has one (Datatilsynet does), or fill in this table:

| Field | For Buddy |
|---|---|
| Controller | Your name and contact details |
| Purpose | Coordinating the daily life of the families who use the instance: calendars, tasks, meals, pickups, and, if turned on, medicines, sleep and an AI meal planner |
| Categories of people | Guardians, children, babysitters, and members of shared groups |
| Categories of data | The list under "What Buddy holds" in [PRIVACY.md](../PRIVACY.md#what-buddy-holds), minus what you turned off in step 2. Mark medicines and sleep as health data (Article 9) |
| Recipients | The processors from step 3 |
| Transfers outside the EU/EEA | Any processor or AI provider outside the EU/EEA, and the safeguard it relies on (for example the EU-US Data Privacy Framework or standard contractual clauses) |
| Retention | Account data: until the account is deleted. AI sessions: 30 days after they end. Backups: 30 days (step 4). Logs: your choice from step 5 |
| Security measures | Sign-in through Keycloak; HTTPS only; database not reachable from the internet; stored AI keys and saved API responses encrypted; health-data reads logged; backups rotated after 30 days |

## 7. Do a DPIA

A data protection impact assessment (Article 35) is required when processing is likely to be high
risk. Health data about children, and data sent to an AI provider, are both on most regulators'
lists. If you kept `Medicines`, `SleepDiary` or the AI assistant on, and the household exemption
doesn't cover you, do one.

A DPIA for a Buddy instance doesn't need to be long. It answers four questions:

1. **What is processed and why?** Reuse the record from step 6.
2. **Is it necessary and proportionate?** Explain why each feature you kept on is needed, and why
   you turned the others off.
3. **What could go wrong for the people involved?** For example:
   - someone outside the family reads a child's medicines or sleep diary (a stolen password, a
     wrongly shared group);
   - the VM or a backup is lost or stolen;
   - a deleted account comes back from an old backup;
   - the AI provider keeps or trains on what is sent to it;
   - a guardian who shares custody sees more, or less, than intended.
4. **What reduces each risk?** For each risk, name the measures. Many are already in Buddy (see
   "What Buddy already does" in [PRIVACY.md](../PRIVACY.md#what-buddy-already-does)), and the rest
   are steps 2 to 5 of this guide. Then judge the risk that remains: low, medium or high.

If a risk stays high after the measures, you have to consult your data protection authority before
going ahead (Article 36). For a typical family instance with the measures above, that is unlikely.

Keep the DPIA with the record of processing.

## 8. Write the privacy notice

The privacy notice is for the guardians. Write it in plain language, in the languages your users
read. Give it to every guardian before they start using Buddy, for example as a link in the
invitation email or a page on your own site, and tell them where to find it later.

It has to cover:

- **Who you are** and how to reach you.
- **What you collect.** The list from step 6, in everyday words: "the medicines your child takes
  and when they were given", not "medicine schedules and dose status".
- **Why, and on what legal basis.** Health data needs an Article 9 condition. For a family app
  that is usually the guardian's explicit consent, given on the child's behalf. Say how to
  withdraw it: deleting the account, or asking you.
- **How long you keep it.** The retention line from step 6, including that backups are kept for
  30 days after an account is deleted.
- **Who receives it.** Your processors from step 3, and the AI provider the family picks. Say
  plainly that the family chooses the AI provider and should check its terms.
- **Transfers outside the EU/EEA,** if any.
- **Their rights and how to use them:**
  - access and portability: "Download my data" in the app;
  - erasure: deleting the account, or a sole guardian deleting a child, in the app;
  - correction, restriction and objection: by contacting you;
  - complaints: to the data protection authority, with its name.

Then check the AI notice in the app against your privacy notice. The text guardians acknowledge
before the first AI session lives in
`src/frontend/buddy/src/app/core/i18n/translations/*/mealplan.ts`, under
`aiAssistant.dataSharing`. If the two say different things, change one of them.

## 9. Keep it up to date

- **When you change the setup:** a new hosting provider, email provider, log backend, region, or
  a feature flag turned on, update steps 3 to 8 that it touches. Tell guardians when the privacy
  notice changes.
- **When you upgrade Buddy:** read the release notes for new data or new data flows, such as a new
  feature or an update check. Those need the same treatment.
- **Once a year:** check that the backup job still runs, that old backups really are gone, and
  that the DPAs and regions haven't changed.
- **If something goes wrong:** if personal data is lost, leaked or read by someone who shouldn't
  have it, you have 72 hours to report it to the data protection authority (Article 33), unless
  it is unlikely to harm anyone. If the risk is high, you also have to tell the families involved
  (Article 34). Keep a note of every incident, even the ones you don't report.
