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
The Compose stack starts PostgreSQL, Keycloak, RabbitMQ, Redis, and Mailpit, but
two first-run Keycloak prerequisites are currently manual.

### Create the Keycloak database

The PostgreSQL image creates `POSTGRES_DB`, but Keycloak is configured to use a
separate database named `keycloak`. Create it after the database container is
running:

```bash
docker compose -f .devcontainer/docker-compose.yml exec db \
  psql -U postgres -d postgres -c 'CREATE DATABASE keycloak;'
```

If `.devcontainer/.env` uses different PostgreSQL names, substitute its
`POSTGRES_USER` and `POSTGRES_DB` values. A `database "keycloak" already exists`
error means this step was completed previously. Restart Keycloak afterward:

```bash
docker compose -f .devcontainer/docker-compose.yml restart keycloak
```

### Configure the Buddy realm

Open the Keycloak admin console at `http://localhost:9080` and sign in with
`KEYCLOAK_ADMIN` and `KEYCLOAK_ADMIN_PASSWORD` from `.devcontainer/.env`.
Create a realm named `buddy` with these clients:

- `buddy-frontend`: public OpenID Connect client for the Angular app. Allow
  `http://localhost:4200/*` as a valid redirect URI and
  `http://localhost:4200` as a web origin.
- `buddy-admin-cli`: confidential service-account client used by the backend to
  provision child accounts. Enable service accounts, grant the service account
  the realm-management permissions needed to create users and assign realm
  roles, and place its generated secret in
  `Authentication:KeycloakAdmin:ClientSecret` through user secrets or a local
  configuration override.

The checked-in `appsettings.Development.json` contains a development client
secret, but it only works when it matches the client configured in the local
realm. The repository's only realm export is an integration-test fixture; it is
owned by Testcontainers and is not a supported local-development bootstrap.

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

Open `http://localhost:4200`.

## Local services

| Service | Address | Current role |
| --- | --- | --- |
| Frontend | `http://localhost:4200` | Angular development server |
| Buddy API | `https://localhost:7076` or `http://localhost:5193` | ASP.NET API |
| Keycloak | `http://localhost:9080` | Authentication and child-account provisioning |
| PostgreSQL | `db:5432` inside Compose | Marten event and document storage; Keycloak storage |
| Mailpit | `http://localhost:9025` (`mailpit:1025` SMTP) | Development email capture |
| RabbitMQ | `http://localhost:15672` | Provisioned for development; not currently registered by the app |
| Redis | `redis:6379` | Provisioned for development; not currently registered by the app |

RabbitMQ and Redis are available for future work, but the current application
does not depend on either service. Do not document them as production
requirements unless application registration is added.

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

- **Keycloak reports that database `keycloak` does not exist:** complete the
  database creation step and restart Keycloak.
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
