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
  `postgres/init-keycloak-db.sql`, run once via `docker-entrypoint-initdb.d`
  on an empty data volume).
- Keycloak imports the `buddy` realm — clients `buddy-frontend` (public,
  used by the Angular app) and `buddy-admin-cli` (confidential, used by the
  backend to provision child accounts), plus seeded users `alice`/`bob`/
  `carol` — from the checked-in export at `keycloak/buddy-realm.json`, via
  `start-dev --import-realm`. Import is skipped if the realm already exists,
  so this only takes effect on a fresh `postgres-data` volume.

The checked-in `appsettings.Development.json` client secret for
`buddy-admin-cli` matches the one baked into `keycloak/buddy-realm.json`;
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
  `postgres-data` was initialized before `postgres/init-keycloak-db.sql` was
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
- **A Testcontainers suite cannot start:** confirm that `docker ps` works from
  inside the development container.
