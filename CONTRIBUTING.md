# Contributing to Buddy

Thanks for helping. Buddy is maintained by one person, so small, focused pull requests get
reviewed fastest. For a bigger change, open an issue first so we can agree on the approach.

By taking part you agree to follow the [code of conduct](CODE_OF_CONDUCT.md). Report security
problems privately as described in [SECURITY.md](SECURITY.md), never in a public issue.

## Set up

The supported environment is the VS Code development container. It has .NET, Node, the Angular
CLI and Docker, and runs Postgres, Keycloak and Mailpit next to the app.

1. `cp .devcontainer/.env.example .devcontainer/.env`
2. Open the repository in VS Code and run **Dev Containers: Reopen in Container**. On a first run,
   finish the Postgres and Keycloak setup in the
   [development container guide](.devcontainer/README.md).
3. Start the API (`cd src/backend/buddy && dotnet run`) and, in another terminal, the frontend
   (`cd src/frontend/buddy && npm install && npm start`). The app runs at
   <http://localhost:4300>.

The [README](README.md#getting-started) has more detail, and seeded test users are described in
the [development container guide](.devcontainer/README.md).

## Install the git hooks

```bash
task hooks:install
```

The pre-commit hook runs Prettier and ESLint, the i18n parity check and C# whitespace formatting
on staged files. Fix what it reports rather than committing with `--no-verify`. A post-commit hook
may add a `docs: sync documentation (auto)` commit; put `[skip-docs]` in your commit message to
skip it.

## Run the tests

| Command | What it runs |
| --- | --- |
| `task test` | Backend and frontend unit/integration tests |
| `task test:backend` | xunit + Alba integration tests (needs Docker for Testcontainers) |
| `task test:frontend` | Vitest specs |
| `task test:e2e` | Playwright against the real API, Keycloak and Postgres |

Run `task test` before opening a pull request. The [testing guide](docs/testing.md) covers
running a single test and reading failures.

## How the code is organised

- **Backend** (`src/backend`): vertical slices in `buddy/Features/<Domain>/<UseCase>/`, event
  sourced with Marten. Never rename or remove fields on persisted events; add golden files for new
  events. See [docs/backend](docs/backend).
- **Frontend** (`src/frontend/buddy`): Angular, standalone components and signals, Tailwind with a
  dark-mode pair for every colour. See [docs/frontend/README.md](docs/frontend/README.md).
- **Design docs**: plan non-trivial features in `docs/backend/analysis/` or
  `docs/frontend/analysis/` before writing code.

## UI text in English and Danish

Every user-facing string lives in the typed dictionaries in
`src/frontend/buddy/src/app/core/i18n/translations/`, in **both** `en` and `da`. Don't hardcode
text in templates, including `aria-label`s and screen-reader-only labels. The build fails when a
key is missing in either language; also run:

```bash
node .claude/skills/i18n/check-parity.mjs
```

If you don't speak Danish, write your best attempt and say so in the pull request.

## Screenshots

Every page appears in [docs/screenshots](docs/screenshots). A new route or a visible change to a
page means updating `src/frontend/buddy/screenshots/pages.ts` (and its demo data) and running
`task docs:screenshots`.

## Commits and pull requests

- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org):
  `feat(calendar): add weekday filtering for recurrence rules`, `fix: ...`, `docs: ...`.
- Keep one topic per pull request, and fill in the
  [pull request template](.github/pull_request_template.md): what and why, how it was tested, and
  the checklist.
- Never commit secrets. `appsettings.*.json` and `.env` files are git-ignored for that reason.
- Don't put personal data in logs; see [docs/backend/observability.md](docs/backend/observability.md).

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
