# Deploying to Azure Container Apps

Scope: ASP.NET API + Keycloak + the Angular frontend as Container Apps, backed
by a managed Azure Database for PostgreSQL Flexible Server, all in one named
resource group. Everything below is CLI-only (`az`) — no portal clicking
required except optionally binding custom domains.

This is an alternative to [README.md](README.md) (the Oracle VM/docker-compose
setup). The two are independent — pick one per environment.

Why this shape, vs. a 1:1 port of the docker-compose file:

- **No edge Caddy.** Each Container App gets its own HTTPS ingress with a free
  managed certificate, so the reverse-proxy-for-TLS role Caddy played on the
  VM is redundant here.
- **Postgres is a managed service, not a container.** Container Apps has no
  good answer for persistent block storage; a managed Flexible Server gives
  you automated backups and point-in-time restore for free instead of you
  managing a Docker volume yourself.
- **The Keycloak theme is baked into a custom image** (`deploy/azure/keycloak/Dockerfile`)
  rather than volume-mounted, since Container Apps doesn't support the kind of
  bind mount the compose file uses.

## 1. Prerequisites

```
az login
az account set --subscription "<subscription name or id>"
```

You need `Contributor` (or equivalent) on the subscription/resource group. You
also need `jq` and `curl` installed locally — `deploy.sh` uses them to talk to
Keycloak's Admin REST API directly (there's no `az` command for that).

## 2. Configure

```
cd deploy/azure
cp .env.example .env
# fill in real values — pick globally-unique ACR_NAME / PG_SERVER_NAME,
# a resource group name and Azure region, and real passwords
```

`RESOURCE_GROUP` is the named resource group everything gets created in —
change it if `buddy-rg` isn't what you want. `KEYCLOAK_ADMIN_CLI_SECRET` can
be a placeholder for now; you'll generate the real one in step 4 ("Configure
the realm") and update the `api` app. `.claude/skills/deploy/check-env.sh
deploy/azure` compares `.env` with `.env.example` by key name and flags
values still equal to an `.env.example` placeholder, without printing values.

## 3. Deploy

```
./deploy.sh
```

This, in order:

1. Registers the required resource providers and the `containerapp` CLI
   extension.
2. Creates the resource group.
3. Creates an Azure Container Registry (admin credentials enabled, used by
   Container Apps to pull images).
4. Creates the PostgreSQL Flexible Server, makes sure public network access
   is on (only if it's currently off; see "Postgres network exposure"
   below), opens the firewall to Azure services only, and creates both the `buddy` app database and the `keycloak` database.
5. Creates the Container Apps environment, then computes the three apps'
   final FQDNs (`<app-name>.<environment-domain>`) up front — Container Apps
   FQDNs are deterministic, so every app's env vars can reference its peers'
   real URLs on first create, with no create-then-patch step needed.
6. Builds the Keycloak image (`quay.io/keycloak/keycloak:21.1.1` + the
   `themes/buddy` theme) via `az acr build` — builds happen in Azure, no
   local Docker daemon required — and deploys it.
7. Builds and deploys the API image, wired up with Brevo SMTP if
   `BREVO_SMTP_LOGIN`/`BREVO_SMTP_KEY`/`MAIL_FROM_ADDRESS` are set in `.env` —
   see "Mail" below.
8. Builds the frontend image with `API_BASE_URL`/`KEYCLOAK_AUTHORITY` passed
   as build args (baked into `runtime-config.json` at build time, same as the
   Oracle setup), and deploys it.
9. Points Keycloak's `buddy` realm at the same Brevo SMTP credentials via its
   Admin REST API, so Keycloak's own emails (password resets, address
   verification) send too — skipped with a note on the very first run, since
   the realm doesn't exist until you finish step 4 ("Configure the realm").

At the end it prints the three `https://*.azurecontainerapps.io` URLs.

## 4. Configure the realm

Same manual step as the Oracle deployment — the dev realm isn't ported
automatically:

1. Log into the printed Keycloak URL as `KEYCLOAK_ADMIN`.
2. Export the realm from your working dev Keycloak (Realm settings > Action
   > Partial export, include clients) and import it here, **or** recreate the
   `buddy` realm and its clients (`buddy-frontend`, `buddy-admin-cli`) by
   hand.
3. Update each client's **Valid redirect URIs** / **Web origins** to the
   real frontend/API URLs printed by `deploy.sh`.
