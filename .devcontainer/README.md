# Development container

The development container is the supported local environment for Buddy. It
provides the .NET and Node toolchains, Docker access, PostgreSQL tooling,
Angular CLI, and Go Task. Docker Compose places the workspace and local
services on the same `buddy_network` network used by the application's checked-in
development configuration.

## First-time setup

Before opening the repository in the container, create the local environment
file:

```bash
cp .devcontainer/.env.example .devcontainer/.env
```

The file is git-ignored. Change the placeholder passwords before using the
container on a shared machine.

Open the repository in VS Code and run **Dev Containers: Reopen in Container**.
The Compose stack starts PostgreSQL, Keycloak, and Mailpit. Both first-run
Keycloak prerequisites are automated:

- Postgres creates the separate `keycloak` database Keycloak needs (see
  `postgres/init-keycloak-db.sh`, run once via `docker-entrypoint-initdb.d`
  on an empty data volume).
- Keycloak imports the `buddy` realm — clients `buddy-frontend` (public,
  used by the Angular app) and `buddy-admin-cli` (confidential, used by the
  backend to provision child accounts), plus seeded users `alice`/`bob`/
  `carol` — from the checked-in export at `keycloak/buddy-realm.json`, via
  `start-dev --import-realm`. Import is skipped if the realm already exists,
  so this only takes effect on a fresh `postgres-data` volume.

The `buddy-admin-cli` client secret in your local
`src/backend/buddy/appsettings.Development.json` (git-ignored, like every
`appsettings.*.json`) must match the one baked into `keycloak/buddy-realm.json`;
change both together if you rotate it.

## Running Buddy

Generate and trust a development certificate if HTTPS is not already configured:

```bash
task generer-cert
task cert
```

Start the API:

```bash
cd src/backend/buddy
dotnet run
```

The launch profile listens on:

- `https://localhost:7076` — the URL used by the checked-in frontend runtime config
- `http://localhost:5193` — plain HTTP API access

Start the frontend in another terminal:

```bash
cd src/frontend/buddy
npm install
npm start
```

Open `http://localhost:4300`.

## Local services

| Service | Address | Current role |
| --- | --- | --- |
| Frontend | `http://localhost:4300` | Angular development server |
| Buddy API | `https://localhost:7076` or `http://localhost:5193` | ASP.NET API |
| Keycloak | `http://localhost:9080` | Authentication and child-account provisioning |
| PostgreSQL | `db:5432` inside Compose | Marten event and document storage; Keycloak storage |
| Mailpit | `http://localhost:9025` (`mailpit:1025` SMTP) | Development email capture |

The `localhost:9080`/`9025`/`2025` addresses are the ports Compose forwards to
your **host** browser. From **inside** the devcontainer (terminal, `dotnet run`,
Playwright, scripts), use the Compose hostnames instead: `http://keycloak:8080`,
`http://mailpit:8025` (SMTP `mailpit:1025`) and `db:5432`.

Inside the container, `localhost:9080`/`9025`/`2025` may not be this stack at
all. If anyone has run `docker compose up` from *within* the devcontainer
(docker-in-docker), a second, nested stack (`devcontainer-keycloak-1`,
`devcontainer-db-1`, `devcontainer-mailpit-1` in `docker ps`) publishes those
same ports there. That nested Keycloak has its **own signing keys**, so tokens
it issues get `401 invalid_token "The signature key was not found"` from the
API, and its Mailpit never receives the API's mail. Don't use those ports from
inside the container. Use `keycloak:8080`/`mailpit:8025`. The nested stack is
harmless otherwise. To tell the two apart: `getent hosts keycloak` (the real
one) resolves to a different network than
`docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' devcontainer-keycloak-1`,
and `docker info` reports the in-container daemon. Leave the nested stack
alone, or stop it deliberately with `docker compose -p devcontainer down` once
nobody relies on it.

## Useful commands

Inspect the Compose stack:

```bash
docker compose -f .devcontainer/docker-compose.yml ps
docker compose -f .devcontainer/docker-compose.yml logs keycloak
docker compose -f .devcontainer/docker-compose.yml logs db
```

List repository tasks:

```bash
task --list
```

The `db:marten:*` tasks inspect or clear tables in the `users` schema. The
`db:marten:clear-events` task is destructive and does not reset every feature
schema.

Run tests using the [testing guide](../docs/testing.md).

## Git hooks: AI-assisted documentation sync

A `post-commit` hook can ask an AI agent (Claude, Codex, or GitHub Copilot) to
update `docs/` and `README.md` after each commit. It's opt-in per clone:

```bash
task hooks:install AGENT=claude   # or: codex, copilot
```

See [.devcontainer/git-hooks/README.md](git-hooks/README.md) for how it
works, requirements, and how to skip it for a single commit.

## Troubleshooting

- **Keycloak reports that database `keycloak` does not exist:** this means
  `postgres-data` was initialized before `postgres/init-keycloak-db.sh` was
  added. Run the `CREATE DATABASE keycloak;` statement manually against the
  `db` service (`docker compose -f .devcontainer/docker-compose.yml exec db
  psql -U postgres -d postgres -c 'CREATE DATABASE keycloak;'`) and restart
  Keycloak, or remove the `postgres-data` volume to reinitialize from
  scratch.
- **Login redirects are rejected:** verify the `buddy-frontend` redirect URI,
  web origin, realm name, and client ID against
  `src/frontend/buddy/public/config/runtime-config.json`.
- **Child creation cannot obtain an admin token:** verify the
  `buddy-admin-cli` service account, its realm-management roles, and the secret
  used by the API.
- **The browser rejects the API certificate:** rerun the certificate tasks and
  restart `dotnet run`.
- **`53300: sorry, too many clients already` / psql can't connect:** all nine
  Marten stores share one Npgsql pool (`NpgsqlDataSource`), capped at
  `Maximum Pool Size=50` unless the connection string sets its own (see
  `src/backend/buddy/Common/Postgres/PostgresDataSource.cs`), and Compose
  starts Postgres with `max_connections=200` for headroom (applies after the
  `db` container is recreated). The API's connections show up with
  `application_name` `buddy`. If you still hit it, check who holds
  connections:
  `PGPASSWORD=postgres psql -h db -U postgres -c "select count(*), application_name, state from pg_stat_activity group by 2,3"`.
  Stopping the API (`fuser -k -TERM 5193/tcp 7076/tcp`) releases its pool.
- **Seeded users have piles of `E2eChild…` children / `E2eGroup…` groups:**
  leftovers from e2e runs before per-test cleanup existed (or a killed run). Run
  `node e2e/scripts/cleanup-leftover-e2e-data.mjs` from
  `src/frontend/buddy` with the API running (dry run), then again with
  `--apply`.
- **A Testcontainers suite cannot start:** confirm that `docker ps` works from
  inside the development container.
