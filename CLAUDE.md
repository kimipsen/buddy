# CLAUDE.md

Buddy is a family coordination app for guardians of children with ADHD; see [README.md](README.md).

## Stack

- **Backend** (`src/backend`): .NET 11 preview, vertical slices in `buddy/Features/<Domain>/<UseCase>/`, Marten event sourcing with inline snapshot projections, WolverineFx, FluentValidation, minimal APIs, `Result<T>` union. Integration tests in `buddy.IntegrationTests` (xunit, Alba, Testcontainers; needs Docker).
- **Frontend** (`src/frontend/buddy`): Angular 22, zoneless, standalone, signals, Tailwind 4, Vitest unit specs, Playwright e2e. UI strings are typed English/Danish dictionaries in `src/app/core/i18n`.
- **Services** (devcontainer): Postgres `db:5432`, Keycloak `http://keycloak:8080` (realm `buddy`), Mailpit `http://mailpit:8025`. From inside the container, use these hostnames, not `localhost:9080`/`9025`; those reach a different nested stack.
- **Ports**: API `http://localhost:5193` / `https://localhost:7076`; frontend `http://localhost:4300`.

## Commands (from repo root)

- `task test` (all), `task test:backend`, `task test:frontend`, `task test:e2e`
- One frontend spec: `cd src/frontend/buddy && npx ng test --watch=false --include src/app/path/foo.spec.ts`
- One backend test: `dotnet test src/backend/backend.slnx --filter FullyQualifiedName~<Name>`
- Translation parity: `node .claude/skills/i18n/check-parity.mjs`
- Inspect events: `task db:marten:streams SCHEMA=<schema>` (`SCHEMA` is required)

## Project skills (`.claude/skills/`)

Use the matching skill instead of improvising. Each one holds the verified conventions for its area.

| Area | Skill |
| --- | --- |
| Backend conventions | `claude-backend` |
| New endpoint / use case / aggregate | `backend-feature` |
| Angular components, services, specs | `buddy-frontend` |
| UI strings (en + da) | `i18n` |
| Playwright e2e | `e2e-test` |
| Run / screenshot the app | `run-buddy` |
| Docs screenshots of every page (`task docs:screenshots`) | `doc-screenshots` |
| Full feature from a design doc | `feature-from-analysis` |
| Review a diff | `backend-aware-review` |
| Mutation testing | `mutation-fix` (frontend), `mutation-fix-backend` |
| SonarCloud findings | `sonar-triage` |
| Production deploy (always confirm first) | `deploy` |

`agents/` holds Codex/Copilot packages; Claude Code doesn't load them.

After adding a skill or changing a skill's `description`, check that it still triggers, and doesn't steal prompts from its neighbours, with `skill-creator`'s description eval.

## Conventions

- Every page appears in the documentation screenshots (`docs/screenshots`). A new route or a visible change to a page means updating `src/frontend/buddy/screenshots/pages.ts` (plus demo data in `demo-family.ts`) and running `task docs:screenshots`. `src/app/screenshot-coverage.spec.ts` enforces the route list. See the `doc-screenshots` skill.
- Plan before non-trivial changes. Design docs live in `docs/backend/analysis/` and `docs/frontend/analysis/`.
- Change tests rather than production code when hardening specs; ask before changing production code for a bug a test uncovers.
- Never commit secrets. `appsettings.*.json` and `.env` are git-ignored.
- `task hooks:install` adds a pre-commit hook (Prettier/ESLint, i18n parity and C# whitespace on staged files; fix what it reports rather than using `--no-verify`) and a post-commit hook that may add a `docs: sync documentation (auto)` commit. Add `[skip-docs]` to the commit message to skip the latter.
- Stop dev servers by port (`fuser -k -TERM 4300/tcp 5193/tcp 7076/tcp`), not with `pkill -f`, which can match your own shell.