4. Generate a new secret for `buddy-admin-cli` (Clients > buddy-admin-cli >
   Credentials), then update the running API app with it. The commands below
   use `$RESOURCE_GROUP` etc. from `.env` — load them into your shell first
   (they're not exported just by `deploy.sh` having run earlier):
   ```
   cd deploy/azure
   set -a; source .env; set +a

   az containerapp secret set \
     --name api --resource-group "$RESOURCE_GROUP" \
     --secrets keycloak-admin-cli-secret="$KEYCLOAK_ADMIN_CLI_SECRET"
   az containerapp revision restart \
     --name api --resource-group "$RESOURCE_GROUP" \
     --revision "$(az containerapp revision list --name api --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
   ```

## 5. Mail

Outbound email (the API's `IEmailSender`/`SmtpEmailSender` — verification and
invite emails — and Keycloak's own password-reset/address-verification mail)
goes through **Brevo's SMTP relay** (`smtp-relay.brevo.com:587`, STARTTLS),
configured entirely via `.env`, no Azure resource involved.

This replaced an earlier Azure Communication Services (ACS) approach: ACS
Email's SMTP AUTH requires a Microsoft Entra app registration scoped to the
ACS resource, which needs a directory permission that `Contributor` on the
resource group doesn't grant — in a managed tenant, getting that permission
(or the RBAC rights to assign the app its role) can mean waiting on an admin,
which wasn't worth it here just to send a handful of transactional emails.
After that it used Gmail SMTP with an App Password, which limited sending to
about 500 mails a day from a personal `@gmail.com` address. Brevo's free plan
(300 mails/day) lets you send from your own verified domain.

**Setup:**

1. Create a [Brevo](https://www.brevo.com) account.
2. Verify the address you want to send from (Senders, Domains & Dedicated
   IPs > Senders), or better, authenticate the whole domain (Domains: add
   the DKIM/DMARC DNS records Brevo shows) so mail doesn't land in spam.
3. Under SMTP & API > SMTP, note the **SMTP login** (looks like
   `1a2b3c001@smtp-brevo.com`, not your account email) and generate an
   **SMTP key**. An API key (`xkeysib-…`) won't work for SMTP.
4. In `.env`, set:
   ```
   BREVO_SMTP_LOGIN=1a2b3c001@smtp-brevo.com
   BREVO_SMTP_KEY=<the SMTP key>
   MAIL_FROM_ADDRESS=noreply@yourdomain.com
   MAIL_FROM_NAME=Buddy
   ```
5. Run `./deploy.sh`. All of these are optional. If the login, key or from
   address is blank, the API and Keycloak deploy without outbound email and
   the script prints a warning. `MAIL_FROM_NAME` defaults to `Buddy`.

`deploy.sh` sets `Mail__Host`/`Mail__Port`/`Mail__Credentials__Username`/`Mail__Credentials__Password`/
`Mail__FromAddress`/`Mail__FromName` on the `api` app (password via a `mail-smtp-password`
Container App secret, same as every other credential in this script), and —
once the `buddy` realm exists (step 4, "Configure the realm") — configures Keycloak's Realm
Settings > Email to match via the Admin REST API. On the very first
`./deploy.sh` run the realm doesn't exist yet, so that last part just prints a
note and skips — rerun `./deploy.sh` after finishing step 4 to pick it up (or
set it manually in Realm Settings > Email if you'd rather not rerun the whole
script).

Since `IEmailSender`/`MailOptions` is plain SMTP, switching to another
provider later (Postmark, SendGrid, ACS) means changing the host and
credentials in `deploy.sh`/`.env`, not the code.

## 6. Custom domains (optional)

By default everything runs on `*.azurecontainerapps.io` — `deploy.sh` skips
custom domains unless you ask for them, so you can start out on the Azure
domains and add your own later without redoing anything.

To use your own domains (e.g. `app.yourdomain.com`, `api.yourdomain.com`,
`auth.yourdomain.com`) instead:

1. In `.env`, set `FRONTEND_CUSTOM_DOMAIN` / `API_CUSTOM_DOMAIN` /
   `KEYCLOAK_CUSTOM_DOMAIN` to the domains you want (set only the ones you're
   ready to switch — the rest keep using their Azure domain).
2. For each domain, create a CNAME pointing at that app's default
   `*.azurecontainerapps.io` FQDN.
3. Rerun `./deploy.sh`.

On this run, `deploy.sh` rebuilds the frontend image with the new
`API_BASE_URL`/`KEYCLOAK_AUTHORITY` build args, updates the API app's
`Authentication__Keycloak__*` and `Cors__AllowedOrigins__0` env vars and the
Keycloak app's `KC_HOSTNAME` to the new domains, and adds + binds each
configured hostname to its Container App.

Domain verification needs a one-time TXT record (`asuid.<domain>`) in
addition to the CNAME. If the CNAME/TXT records haven't propagated yet, the
binding step prints the exact record to create and the command to fetch its
value — create it and rerun `./deploy.sh` again. Azure issues and manages the
TLS certificate for you once the records resolve.

## 7. Verify the deploy

`deploy.sh` exits non-zero if any `az` step fails, and prints `!!` lines for
non-fatal problems (custom domain not bound yet, Keycloak SMTP skipped). Then
check the running state (all read-only). Load `.env` first as in step 4, and
use the URLs `deploy.sh` printed:

```
az containerapp list -g "$RESOURCE_GROUP" \
  --query "[].{name:name, status:properties.runningStatus, revision:properties.latestRevisionName, ready:properties.latestReadyRevisionName}" -o table
az containerapp revision list -n api -g "$RESOURCE_GROUP" \
  --query "[].{name:name, active:properties.active, health:properties.healthState, running:properties.runningState, traffic:properties.trafficWeight}" -o table

curl -fsS  https://<api host>/health/ready        # -> {"status":"Healthy",...}
curl -fsSI https://<frontend host>/ | head -1     # -> HTTP/2 200
curl -fsS  https://<frontend host>/config/runtime-config.json   # apiBaseUrl / keycloak.authority point at the right hosts
curl -fsS  https://<keycloak host>/realms/buddy/.well-known/openid-configuration | jq -r .issuer
```

- `latestRevisionName` should equal `latestReadyRevisionName`; otherwise the
  new revision is still provisioning or failed (`az containerapp logs show -n
  api -g "$RESOURCE_GROUP" --tail 50`).
- `/health/ready` checks that the API reaches Postgres (`"Unhealthy"` and a
  503 if not) and Keycloak's OIDC discovery (`"Degraded"`, still 200, if
  not). `/health` only confirms the process is serving. A real login in the
  app is still the end-to-end test. See
  [docs/backend/observability.md](../docs/backend/observability.md).
- The issuer must be exactly `https://<keycloak host>/realms/buddy` (the API's
  `ValidIssuer`). A 404 on the `buddy` realm means step 4 hasn't been done.

The apps have no health probes configured, so Container Apps only knows the
container started. That's why the checks above are manual. The API's
`/health` (liveness) and `/health/ready` (readiness) are ready to be used as
probes, but `deploy.sh` doesn't configure them yet.

## 8. Rollback

### How deploy.sh rolls out

- No `--revisions-mode` is passed to `az containerapp create`, so all three
  apps use the default **single** revision mode: each `az containerapp
  update` creates a new revision, and once it's running it gets 100% of the
  traffic and the previous revision is deactivated (not deleted; inactive
  revisions are kept and listed).
- Each image is pinned by **digest** (`buddy-api@sha256:...`), not by
  `:latest`, so every revision records exactly which image it ran. ACR Basic
  has no retention policy, so the older digests stay in the registry after
  `:latest` moves on.
- Secrets (`az containerapp secret set`) are **app-level, not per revision**.
  Rolling back a revision does not roll back a secret or the `.env` values
  baked into one; env vars, on the other hand, are part of the revision
  template and do roll back with it.

### Roll an app back to its previous revision

```
cd deploy/azure && set -a; source .env; set +a
APP=api   # or keycloak / frontend

# 1. Find the last good revision and its image digest
az containerapp revision list -n "$APP" -g "$RESOURCE_GROUP" --all \
  --query "sort_by(@, &properties.createdTime)[].{name:name, created:properties.createdTime, active:properties.active, image:properties.template.containers[0].image}" -o table

# 2a. Recreate it as a new revision (same image + env vars + scale settings):
az containerapp revision copy -n "$APP" -g "$RESOURCE_GROUP" --from-revision <good-revision-name>

# 2b. Or just point the app back at the old image digest:
az containerapp update -n "$APP" -g "$RESOURCE_GROUP" \
  --image "$ACR_NAME.azurecr.io/buddy-$APP@sha256:<digest>"
```

Both create a new revision, which takes all traffic once it's running, the
same way a deploy does. Then run the checks in step 7. (The image repository
names are `buddy-api`, `buddy-keycloak` and `buddy-frontend`.)

`az containerapp revision activate` alone is not a rollback in single mode:
traffic follows the latest revision. To move traffic between two revisions
without creating a new one, switch the app to multiple revision mode first:

```
az containerapp revision set-mode -n "$APP" -g "$RESOURCE_GROUP" --mode multiple
az containerapp revision activate -n "$APP" -g "$RESOURCE_GROUP" --revision <good-revision-name>
az containerapp ingress traffic set -n "$APP" -g "$RESOURCE_GROUP" --revision-weight <good-revision-name>=100
```

In multiple mode the next `./deploy.sh` run creates a new revision **without
moving traffic to it**, because `deploy.sh` doesn't set traffic weights. Set
the mode back (`--mode single`) before the next deploy, or traffic stays on
the old revision.

Rolling back the frontend alone only makes sense if its baked-in
`runtime-config.json` (API/Keycloak URLs) still matches; rolling back the API
alone is the usual case. A later `./deploy.sh` run from the current tree
redeploys whatever is checked out, so to stay rolled back, check out the good
commit before running it again.

### Rolling back with a code checkout

The alternative that needs no revision handling: `git checkout <last good
commit>` and rerun `./deploy.sh`. That rebuilds all three images, so it's
slower and produces new digests, but it's the same path as any deploy.

### Data: events don't roll back

> **Warning:** the API stores its data as Marten event streams. Rolling back
> the API **code** doesn't remove events the newer version appended. If the
> release you're rolling back added new event types (or changed an event's
> shape), the older code may fail to load any stream containing them, e.g.
> errors loading an aggregate or rebuilding a snapshot, for exactly the users
> who used the new feature. Before rolling back across such a release, decide
> whether that's acceptable or whether you also need a database restore
> (below), which loses everything written since the restore point.

The database itself is rolled back with Flexible Server point-in-time
restore, which creates a **new** server (`az postgres flexible-server restore
--source-server "$PG_SERVER_NAME" --restore-time <UTC time> --name <new
name>`); you then point `PG_SERVER_NAME` at it and rerun `./deploy.sh`, or
update the `postgres-connection-string` / `pg-password` secrets and Keycloak's
`KC_DB_URL_HOST`. Keycloak's database is on the same server, so it rolls back
too (users created since then disappear).

## Notes

- **Redeploying after a code change**: just rerun `./deploy.sh`. It rebuilds
  all three images and updates each app pinned to that build's image digest,
  so the new revision always rolls out even though the tag is always
  `:latest`.
- **ACR auth**: this script uses ACR admin username/password for simplicity.
  For production hygiene, switch to a system-assigned managed identity per
  Container App with the `AcrPull` role instead, so there's no shared
  credential to rotate.
- **Postgres network exposure**: the Container Apps environment isn't
  VNet-integrated, so `keycloak` and `api` reach Postgres over its public
  endpoint. That's why `deploy.sh` needs public network access on the
  server: firewall rules only apply while it's on, and the
  `AllowAzureServices` rule (`0.0.0.0`) then admits Azure-originated traffic
  only. Nothing in `deploy.sh` connects to Postgres from your machine (the
  databases are created via `az postgres flexible-server db create`, through
  the Azure control plane). `deploy.sh` reads the current setting first and
  only runs `az postgres flexible-server update --public-access Enabled` when
  it's off (it used to run it on every deploy); a VNet-integrated server is
  left alone. Note that `AllowAzureServices` admits any Azure resource in any
  tenant that has your credentials — the credentials are still required, but
  if you want to remove public network exposure entirely, integrate the
  Container Apps environment and the Flexible Server into the same VNet
  (`--infrastructure-subnet-resource-id` on the environment, private access
  on the server) instead, and drop the firewall-rule step from `deploy.sh`.
- **Backups**: unlike the Oracle VM (manual volume snapshots), Flexible
  Server takes automated daily backups with point-in-time restore by default
  — no extra setup needed. See "Rollback" (step 8) for restoring it.
- Base images are pinned by tag and digest (the .NET tags match
  `global.json`), so redeploying the same commit rebuilds the same images. See
  the Oracle guide's notes.
