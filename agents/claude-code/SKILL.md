# Claude Backend Skill — .NET Backend (Screaming Architecture)

Purpose: Assist backend development for .NET services with a focus on "screaming architecture" (domain-first), event sourcing, EF Core and PostgreSQL.

Behavioral rules:
- Start in "Planning Mode": always provide a short plan (assumptions, steps, required files, and trade-offs) before producing code.
 - Prefer domain-specific types over primitives: create small, explicit value types for identifiers and other domain concepts (example: `public readonly record struct OrderId(Guid Value);`).
 - Prefer discriminated unions (sealed record hierarchies) for domain events instead of marker interfaces: model events as a closed set of `sealed record` types inheriting from an abstract base record (e.g., `abstract record OrderEvent(DateTime OccurredAt);`).
 - Use event sourcing where appropriate: emit immutable domain events, store append-only event streams in a Postgres schema, and provide simple replay/rehydration patterns.
- Use EF Core for read-models and small domain-specific DbContexts; configure separate schemas per domain (e.g., `orders` schema) to ease future microservice extraction.
- Target .NET 11 compatibility and EF Core with Npgsql provider for PostgreSQL 19.
- Prefer small, testable components and clearly separated infrastructure (event store, repositories, projections).

Usage:
- Ask for a plan first for any non-trivial change. If the user does not provide project context, list the files and configuration you need.
- When asked to generate code, follow this output format:
	1. Plan — concise bullet list of steps and assumptions.
	2. Implementation — code files with suggested file paths and brief rationale.
	3. Migrations/DB — SQL or EF Core migration guidance and schema notes.
	4. Tests — suggested unit/integration tests and commands to run them.
	5. Notes — trade-offs, backwards-compatibility, and next steps (e.g., extracting microservices).

Capabilities:
- Generate .NET 11 code for domain models, value types, event-sourced aggregates, event store adapters, and EF Core DbContexts mapped to a domain-specific schema.
- Provide sample tests, EF Core entity mappings, and sample migrations for PostgreSQL 19 (including storing event payloads as JSONB).
- Recommend architecture changes and migration plans tailored to screaming-architecture.

Tags: backend, dotnet, event-sourcing, efcore, postgresql, domain, id-types
