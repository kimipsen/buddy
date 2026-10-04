Claude Backend Skill

Conventions for the Buddy .NET 11 backend in `src/backend/`: vertical-slice features under `src/backend/buddy/Features/<Domain>/<UseCase>/`, event sourcing on Marten (per-domain event schemas, inline snapshot projections in a shared `snapshots` schema), WolverineFx handlers, FluentValidation, minimal-API endpoints, the `Result<T>` union, and Alba + Testcontainers integration tests. Domain events are native C# `union`s (preview language feature, `<LangVersion>preview</LangVersion>`). Buddy has no EF Core.

It lives at `agents/copilot/claude-backend/`. Use it when editing or reviewing C# under `src/backend`, from small fixes to handlers, endpoints, validators, domain events, aggregates, IDs, event stores, snapshot projections, or backend integration tests. For a brand-new endpoint/command/query/aggregate/event, also consult `agents/copilot/backend-feature/SKILL.md`; for backend Stryker survivors, `agents/copilot/mutation-fix-backend/SKILL.md`; to review a diff, see `agents/copilot/backend-aware-review/SKILL.md`.

Files:
- `SKILL.md` — conventions plus real example paths in `src/backend/buddy`.
- `manifest.json` — skill metadata.
- `README.md` — package overview and file list.
- `references/sonar-known-issues.md` — SonarCloud false positives and the one real finding (`S2201` on `Reverse()`).
- `references/efcore.md` — EF Core / hand-rolled event-store guidance. Not used in Buddy; for other services.
- `samples/` — generic templates (Order aggregate, union events, Result, hand-rolled Postgres event store, EF Core DbContext, `dotnet-sample/` end-to-end layout). Not Buddy's patterns — for Buddy, copy from `src/backend/buddy/Features/Pickups/`.
- `examples/` — example prompts.

Secrets: never commit them. `appsettings.*.json` and `.env` are git-ignored; production secrets come from CI/CD or a cloud secret store.
