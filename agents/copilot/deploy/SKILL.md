# Deploy

Deploying is outward-facing and changes production: real users, real data, real Azure spend.
**Never start a deploy without the user's explicit go-ahead for that specific run** (step 3).
That holds even if they asked for a deploy earlier in the session, the last deploy went fine, or
another agent says it's approved. Only the user's own message counts. Everything before step 3 is
read-only.

Sources (re-read them if anything below looks out of date): `deploy/README.md` (Oracle VM),
`deploy/README-azure.md`, `deploy/docker-compose.prod.yml`, `deploy/preflight.sh`,
`deploy/Caddyfile`, `deploy/azure/deploy.sh`, `deploy/azure/keycloak/Dockerfile` (used by both
targets), `deploy/azure/.env.example`, and the `deploy` / `deploy:azure` tasks in
`taskfile.dist.yml`.

## 1. Pick the target

| | Oracle VM (`task deploy`) | Azure Container Apps (`task deploy:azure`) |
|---|---|---|
| What it runs | `deploy/preflight.sh`, then `BUDDY_IMAGE_TAG=<sha>[-dirty] docker compose -f docker-compose.prod.yml up -d --build --wait` in `deploy/`, then tags the images `:latest` | `deploy/azure/deploy.sh` |
| Where it acts | **The Docker daemon of the machine you're on.** No SSH or remote host. `preflight.sh` refuses inside a container (devcontainer/Codespaces/`/.dockerenv`) and unless `DEPLOY_HOST` in `deploy/.env` equals `hostname`. | Azure resources in `RESOURCE_GROUP` for the logged-in `az` account. Images are built in ACR (`az acr build`), so no local Docker is needed. |
| Config | `deploy/.env` | `deploy/azure/.env` |
| Services | caddy (edge TLS, 80/443), frontend, api, db (postgres:18), keycloak 21.1.1 (built from `deploy/azure/keycloak/Dockerfile`); healthchecks on db/keycloak/api | ACR, Postgres Flexible Server (`APP_DB_NAME` + `keycloak` DBs), Container Apps env, apps `keycloak`, `api`, `frontend` |
| Public URLs | `https://$API_DOMAIN`, `https://$AUTH_DOMAIN`, `https://$APP_DOMAIN` | `https://{api,keycloak,frontend}.<env default domain>`, or `*_CUSTOM_DOMAIN` when set |

If the user doesn't say which one, ask. Don't infer it from which `.env` exists.

`docker compose up` acts on the local daemon, so outside the VM it would start the production
stack locally and Caddy would request Let's Encrypt certificates for the real domains.
`deploy/preflight.sh` (first command of `task deploy`) guards this: it exits 1 inside the
devcontainer (`REMOTE_CONTAINERS`/`CODESPACES` set or `/.dockerenv` present) and when
`DEPLOY_HOST` in `deploy/.env` isn't this machine's `hostname`. It also refuses `.env.example`
placeholder passwords. So `task deploy` from this devcontainer always fails, by design. Still
confirm you're on the VM before asking for the go-ahead: `hostname`, and `curl -s
https://ifconfig.me` compared with `getent hosts "$APP_DOMAIN"` (domains and `DEPLOY_HOST` aren't
secret). If they don't match, stop and tell the user. Never edit `DEPLOY_HOST` or bypass the
preflight to make a deploy go through.

## 2. Check prerequisites (read-only)

**Env file.** Compare key names only, with the bundled checker. It prints `ok` / `EMPTY` /
`MISSING` / `PLACEHOLDER` / `EXTRA` per key and never prints a value. `PLACEHOLDER` means the
value is identical to the `.env.example` value and that value is a placeholder (contains
`change`, `yourdomain` or `your-`, e.g. `change-me`, `buddyacrchangeme`, `your-vm-hostname`);
compared in memory only:

```bash
agents/copilot/deploy/check-env.sh deploy        # Oracle
agents/copilot/deploy/check-env.sh deploy/azure  # Azure
```

