---
name: run-buddy
description: Run, start or launch the Buddy app locally in the devcontainer - the .NET API (src/backend/buddy, https://localhost:7076 / http://localhost:5193) and the Angular frontend (src/frontend/buddy, http://localhost:4300) - after health-checking Postgres, Keycloak and Mailpit; log in as a seeded user (alice/bob/carol) and screenshot a page with Playwright; inspect Marten events/streams with the db:marten:* tasks; read emails in Mailpit; stop everything again. Use for "run the app", "start the API/backend", "start the frontend", "launch Buddy", "screenshot the dashboard", "confirm the change works in the real app", "check the events that got written to the X schema", "show the Marten streams", "did the invite email go out", "stop the servers", "free the dev ports". Load it even for a quick stop or event lookup: it has the safe port-based stop and the required SCHEMA argument. Writing or running Playwright specs is e2e-test; production is deploy.
---

# Run Buddy

Goal: get the real stack up, drive it in a browser, see the change working, then leave the machine as you found it. Paths are relative to the repo root (`/workspaces/kim`) unless a step says otherwise. Logs go in the scratchpad (`$S` below = your scratchpad directory).

## 0. Hostnames: use the Compose names, not localhost

From inside the devcontainer, reach the sibling services **by Compose hostname**:

| Service | From the devcontainer (use this) | From the host browser (forwarded) |
| --- | --- | --- |
| Postgres | `db:5432` (user/pass/db `postgres`) | - |
| Keycloak | `http://keycloak:8080` | `http://localhost:9080` |
| Mailpit UI/API | `http://mailpit:8025` | `http://localhost:9025` |
| Mailpit SMTP | `mailpit:1025` | `localhost:2025` |
| API | `https://localhost:7076`, `http://localhost:5193` | same (forwarded) |
| Frontend | `http://localhost:4300` | same (forwarded) |

**Don't trust `localhost:9080` / `localhost:9025` inside the container.** If anyone ran `docker compose up` inside the devcontainer's docker-in-docker, a *second* Keycloak/Mailpit stack answers there (check with `docker ps`). That Keycloak has different signing keys, so its tokens get `401 invalid_token "The signature key was not found"` from the API, and its Mailpit never sees the API's mail. The checked-in `src/frontend/buddy/public/config/runtime-config.json` uses `http://localhost:9080` (right for a host browser, wrong for a browser launched in the container); the screenshot helper (step 4) and `e2e/support/global-setup.ts` both swap it to `http://keycloak:8080`.

## 1. Health-check the backing services

```bash
pg_isready -h db -p 5432
curl -s -o /dev/null -w "keycloak %{http_code}\n" http://keycloak:8080/realms/buddy
curl -s -o /dev/null -w "mailpit %{http_code}\n" http://mailpit:8025/api/v1/info
```

All must be `accepting connections` / `200`. If not: `docker compose -f .devcontainer/docker-compose.yml ps` and `... logs keycloak|db|mailpit` (the Compose stack is started by the devcontainer itself; see `.devcontainer/README.md` Troubleshooting). Keycloak can take ~30s after a container restart.

If `psql` says `FATAL: sorry, too many clients already` (`53300` in the API log): all nine Marten stores share one Npgsql pool (`src/backend/buddy/Common/Postgres/PostgresDataSource.cs`), capped at `Maximum Pool Size=50` unless the connection string sets its own, so a running API never holds more than 50 connections (a 2100-request burst at 128 parallel peaked at exactly 50). Before, each store had its own pool defaulting to 100 and one dashboard load took all 100. Check who holds connections with `PGPASSWORD=postgres psql -h db -U postgres -c "select count(*), application_name, state from pg_stat_activity group by 2,3"`. The API's rows have `application_name` `buddy` (unless the connection string sets `Application Name`), and Keycloak's are `PostgreSQL JDBC Driver`. Stopping the API (step 7) releases its pool. If you see it again, several API/test hosts running at once, a connection string with a large explicit `Maximum Pool Size`, or something else holding connections is the likely cause.

## 2. Check ports and stop stale processes

```bash
ss -ltnp | grep -E ':(4300|5193|7076)\b'
```

- Listener is a process you started earlier this session and you want to keep it: reuse it, skip step 3 for it.
- Stale or unknown: `fuser -k -TERM 4300/tcp 5193/tcp 7076/tcp`, then re-run `ss` until empty. The API process shows up as `buddy`, the frontend as `ng serve (buddy`.
- **Don't** use `pkill -f "dotnet run"` / `pkill -f "ng serve"`: the pattern matches your own Bash tool's shell command line and kills it. Kill by port (`fuser`) or by PID.

## 3. Start the API and the frontend (background)

Run each with `run_in_background: true`, output to a log:

```bash
cd /workspaces/kim/src/backend/buddy && dotnet run > $S/api.log 2>&1
cd /workspaces/kim/src/frontend/buddy && npm start > $S/web.log 2>&1
```

- `dotnet run` uses the `https` launch profile (`Properties/launchSettings.json`): `https://localhost:7076;http://localhost:5193`, `ASPNETCORE_ENVIRONMENT=Development`, which loads `appsettings.Development.json` (Postgres at `db`, Keycloak admin at `keycloak:8080`, CORS for 4300). Mail goes to `mailpit:1025` (`appsettings.json` + devcontainer env).
- `npm start` = `ng serve`, port 4300 from `angular.json`. It watches files, so frontend edits hot-reload; backend edits need an API restart.
- First `npm start` needs `npm install` if `node_modules` is missing.

Wait until both are ready (no polling loop longer than needed):

```bash
for i in $(seq 1 40); do
  a=$(curl -sk -o /dev/null -w "%{http_code}" https://localhost:7076/health)
  w=$(curl -s  -o /dev/null -w "%{http_code}" http://localhost:4300)
  [ "$a" = 200 ] && [ "$w" = 200 ] && break; sleep 3
done; echo "api=$a web=$w"
```

Other API probes: `https://localhost:7076/openapi/v1.json` (200 in Development; the readiness URL Playwright uses), `http://localhost:5193/health`, `https://localhost:7076/users/me` (401 without a token = auth is wired). If it doesn't come up, read `$S/api.log` / `$S/web.log`: build errors, `address already in use` (step 2), or a Postgres/Keycloak error (step 1). Cert errors: `task generer-cert && task cert`, then restart the API (`curl -k` and Playwright's `ignoreHTTPSErrors` don't care either way).

## 4. Log in and screenshot a page

If a seeded guardian shows dozens of `E2eChild…` children or `E2eGroup…`/`E2eCalendar…` entries, they're leftovers from e2e runs before per-test cleanup existed (or from a killed run). They make the per-item fan-outs on `/guardian` and `/guardian/admin` slow. From `src/frontend/buddy`, with the API running: `node e2e/scripts/cleanup-leftover-e2e-data.mjs` (dry run), then `--apply`. It only touches names in the exact shape the e2e helpers generate (`E2e…Child<suffix>` / `Testson`, owned `E2eGroup[A|B]<suffix>`, `E2eCalendar<suffix>`, `e2echild*` Keycloak users).

Seeded users come from `.devcontainer/keycloak/buddy-realm.json` (realm `buddy`, public client `buddy-frontend` with direct grants enabled); passwords are mirrored in `SEEDED_USERS` in `src/frontend/buddy/e2e/support/seeded-users.ts` (re-exported by `auth-fixture.ts`). alice, bob and carol are all **guardians** (`<name>@buddy.test`, email verified). There's no seeded child login and no self-registration. Never change their passwords or emails; other specs depend on them.

Use the bundled helper, run from `src/frontend/buddy` (it resolves Playwright from there; Chromium is already installed if `npx playwright test` has ever run, otherwise `npx playwright install --with-deps chromium`):

```bash
cd /workspaces/kim/src/frontend/buddy
node ../../../.claude/skills/run-buddy/screenshot.mjs --path /guardian --user alice --out $S/guardian.png
node ../../../.claude/skills/run-buddy/screenshot.mjs --path /guardian/admin --user bob --full-page --wait-for "Your profile" --out $S/admin.png
node ../../../.claude/skills/run-buddy/screenshot.mjs --form --path /guardian/calendar --user carol --out $S/cal.png   # real Keycloak login form
node ../../../.claude/skills/run-buddy/screenshot.mjs --anon --path /login --out $S/login.png
```

- Default mode mints a token via direct grant against `http://keycloak:8080` and seeds it into `sessionStorage['buddy_keycloak_tokens']`, the same as the e2e `loginAs` fixture. `--form` drives `/login` → "Sign in with Keycloak" → Keycloak's hosted form, then waits for the app to land on `/guardian` or `/child` before navigating.
- It prints `[http 4xx/5xx]` responses and browser console errors to stderr. Read them: a 401 means the token's issuer/keys don't match (see step 0), and a 500 means check `$S/api.log`.
- Then **look at the PNG** (Read tool) and check the change is actually visible. Main routes: `/guardian`, `/guardian/admin`, `/guardian/calendar`, `/guardian/medicine`, `/guardian/pickup`, `/guardian/mealplan`, `/child`.
- For anything more interactive than one screenshot (clicking through a flow), write a throwaway Playwright spec in the scratchpad or follow the `e2e-test` skill instead of growing this script.

## 5. Inspect Marten events

Each feature has its own Postgres schema. `SCHEMA` is **required** (the task descriptions say "default users", but there is no default; omitting it fails). Allowed values: `users groups tasklibrary medicines calendars progress mealplans pickups`.

```bash
task db:marten:events SCHEMA=calendars        # latest 50 events: seq_id, stream_id, version, type, timestamp
task db:marten:streams SCHEMA=calendars       # latest 50 streams
task db:marten:event-types SCHEMA=users       # event counts per type
task db:marten:tables SCHEMA=groups           # mt_* tables in the schema
```

Need the payload? Query it directly:

```bash
PGPASSWORD=postgres psql -h db -U postgres -d postgres -c "select seq_id, type, data from calendars.mt_events order by seq_id desc limit 5;"
```

Snapshot/inline projections live in the shared `snapshots` schema. **Never run `task db:marten:clear-events`** unless the user asks: it truncates every `mt_*` table in the schema, is not reversible and doesn't reset other schemas.

## 6. Email flows (Mailpit)

The API sends invite/verification mail to `mailpit:1025`. Read it from the container:

```bash
curl -s "http://mailpit:8025/api/v1/search?query=to:bob@buddy.test" | jq '.messages[] | {ID, Subject, Created}'
curl -s "http://mailpit:8025/api/v1/message/<ID>" | jq -r .Text      # plain body; links are FrontendBaseUrl (http://localhost:4300)/invite/<token> or /guardian-invite/<token>
```

The UI is `http://localhost:9025` for the user's host browser (or screenshot `http://mailpit:8025` from the container). Search by a unique string from your test (child/group name), not "newest message": other runs mail the same seeded addresses. Bob's backend email can differ from `bob@buddy.test` after e2e runs; `GET /users/me` gives the current one.

## 7. Stop everything

Stop only what you started:

```bash
fuser -k -TERM 4300/tcp 5193/tcp 7076/tcp
ss -ltnp | grep -E ':(4300|5193|7076)\b' || echo "ports clear"
```

(Or `TaskStop` the background tasks.) Leave the Compose services (db/keycloak/mailpit) running: they belong to the devcontainer. Make sure `git status` shows no changes to `src/frontend/buddy/public/config/runtime-config.json`. If a `runtime-config.json.e2e-backup` exists, an interrupted e2e run left it behind: restore with `mv public/config/runtime-config.json.e2e-backup public/config/runtime-config.json`.

## 8. Report

Say what you ran, the URLs you checked, which screenshots you looked at and what they showed (attach the paths), and any 4xx/5xx or log errors, flagged as either caused by the change or pre-existing/environmental.
