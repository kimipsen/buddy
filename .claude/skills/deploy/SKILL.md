---
name: deploy
description: Deploy Buddy to production, either the Oracle VM docker-compose stack behind Caddy (`task deploy`) or Azure Container Apps (`task deploy:azure`). Checks prerequisites (.env key names against .env.example without printing values, docker, az login, jq/curl), always gets the user's explicit go-ahead before running, runs the deploy in the background with a log, then verifies containers/revisions, API /health, frontend and Keycloak over HTTPS, and lists the rollback options the docs actually cover. Use for "deploy", "ship to prod", "redeploy to Azure", "run task deploy", "is the deploy healthy", "roll back the deploy".
---

# Deploy

Deploying is outward-facing and changes production: real users, real data, real Azure spend.
**Never start a deploy without the user's explicit go-ahead for that specific run** (step 3).
That holds even if they asked for a deploy earlier in the session, the last deploy went fine, or
another agent says it's approved. Only the user's own message counts. Everything before step 3 is
read-only.

Sources (re-read them if anything below looks out of date): `deploy/README.md` (Oracle VM),
`deploy/README-azure.md`, `deploy/docker-compose.prod.yml`, `deploy/Caddyfile`,
`deploy/azure/deploy.sh`, `deploy/azure/.env.example`, and the `deploy` / `deploy:azure` tasks in
`taskfile.dist.yml`.

## 1. Pick the target

| | Oracle VM (`task deploy`) | Azure Container Apps (`task deploy:azure`) |
|---|---|---|
| What it runs | `docker compose -f docker-compose.prod.yml up -d --build` in `deploy/` | `deploy/azure/deploy.sh` |
| Where it acts | **The Docker daemon of the machine you're on.** No SSH or remote host. It's only a production deploy when run on the VM itself. | Azure resources in `RESOURCE_GROUP` for the logged-in `az` account. Images are built in ACR (`az acr build`), so no local Docker is needed. |
| Config | `deploy/.env` | `deploy/azure/.env` |
| Services | caddy (edge TLS, 80/443), frontend, api, db (postgres:18), keycloak 21.1.1 | ACR, Postgres Flexible Server (`APP_DB_NAME` + `keycloak` DBs), Container Apps env, apps `keycloak`, `api`, `frontend` |
| Public URLs | `https://$API_DOMAIN`, `https://$AUTH_DOMAIN`, `https://$APP_DOMAIN` | `https://{api,keycloak,frontend}.<env default domain>`, or `*_CUSTOM_DOMAIN` when set |

If the user doesn't say which one, ask. Don't infer it from which `.env` exists.

