Claude Backend Skill

This directory contains templates and guidance for backend development with .NET (screaming-architecture), event sourcing, EF Core, and PostgreSQL. Prefer targeting .NET 11 and model domain events as discriminated unions (sealed record hierarchies) rather than marker interfaces.

It lives at `.claude/skills/claude-backend/`, which is where Claude Code discovers project skills. Claude Code loads it automatically when a task matches the `description` in `SKILL.md`'s frontmatter; you can also invoke it explicitly with `/claude-backend`.

Files:
- `SKILL.md` — YAML frontmatter (`name`, `description`) plus high-level rules and output format (Planning Mode, ID-type preference, event sourcing, schemas)
- `manifest.json` — skill metadata and preferences
- `examples/` — example prompts to drive reproducible outputs
- `samples/` — small code templates (value types, aggregate, DbContext, event store notes)

Use these templates as starting points; adapt names and namespaces to your project.
