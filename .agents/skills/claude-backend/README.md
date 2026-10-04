Claude Backend Skill

Conventions for the Buddy .NET 11 backend in `src/backend/`: vertical-slice features under `src/backend/buddy/Features/<Domain>/<UseCase>/`, event sourcing on Marten (per-domain event schemas, inline snapshot projections in a shared `snapshots` schema), WolverineFx handlers, FluentValidation, minimal-API endpoints, the `Result<T>` union, and Alba + Testcontainers integration tests. Domain events are native C# `union`s (preview language feature, `<LangVersion>preview</LangVersion>`). Buddy has no EF Core.

It lives at `.agents/skills/claude-backend/`, where Codex discovers project skills. It loads when a task matches the `description` in `SKILL.md`'s frontmatter.

Files:
- `SKILL.md` - frontmatter plus the conventions, each with a real example path in `src/backend/buddy`.
- `manifest.json` - skill metadata.
- `references/sonar-known-issues.md` - SonarCloud false positives and the one real finding (`S2201` on `Reverse()`).
- `references/efcore.md` - EF Core / hand-rolled event store guidance. Not used in Buddy; for other services.
- `samples/` - generic templates (Order aggregate, union events, Result, hand-rolled Postgres event store, EF Core DbContext, `dotnet-sample/` end-to-end layout). Not Buddy's patterns - for Buddy, copy from `src/backend/buddy/Features/Pickups/`.
- `examples/` - example prompts.

Secrets: never commit them. `appsettings.*.json` and `.env` are git-ignored; production secrets come from CI/CD or a cloud secret store.
