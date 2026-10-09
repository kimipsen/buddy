Copilot Documentation Skill

This folder contains templates and guidance for documentation tasks performed by the Copilot agent.

Files:
- `SKILL.md` — behavior, output format, and responsibilities
- `manifest.json` — metadata for the skill
- `examples/` — example prompts for common documentation tasks
- `templates/` — Markdown templates for frontend and backend docs, ADRs, changelogs, and TOC entries

Use these templates as a starting point; ask the agent to adapt templates to your repo structure.

## Ported Buddy skills

The following sibling packages are Copilot-format ports of the Buddy project
skills that live canonically at `.claude/skills/<name>/` (see that
directory's own docs for the Claude Code originals). Each one follows the
same `SKILL.md` + `manifest.json` + `README.md` convention as this package,
with any supporting scripts/templates/references copied alongside and
internal paths updated to point at `agents/copilot/<name>/`. Keep both
copies aligned when a convention changes.

- [`backend-feature/`](backend-feature/README.md) — add a vertical slice
  (command/query, validator, handler, endpoint, tests) to the .NET backend.
- [`claude-backend/`](claude-backend/README.md) — Buddy .NET backend
  conventions (vertical slices, Marten event sourcing, WolverineFx, Result<T>).
- [`backend-aware-review/`](backend-aware-review/README.md) — review a diff,
  grounding backend/.NET files in the backend conventions.
- [`buddy-frontend/`](buddy-frontend/README.md) — Angular 22 frontend
  conventions (zoneless, signals, Tailwind, Vitest).
- [`i18n/`](i18n/README.md) — add/check English/Danish UI strings and
  parity.
- [`e2e-test/`](e2e-test/README.md) — write, run, and debug Playwright e2e
  specs.
- [`doc-screenshots/`](doc-screenshots/README.md) — regenerate the
  documentation screenshot set.
- [`run-buddy/`](run-buddy/README.md) — run/demo the stack locally and take
  screenshots.
- [`feature-from-analysis/`](feature-from-analysis/README.md) — build a full
  feature end to end from a design doc, or draft one.
- [`mutation-fix/`](mutation-fix/README.md) — triage and fix frontend
  (StrykerJS) mutation-testing survivors.
- [`mutation-fix-backend/`](mutation-fix-backend/README.md) — triage and fix
  backend (Stryker.NET) mutation-testing survivors.
- [`sonar-triage/`](sonar-triage/README.md) — triage SonarCloud/SonarQube
  findings.
- [`deploy/`](deploy/README.md) — deploy to the Oracle VM or Azure Container
  Apps, with pre-flight checks and verification.
- [`rebase-commit/`](rebase-commit/README.md) — commit finished work and land
  it on master with rebase + fast-forward (no merge commits, no push).