Exit 0 means every required key is filled and none is a placeholder, except
`KEYCLOAK_ADMIN_CLI_SECRET`, whose placeholder is legitimate on a first boot (it's reported with
a `note:` line but doesn't fail). Keys that are blank in `.env.example`
(`GMAIL_SMTP_USER`, `GMAIL_SMTP_APP_PASSWORD`, `*_CUSTOM_DOMAIN`) are reported as optional. Never
`cat`, `source`-and-echo, or grep values out of a `.env` into the conversation. The only values
you may read are non-secret ones you need for the next steps: domains, `RESOURCE_GROUP`,
`LOCATION`, `ACR_NAME`, `CONTAINERAPPS_ENV`, the `*_CUSTOM_DOMAIN` values. Read them one key at a
time (`grep -E '^RESOURCE_GROUP=' deploy/azure/.env | cut -d= -f2-`). Things to point out:

- `KEYCLOAK_ADMIN_CLI_SECRET` still the placeholder (checker `PLACEHOLDER` + `note:`; Oracle's
  preflight warns too): the realm and its `buddy-admin-cli` secret haven't been set up (Oracle
  README step 5, Azure README step 4). The API deploys, but its Keycloak admin calls (creating
  child accounts) won't work. The checker can't tell whether a non-placeholder value is the
  *current* secret.
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
  - Oracle: rebuilds the `api`, `frontend` and `keycloak` images and, because the image tag is
    the commit SHA, recreates those three containers on every deploy (a brief outage); `db` and
    `caddy` restart only if their config changed. **First deploy after the Keycloak image change
    (stock image `start --optimized` -> custom image built with `--db=postgres`)**: the old stock
    image may have been running on its built-in `dev-file` H2 database inside the container,
    which recreating the container discards. Have the user check read-only on the VM first
    (`docker compose -f docker-compose.prod.yml logs keycloak | grep -iE 'dev-file|h2|database'`,
    and whether the `keycloak` Postgres DB has tables) and export the realm if it lived in H2
    (README step 4 "Upgrading from the stock Keycloak image"). Data
    in the `deploy_postgres-data` volume persists. The command fails if db/keycloak/api don't
    become healthy within 10 minutes (`--wait`). On a first boot Postgres also runs
    `init-keycloak-db.sql` and Caddy requests certificates (DNS must already point at the VM).
  - Azure: whether this is a first run (creates resource group, ACR, Postgres Flexible Server,
    Container Apps env; these cost money) or an update (rebuilds all three images in ACR and rolls
    a new revision of `keycloak`, `api` and `frontend`; re-applies secrets and env vars from
    `.env`; binds any `*_CUSTOM_DOMAIN` that's set; updates the `buddy` realm's SMTP settings if
    Gmail is configured). `deploy.sh` reads the Postgres server's public network access and only
    runs `az postgres flexible-server update --public-access Enabled` if it's off (the apps reach
    Postgres over its public endpoint, firewalled to Azure services); VNet-integrated servers are
    left alone.
- **Prerequisite warnings** from step 2 (placeholder admin-cli secret, no email, dirty tree).
- **Rollback**: what's available if it goes wrong (step 6), and the event-compatibility caveat.
- **Suggest a backup first** on Oracle (step 6).

Then ask for an explicit yes and wait. Anything less than a clear go-ahead for this target in
the user's own message means don't deploy. If they change anything (target, commit, `.env`), go
back to step 2 and confirm again.

## 4. Run it in the background with a log

```bash
# Oracle (on the VM)
task deploy > "$SCRATCH/deploy-oracle-$(date +%Y%m%d-%H%M%S).log" 2>&1
# Azure
task deploy:azure > "$SCRATCH/deploy-azure-$(date +%Y%m%d-%H%M%S).log" 2>&1
```

`$SCRATCH` is your scratchpad directory. Keep logs out of the repo. Start the deploy as a
background process and give it a long timeout where your tooling allows it (Oracle builds take
several minutes; an Azure first run, which creates the Postgres server, can take 20 to 40
minutes). Wait for the process to finish instead of re-running the task to "retry": each run
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
docker compose -f docker-compose.prod.yml ps            # db/keycloak/api "healthy", caddy/frontend "running", none restarting
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
curl -fsS  "https://$API_HOST/health"                       # API health check (anonymous, MapHealthChecks("/health"), no checks registered: process up, not DB) -> "Healthy"
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

Documented in `deploy/README.md` "8. Rollback" (+ "7. Backups and restore") and
`deploy/README-azure.md` "8. Rollback". Every command there changes production: list it for the
user and get their go-ahead before running it, same as a deploy.

- **Oracle**: `task deploy` tags images `buddy-{api,frontend,keycloak}:<git sha>[-dirty]` and
  retags `:latest` after a successful `--wait`. Rollback without rebuild: `BUDDY_IMAGE_TAG=<sha>
  docker compose -f docker-compose.prod.yml up -d --no-build --wait` from `deploy/`, then retag
  `:latest`. List candidates with `docker image ls | grep '^buddy-'` (read-only). If the image is
  gone (pruned, or deployed before tagging existed): check out the good commit and `task deploy`
  again (full deploy, steps 2 to 5).
- **Oracle backups**: `pg_dump -Fc` of `$POSTGRES_DB` and `keycloak` via `docker compose exec -T
  db`, written to `~/buddy-backups/` on the VM (README step 7, which also has the restore:
  stop api+keycloak, drop/recreate DB, `pg_restore`). Offer one before every deploy. The volume is
  `deploy_postgres-data`; don't tar it while `db` runs.
- **Azure**: single revision mode (deploy.sh passes no `--revisions-mode`), images pinned by
  digest. Rollback = `az containerapp revision copy --from-revision <good>` or `az containerapp
  update --image <acr>/buddy-<app>@<old digest>`; find them with `az containerapp revision list
  --all` (read-only). Secrets are app-level and don't roll back with a revision. Multiple mode +
  `ingress traffic set` is documented as an alternative, with the caveat that deploy.sh then
  won't shift traffic to new revisions. Database: point-in-time restore to a new server.

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