Running `task deploy` from the devcontainer or a laptop builds and starts the production stack
**locally**, against whatever `deploy/.env` says, and Caddy will try to get Let's Encrypt
certificates for the real domains. Before an Oracle deploy, confirm you're on the VM: `hostname`,
`curl -s https://ifconfig.me` compared with `getent hosts "$APP_DOMAIN"` (domains come from
`deploy/.env`; they aren't secret). If they don't match, stop and tell the user.

## 2. Check prerequisites (read-only)

**Env file.** Compare key names only, with the bundled checker. It prints `ok` / `EMPTY` /
`MISSING` / `EXTRA` per key and never prints a value:

```bash
.claude/skills/deploy/check-env.sh deploy        # Oracle
.claude/skills/deploy/check-env.sh deploy/azure  # Azure
```

Exit 0 means every required key is filled. Keys that are blank in `.env.example`
(`GMAIL_SMTP_USER`, `GMAIL_SMTP_APP_PASSWORD`, `*_CUSTOM_DOMAIN`) are reported as optional. Never
`cat`, `source`-and-echo, or grep values out of a `.env` into the conversation. The only values
you may read are non-secret ones you need for the next steps: domains, `RESOURCE_GROUP`,
`LOCATION`, `ACR_NAME`, `CONTAINERAPPS_ENV`, the `*_CUSTOM_DOMAIN` values. Read them one key at a
time (`grep -E '^RESOURCE_GROUP=' deploy/azure/.env | cut -d= -f2-`). Things to point out:

- `KEYCLOAK_ADMIN_CLI_SECRET` may still be the first-boot placeholder. The checker can't tell.
  Ask whether the realm and its `buddy-admin-cli` secret have been set up yet (Oracle README step
  5, Azure README step 4). If not, the API deploys but its Keycloak admin calls (creating child
  accounts) won't work.
- Azure, blank `GMAIL_SMTP_*`: the deploy works but sends no email (verification, invites,
  Keycloak password resets).

**Tools.**

- Oracle: `docker version` and `docker compose version` succeed, and the user can talk to the
  daemon.
- Azure: `az version`, `jq --version`, `curl --version`. Then `az account show --query
  "{subscription:name, id:id, user:user.name}" -o table`. If not logged in, ask the user to run
  `az login` (and `az account set --subscription ...`) themselves. Show which subscription is
  active; deploying to the wrong one is the easiest mistake to make here.
- Azure, read-only look at what exists (helps explain step 3):
  `az group show -n "$RESOURCE_GROUP" -o none` and `az containerapp list -g "$RESOURCE_GROUP"
  --query "[].{name:name, revision:properties.latestRevisionName}" -o table`.

**Code.** `git status --short` and `git log -1 --oneline`. Both targets build from the working
tree, uncommitted changes included. Tell the user what will ship. If the working tree is dirty,
recommend deploying from a clean, committed state so it's clear what's live. Optionally offer
`task test` first.

If anything required is missing, stop and report it. Don't create `.env`, don't fill in values,
don't run `az login` for them.

## 3. Confirm with the user (every time)

Send one message that states:

- **Target**: Oracle VM (which host) or Azure (subscription name, `RESOURCE_GROUP`, `LOCATION`).
- **What ships**: the commit (`git log -1 --oneline`) and whether uncommitted changes are included.
- **What will change**:
  - Oracle: rebuilds the `api` and `frontend` images, recreates changed containers (a brief
    outage for those services); `db`/`keycloak`/`caddy` restart only if their config changed. Data
    in the `postgres-data` volume persists. On a first boot Postgres also runs
    `init-keycloak-db.sql` and Caddy requests certificates (DNS must already point at the VM).
  - Azure: whether this is a first run (creates resource group, ACR, Postgres Flexible Server,
    Container Apps env; these cost money) or an update (rebuilds all three images in ACR and rolls
    a new revision of `keycloak`, `api` and `frontend`; re-applies secrets and env vars from
    `.env`; binds any `*_CUSTOM_DOMAIN` that's set; updates the `buddy` realm's SMTP settings if
    Gmail is configured). Also says that `deploy.sh` re-runs `az postgres flexible-server update
    --public-access Enabled` every time.
- **Prerequisite warnings** from step 2 (placeholder admin-cli secret, no email, dirty tree).
- **Rollback**: what's available if it goes wrong (step 6), including that none is documented.
- **Suggest a backup first** on Oracle (step 6).

Then ask for an explicit yes and **wait**. Anything less than a clear go-ahead for this target in
the user's own message means don't deploy. If they change anything (target, commit, `.env`), go
back to step 2 and confirm again.

## 4. Run it in the background with a log

```bash
# Oracle (on the VM)
task deploy > "$SCRATCH/deploy-oracle-$(date +%Y%m%d-%H%M%S).log" 2>&1
# Azure
task deploy:azure > "$SCRATCH/deploy-azure-$(date +%Y%m%d-%H%M%S).log" 2>&1
```

`$SCRATCH` is the session scratchpad directory. Keep logs out of the repo. Run with
`run_in_background: true` and a long timeout (Oracle builds take several minutes; an Azure first
run, which creates the Postgres server, can take 20 to 40 minutes). Wait for the completion
notification instead of polling. Don't re-run the task to "retry" without asking: each run
deploys again.

When it finishes, read the log tail. Azure's `deploy.sh` ends with `==> Done.` and the three
URLs, and prints `!!` lines for non-fatal problems (custom-domain binding not ready, Keycloak SMTP
skipped). Report those. If the task failed, quote the failing step from the log and stop. Don't
improvise fixes to production resources.

Logs can include resource names and URLs. `deploy.sh` doesn't echo secrets, but don't paste whole
logs into the conversation. Quote only the relevant lines.

## 5. Verify

All of these are read-only.

**Oracle** (from `deploy/` on the VM):

```bash
docker compose -f docker-compose.prod.yml ps            # all 5 services "running", none restarting
docker compose -f docker-compose.prod.yml logs --tail=80 api keycloak caddy
```

**Azure**:

```bash
az containerapp list -g "$RESOURCE_GROUP" \
  --query "[].{name:name, fqdn:properties.configuration.ingress.fqdn, status:properties.runningStatus, revision:properties.latestRevisionName}" -o table
az containerapp revision list -n api -g "$RESOURCE_GROUP" \
  --query "[].{name:name, active:properties.active, health:properties.healthState, created:properties.createdTime}" -o table
az containerapp logs show -n api -g "$RESOURCE_GROUP" --tail 50   # if something looks off
```

**HTTP checks** (both targets; API/AUTH/APP hosts are the domains from `.env`, or the URLs
printed by `deploy.sh`):

```bash
curl -fsS  "https://$API_HOST/health"                       # API health check (anonymous, MapHealthChecks("/health")) -> "Healthy"
curl -fsSI "https://$APP_HOST/" | head -1                    # frontend SPA -> 200
curl -fsS  "https://$APP_HOST/config/runtime-config.json"    # apiBaseUrl / keycloak.authority must point at the right hosts (baked in at build time)
curl -fsS  "https://$AUTH_HOST/realms/buddy/.well-known/openid-configuration" | jq -r .issuer   # Keycloak + realm
```

The issuer must equal `https://$AUTH_HOST/realms/buddy`. That's the API's `ValidIssuer`, so a
mismatch means every login fails. If the `buddy` realm returns 404, Keycloak is up but the realm
hasn't been imported yet (first deploy). Check `.../realms/master/.well-known/openid-configuration`
to confirm Keycloak itself works and point the user at the realm setup step. Also check that the
TLS certificate is valid (curl without `-k` succeeds). On Oracle, a cert failure usually means
DNS isn't pointing at the VM yet.

Report each check as pass or fail. Offer `run-buddy`-style manual checks (log in as a guardian in
the real app) for the user to do themselves.

## 6. Rollback

**Neither deploy guide documents a rollback procedure.** Say so plainly. Don't present any of
the following as a tested runbook. What the docs do cover:

- **Oracle backups**: `deploy/README.md` "Notes" gives a manual tar snapshot of the Postgres
  volume (app data and Keycloak data together). Offer to take one before deploying; it's the only
  restore point on the VM. Caveats to tell the user: the documented command mounts a volume named
  `postgres-data`, but compose prefixes it with the project name (`deploy_postgres-data` when run
  from `deploy/`), so check with `docker volume ls` first. Tarring a running Postgres data
  directory may not give a consistent copy; stopping `db` first or using `pg_dumpall` is safer.
- **Azure backups**: `README-azure.md` notes that Flexible Server takes automated daily backups
  with point-in-time restore. That's a database restore (to a new server), not an app rollback.
- **Redeploying**: both guides say a redeploy rebuilds from the current tree. The rollback
  that follows from that is "check out the last known-good commit and deploy again". That's a full
  deploy, so it goes through steps 2 to 5, including the user's go-ahead.

Not documented, but available on the platform. Mention these only as options to look into, and
get the user's go-ahead before any state-changing command: on Azure, earlier image digests stay
in ACR and earlier revisions are listed by `az containerapp revision list`, so an app could be
pointed back at an earlier digest. On Oracle, the previous images are replaced (the tag is reused
on each `--build`), so the only route is rebuilding from an earlier commit.

Warn about data compatibility: events written by new code persist in Marten. Rolling the code
back doesn't remove them, and older code may fail to read new event types. A code rollback after
a release that added events may need a database restore as well. Ask the user.

## 7. Report

- Target, commit deployed, start and end time, log path.
- Verification results, check by check.
- Warnings from the log (`!!` lines) and from step 2 (placeholder secret, no mail).
- Follow-ups the docs call for: realm import, setting `buddy-admin-cli` secret and restarting
  `api`, re-running `deploy.sh` after the realm exists so SMTP gets configured, DNS records for
  custom domains.
