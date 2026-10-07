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
# and DEPLOY_HOST (the output of `hostname` on this VM)
```

`task deploy` runs `deploy/preflight.sh` first, which refuses to deploy:

- from inside a container (`REMOTE_CONTAINERS` / `CODESPACES` set, or
  `/.dockerenv` present, i.e. the devcontainer, Codespaces or a `docker exec`
  shell). `docker compose up` acts on the local Docker daemon, so run from the
  devcontainer it would start the production stack there and request real
  Let's Encrypt certificates for your domains;
- unless `DEPLOY_HOST` in `.env` equals this machine's `hostname`. This is a
  positive "this is the VM" check: a fresh `.env` from `.env.example`, or a
  copy on a laptop, fails it;
- while `POSTGRES_PASSWORD` or `KEYCLOAK_ADMIN_PASSWORD` is still the
  `.env.example` placeholder. A placeholder `KEYCLOAK_ADMIN_CLI_SECRET` only
  prints a warning (see below).

Values are compared without being printed.

`KEYCLOAK_ADMIN_CLI_SECRET` can be a placeholder on the very first boot —
you'll generate the real one in step 5 and then restart the `api` service.

## 4. First boot

From the repo root on the VM (needs [Task](https://taskfile.dev)):

```
task deploy
```

That runs the preflight above, then, in `deploy/`:

```
BUDDY_IMAGE_TAG=<git short SHA>[-dirty] docker compose -f docker-compose.prod.yml up -d --build --wait --wait-timeout 600
```

and, if every service came up, tags the three built images `:latest` too (so
`:latest` is always the last successful deploy). `--wait` makes the command
fail if a service with a healthcheck (db, keycloak, api) doesn't become
healthy within 10 minutes. Without Task, run the preflight and the compose
command yourself.

This builds the API, frontend and Keycloak images, starts Postgres (creating both the
app DB and, via `init-keycloak-db.sql`, the `keycloak` DB), starts Keycloak
in production mode (from `deploy/azure/keycloak/Dockerfile`, the same image
the Azure deployment uses: the stock image plus `kc.sh build --db=postgres`
and the buddy theme; `start --optimized` skips the build step, so the stock
image would ignore `KC_DB=postgres` and use its built-in `dev-file` H2
database), and gets the edge Caddy to issue Let's Encrypt certs for
`API_DOMAIN`, `AUTH_DOMAIN`, and `APP_DOMAIN` automatically (DNS must already
resolve for this to succeed).

The frontend's `runtime-config.json` (authority/API URL) is baked in at
**build time** from `API_DOMAIN`/`AUTH_DOMAIN` in `.env` — see
`src/frontend/buddy/Dockerfile`. If you change either domain later, you need
to rebuild the `frontend` image (rerun `task deploy`), not just restart it.

**Upgrading from the stock Keycloak image.** Earlier versions of this
compose file ran `quay.io/keycloak/keycloak:21.1.1` with `start --optimized`
directly. That image is built for `dev-file`, so Keycloak may have been
storing the realm in an H2 file inside the container instead of Postgres, and
the first `task deploy` with the custom image recreates the container and
starts on the (possibly empty) `keycloak` Postgres database. Before that
deploy, check which one is in use:

```
docker compose -f docker-compose.prod.yml logs keycloak | grep -iE 'dev-file|h2|database'
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'psql -U "$POSTGRES_USER" -d keycloak -c "\dt" | tail -n 3'
```

If psql says "Did not find any relations" (or the logs mention H2/dev-file), export the `buddy`
realm (Realm settings > Action > Partial export, include clients) before
deploying and re-import it afterwards (step 5); user accounts aren't part of a
partial export, so users have to be recreated or re-registered.

Startup order is driven by healthchecks: Keycloak waits for Postgres
(`pg_isready`), the API waits for both Postgres and Keycloak
(`/health/ready`), Caddy starts after the others have started.

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
   docker compose -f docker-compose.prod.yml up -d --no-build api
   ```
   (`:latest` is the image of the last successful `task deploy`; `--no-build`
   makes sure compose doesn't rebuild from the current tree instead.)

## 6. Verify the deploy

On the VM, from `deploy/`:

```
docker compose -f docker-compose.prod.yml ps   # db, keycloak, api "healthy"; caddy, frontend "running"
docker compose -f docker-compose.prod.yml logs --tail=80 api keycloak caddy
```

From anywhere:

```
curl -fsS  https://api.yourdomain.com/health          # -> Healthy
curl -fsS  https://api.yourdomain.com/version         # -> {"version":"1.2.0","commit":"..."}
curl -fsSI https://app.yourdomain.com/ | head -1       # -> HTTP/2 200
curl -fsS  https://app.yourdomain.com/config/runtime-config.json   # apiBaseUrl / keycloak.authority
curl -fsS  https://auth.yourdomain.com/realms/buddy/.well-known/openid-configuration | jq -r .issuer
```

- `/health` is the API's anonymous `MapHealthChecks("/health")` endpoint
  (also what the compose healthcheck polls). No checks are registered, so it
  says the API process is serving, **not** that Postgres or Keycloak are
  reachable. Keycloak's own `/health/ready` (enabled in the compose build)
  does include its database check; it's only polled inside the network.
- `/version` should show the version `task deploy` printed (from git tags, see
  [docs/versioning.md](../docs/versioning.md)).
- The issuer must be exactly `https://auth.yourdomain.com/realms/buddy` (the
  API's `ValidIssuer`). A 404 for the `buddy` realm means step 5 isn't done.
- curl without `-k` must succeed; a TLS failure usually means DNS doesn't
  point at the VM yet, so Caddy couldn't get a certificate.
- Finally log into the app as a guardian.

## 7. Backups and restore

Postgres holds both the app database (`POSTGRES_DB`) and Keycloak's
(`keycloak`), in the named volume `deploy_postgres-data` (compose prefixes
volumes with the project name, which is the directory name `deploy`; check
with `docker volume ls`). There's no managed backup on a single VM.

Don't tar the volume while `db` is running: that copies files mid-write and
may not restore. Dump with `pg_dump` through the running container instead
(consistent snapshot, no downtime). From `deploy/`:

```
mkdir -p ~/buddy-backups
ts=$(date +%Y%m%d-%H%M%S)
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB"' > ~/buddy-backups/app-$ts.dump
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_dump -U "$POSTGRES_USER" -Fc keycloak' > ~/buddy-backups/keycloak-$ts.dump

# sanity check: lists the archive's contents, fails on a truncated file
docker compose -f docker-compose.prod.yml exec -T db pg_restore --list < ~/buddy-backups/app-$ts.dump | head
```

Take one before every deploy, and copy them off the VM (`scp`, object
storage); a backup on the same disk doesn't survive losing the VM. Delete
backups after 30 days: they hold the data of people who have since deleted
their accounts (see the privacy notice and
[gdpr-data-protection.md](../docs/backend/analysis/gdpr-data-protection.md#backups)).

**Restore** (replaces the current data in that database; take a fresh dump
first if you might want it back):

```
# 0. save the erasure ledger: who has been erased since the backup was taken
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -c "copy (select id, data from erasure.mt_doc_erasureledgerentry) to stdout"' \
  > ~/buddy-backups/erasure-ledger-now.tsv

# 1. stop the apps that write to the databases
docker compose -f docker-compose.prod.yml stop api keycloak

# 2. recreate the app database empty and restore into it
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 \
    -c "DROP DATABASE \"$POSTGRES_DB\" WITH (FORCE)" -c "CREATE DATABASE \"$POSTGRES_DB\""'
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --exit-on-error' < ~/buddy-backups/app-<ts>.dump

# 3. same for Keycloak, if you're restoring it too (users/clients/secrets)
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 \
    -c "DROP DATABASE keycloak WITH (FORCE)" -c "CREATE DATABASE keycloak"'
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_restore -U "$POSTGRES_USER" -d keycloak --no-owner --exit-on-error' < ~/buddy-backups/keycloak-<ts>.dump

# 4. put the erasure ledger back (an older backup may not have the table yet)
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
    -c "create schema if not exists erasure" \
    -c "create table if not exists erasure.mt_doc_erasureledgerentry (id uuid primary key, data jsonb not null, mt_last_modified timestamptz default transaction_timestamp(), mt_version uuid not null default (md5(random()::text || clock_timestamp()::text))::uuid, mt_dotnet_type varchar)" \
    -c "create temp table ledger_import (id uuid, data jsonb)" \
    -c "\copy ledger_import from stdin" \
    -c "insert into erasure.mt_doc_erasureledgerentry (id, data) select id, data from ledger_import on conflict (id) do nothing"' \
  < ~/buddy-backups/erasure-ledger-now.tsv

# 5. start them again
docker compose -f docker-compose.prod.yml up -d --no-build --wait
```

About 30 seconds after the API starts, it erases again everyone on the ledger
whose data the backup brought back, Keycloak accounts included
(`UserErasureService`). Skip steps 0 and 4
only if nobody has deleted their account since the backup was taken.

Restore the two databases from the same backup run: the app's users are
linked to Keycloak user ids, so mixing timestamps can orphan accounts.

## 8. Rollback

Images are built on the VM, and `task deploy` tags each build with the git
commit (`buddy-api:<sha>`, `buddy-frontend:<sha>`, `buddy-keycloak:<sha>`,
with a `-dirty` suffix if the tree had uncommitted changes), so earlier builds
stay on the VM until you delete them:

```
docker image ls --format '{{.Repository}}:{{.Tag}}  {{.CreatedSince}}' | grep '^buddy-'
```

**Roll back to an earlier build without rebuilding** (from `deploy/`):

```
BUDDY_IMAGE_TAG=<good sha> docker compose -f docker-compose.prod.yml up -d --no-build --wait
for svc in api frontend keycloak; do docker tag "buddy-$svc:<good sha>" "buddy-$svc:latest"; done
```

`--no-build` matters: without it compose would build the *current* tree
under the old tag. Run it from a checkout whose `docker-compose.prod.yml` and
`.env` match that build (env vars and compose settings are not part of the
image). The `docker tag` keeps `:latest` (used by the manual commands in this
guide) pointing at what's running.

**If the image is gone** (pruned, or deployed before tagging existed): check
out the last good commit and deploy it again, which rebuilds it:

```
git checkout <good commit>
task deploy
```

Either way, run the checks in step 6 afterwards. Old images take disk space;
remove ones you won't roll back to with `docker image rm buddy-api:<sha> ...`.

> **Warning: events don't roll back.** The API stores its data as Marten
> event streams. Rolling back the code doesn't remove events the newer
> version appended. If the release you're rolling back added new event types
> (or changed an event's shape), the older code may fail to load any stream
> containing them, e.g. errors loading an aggregate or rebuilding a snapshot
> for the users who used the new feature. Before rolling back across such a
> release, decide whether that's acceptable or whether you also need to
> restore the database from a backup taken before the deploy (step 7), which
> loses everything written since.

## Notes

- Every base image is pinned by tag and digest: the .NET SDK/runtime in
  `../src/backend/buddy/Dockerfile` (the SDK tag matches `global.json`; bump
  them together), Node and Caddy in the frontend Dockerfile, and Caddy,
  Postgres and Keycloak here and in `azure/keycloak/Dockerfile`. A rebuild of
  the same commit therefore uses the same images. Dependabot refreshes the
  digests.
- Backups: see step 7. (An earlier version of this guide tarred a volume
  named `postgres-data`; the real volume is `deploy_postgres-data`, so that
  command backed up a new, empty volume.)
- Database connections: the `db` container runs with Postgres's default
  `max_connections=100`, shared by the API and Keycloak. The API uses one
  shared Npgsql pool for all its Marten stores, capped at
  `Maximum Pool Size=50` by default
  (`src/backend/buddy/Common/Postgres/PostgresDataSource.cs`), which leaves
  the rest for Keycloak and `psql`/`pg_dump`. To change the cap, append
  `;Maximum Pool Size=N` to `ConnectionStrings__Postgres` in
  `docker-compose.prod.yml` and leave room under 100 for Keycloak (its own
  pool, `KC_DB_POOL_MAX_SIZE`, defaults to 100 but normally holds only a
  handful). Check usage with
  `docker compose exec db psql -U "$POSTGRES_USER" -c "select count(*), application_name, state from pg_stat_activity group by 2,3"`
  (the API's rows are `buddy`, Keycloak's `PostgreSQL JDBC Driver`).
- Renewing TLS certs, restarting on reboot, and image updates are all your
  responsibility on a self-hosted VM; `restart: unless-stopped` handles
  process crashes/reboots, but not certificate or OS-level maintenance.
