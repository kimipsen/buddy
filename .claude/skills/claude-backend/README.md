Claude Backend Skill

This directory contains templates and guidance for backend development with .NET (screaming-architecture), event sourcing, EF Core, and PostgreSQL. Prefer targeting .NET 11 and model domain events as native C# discriminated unions (`union` with nested `sealed record` cases) rather than marker interfaces or an abstract base record. Unions are a preview language feature, so the sample sets `<LangVersion>preview</LangVersion>`. For expected, recoverable failures (business-rule violations, validation, not-found), prefer returning `Result`/`Result<T>` (see `samples/Result.cs`) over throwing; reserve exceptions for programmer errors, corrupt data, and infrastructure failures.

It lives at `.claude/skills/claude-backend/`, which is where Claude Code discovers project skills. Claude Code loads it automatically when a task matches the `description` in `SKILL.md`'s frontmatter; you can also invoke it explicitly with `/claude-backend`.

Files:
- `SKILL.md` — YAML frontmatter (`name`, `description`) plus high-level rules and output format (Planning Mode, ID-type preference, event sourcing, schemas)
- `manifest.json` — skill metadata and preferences
- `examples/` — example prompts to drive reproducible outputs
- `samples/` — small code templates (value types, aggregate, DbContext, event store notes)

Project layout:
- Place backend source and artifacts under `src/backend/` (for example `src/backend/Domain`, `src/backend/Infrastructure`, `src/backend/Web`). The `samples/dotnet-sample/` demonstrates the recommended structure under `src/` which can be mapped into `src/backend/` for real projects.

Secrets and configuration:
- Use `.env` files for local development secrets (keep them untracked). Add a `src/backend/.env.example` with placeholders and document the required environment variables. For production, prefer injecting secrets via CI/CD or cloud secret stores and avoid embedding secrets in source code or client-side bundles.

Use these templates as starting points; adapt names and namespaces to your project.
