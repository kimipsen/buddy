# Deploying to the Oracle Cloud free VM

Scope: ASP.NET API + PostgreSQL + Keycloak + the Angular frontend, all on one
Always Free VM behind Caddy. RabbitMQ/Redis/Mailpit are not part of this (see
the devcontainer compose for those, if they become real dependencies later).

The frontend is a static SPA (no SSR/Node server at runtime — Angular's
build has no `server.ts`/`@angular/ssr`), so it's built once by Node and then
served as plain static files by its own small Caddy container.

## 1. Provision the VM

1. Create an Oracle Cloud account, then in the console create a Compute
   instance using the **Always Free** ARM shape (`VM.Standard.A1.Flex`,
   e.g. 2 OCPU / 12GB — stays within the always-free allowance).
2. Pick Ubuntu as the image. Note the public IP.
3. In the instance's VCN security list (or NSG), allow ingress on
   `22` (SSH), `80`, `443`. Everything else (5432, keycloak's admin port,
   etc.) should stay closed to the internet — only Caddy is public.
4. SSH in and install Docker + the compose plugin:
   ```
   curl -fsSL https://get.docker.com | sh
   sudo usermod -aG docker $USER
   sudo apt-get install -y docker-compose-plugin
   ```

## 2. DNS

Point three A records at the VM's public IP:
`api.yourdomain.com`, `auth.yourdomain.com`, `app.yourdomain.com`.

## 3. Configure secrets

```
git clone <this repo> && cd buddy/deploy
cp .env.example .env
# fill in real values, especially POSTGRES_PASSWORD and KEYCLOAK_ADMIN_PASSWORD
```

`KEYCLOAK_ADMIN_CLI_SECRET` can be a placeholder on the very first boot —
you'll generate the real one in step 5 and then restart the `api` service.

## 4. First boot

```
docker compose -f docker-compose.prod.yml up -d --build
```

This builds the API and frontend images, starts Postgres (creating both the
app DB and, via `init-keycloak-db.sql`, the `keycloak` DB), starts Keycloak
in production mode, and gets the edge Caddy to issue Let's Encrypt certs for
`API_DOMAIN`, `AUTH_DOMAIN`, and `APP_DOMAIN` automatically (DNS must already
resolve for this to succeed).

The frontend's `runtime-config.json` (authority/API URL) is baked in at
**build time** from `API_DOMAIN`/`AUTH_DOMAIN` in `.env` — see
`src/frontend/buddy/Dockerfile`. If you change either domain later, you need
to rebuild the `frontend` image (`docker compose -f docker-compose.prod.yml
up -d --build frontend`), not just restart it.

## 5. Configure the realm

The dev realm isn't automatically ported over. Easiest path:

1. Log into `https://auth.yourdomain.com` as `KEYCLOAK_ADMIN`.
2. Export the realm from your working dev Keycloak (Realm settings >
   Action > Partial export, include clients) and import it here, **or**
   recreate the `buddy` realm and its clients (`buddy-frontend`,
   `buddy-admin-cli`) by hand.
3. Update each client's **Valid redirect URIs** / **Web origins** to the
   real `app.yourdomain.com` / `api.yourdomain.com` values.
4. Generate a new secret for `buddy-admin-cli` (Clients > buddy-admin-cli >
   Credentials), put it in `.env` as `KEYCLOAK_ADMIN_CLI_SECRET`, then:
   ```
   docker compose -f docker-compose.prod.yml up -d api
   ```

## Notes

- The `.NET nightly` SDK/runtime image tags in
  `../src/backend/buddy/Dockerfile` track a floating `11.0-preview` tag.
  Pin it to the exact preview version your devcontainer uses
  (`dotnet --version`) before relying on this for anything long-lived —
  a floating preview tag can drift out from under you on a rebuild.
- Backups: `postgres-data` is a named Docker volume holding both the app
  data and Keycloak's data. Snapshot it regularly
  (`docker run --rm -v postgres-data:/data -v $(pwd):/backup alpine tar czf /backup/pg-backup.tar.gz /data`)
  — there's no managed backup here, this is a single VM.
- Renewing TLS certs, restarting on reboot, and image updates are all your
  responsibility on a self-hosted VM; `restart: unless-stopped` handles
  process crashes/reboots, but not certificate or OS-level maintenance.
