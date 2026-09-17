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
be a placeholder for now; you'll generate the real one in step 5 and update
the `api` app.

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
4. Creates the PostgreSQL Flexible Server, opens it to Azure services only,
   and creates both the `buddy` app database and the `keycloak` database.
5. Creates the Container Apps environment, then computes the three apps'
   final FQDNs (`<app-name>.<environment-domain>`) up front — Container Apps
   FQDNs are deterministic, so every app's env vars can reference its peers'
   real URLs on first create, with no create-then-patch step needed.
6. Builds the Keycloak image (`quay.io/keycloak/keycloak:21.1.1` + the
   `themes/buddy` theme) via `az acr build` — builds happen in Azure, no
   local Docker daemon required — and deploys it.
7. Builds and deploys the API image, wired up with Gmail SMTP if
   `GMAIL_SMTP_USER`/`GMAIL_SMTP_APP_PASSWORD` are set in `.env` — see "Mail"
   below.
8. Builds the frontend image with `API_BASE_URL`/`KEYCLOAK_AUTHORITY` passed
   as build args (baked into `runtime-config.json` at build time, same as the
   Oracle setup), and deploys it.
9. Points Keycloak's `buddy` realm at the same Gmail SMTP credentials via its
   Admin REST API, so Keycloak's own emails (password resets, address
   verification) send too — skipped with a note on the very first run, since
   the realm doesn't exist until step 4 below.

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
goes through **Gmail SMTP**, configured entirely via `.env`, no Azure resource
involved.

This replaced an earlier Azure Communication Services (ACS) approach: ACS
Email's SMTP AUTH requires a Microsoft Entra app registration scoped to the
ACS resource, which needs a directory permission that `Contributor` on the
resource group doesn't grant — in a managed tenant, getting that permission
(or the RBAC rights to assign the app its role) can mean waiting on an admin,
which wasn't worth it here just to send a handful of transactional emails.
Gmail SMTP needs nothing beyond a Google account.

**Setup:**

1. Enable 2-Step Verification on the Gmail account you want to send from
   (Google Account > Security) — required for the next step to be available.
2. Create an [App Password](https://myaccount.google.com/apppasswords) for
   it. This is a 16-character password scoped to SMTP, distinct from your
   normal Gmail password — that won't work here.
3. In `.env`, set:
   ```
   GMAIL_SMTP_USER=you@gmail.com
   GMAIL_SMTP_APP_PASSWORD=<the 16-character app password>
   ```
4. Run `./deploy.sh`. Both are optional — leave them blank and the API and
   Keycloak deploy without outbound email (same as before this feature
   existed), with a warning printed either way.

`deploy.sh` sets `Mail__Host`/`Mail__Port`/`Mail__Username`/`Mail__Password`/
`Mail__FromAddress` on the `api` app (password via a `mail-smtp-password`
Container App secret, same as every other credential in this script), and —
once the `buddy` realm exists (step 4 below) — configures Keycloak's Realm
Settings > Email to match via the Admin REST API. On the very first
`./deploy.sh` run the realm doesn't exist yet, so that last part just prints a
note and skips — rerun `./deploy.sh` after finishing step 4 to pick it up (or
set it manually in Realm Settings > Email if you'd rather not rerun the whole
script).

**Limitations of Gmail SMTP**: sending quotas are low (around 500/day for a
regular account) and mail comes from a personal-looking `@gmail.com` address
— fine for a small app's transactional volume, not meant as a long-term
production mail provider. If you outgrow it, look at a dedicated transactional
email provider (Azure Communication Services once the Entra permission is
sorted, SendGrid, Postmark, etc.) — since `IEmailSender`/`MailOptions` is
plain SMTP already, most providers are a drop-in `.env` change (`Mail__Host`
etc.) rather than a code change.

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

## Notes

- **Redeploying after a code change**: just rerun `./deploy.sh`. It rebuilds
  all three images and updates each app pinned to that build's image digest,
  so the new revision always rolls out even though the tag is always
  `:latest`.
- **ACR auth**: this script uses ACR admin username/password for simplicity.
  For production hygiene, switch to a system-assigned managed identity per
  Container App with the `AcrPull` role instead, so there's no shared
  credential to rotate.
- **Postgres network exposure**: the `AllowAzureServices` firewall rule opens
  the server to any Azure resource in any tenant that has your credentials —
  the credentials are still required, but if you want to remove public
  network exposure entirely, integrate the Container Apps environment and the
  Flexible Server into the same VNet (`--infrastructure-subnet-resource-id`
  on the environment, private access on the server) instead.
- **Backups**: unlike the Oracle VM (manual volume snapshots), Flexible
  Server takes automated daily backups with point-in-time restore by default
  — no extra setup needed.
- The `.NET nightly` SDK/runtime image tags in
  `../../src/backend/buddy/Dockerfile` track a floating `11.0-preview` tag —
  pin it to the exact preview version you're relying on before treating this
  as a long-lived deployment, same caveat as the Oracle guide.
