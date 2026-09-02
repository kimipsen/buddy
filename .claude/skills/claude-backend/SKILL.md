---
name: claude-backend
description: .NET backend development with screaming architecture (domain-first), event sourcing, EF Core and PostgreSQL. Use when creating or changing domain models, value/ID types, aggregates, domain events, event stores, DbContexts, projections, or EF Core migrations in a .NET service.
---

# Claude Backend Skill — .NET Backend (Screaming Architecture)

Purpose: Assist backend development for .NET services with a focus on "screaming architecture" (domain-first), event sourcing, EF Core and PostgreSQL.

Project layout requirement: place backend source code and related artifacts under `src/backend/` in the repository (for example `src/backend/Domain`, `src/backend/Infrastructure`, `src/backend/Web`).

Behavioral rules:
- Start in "Planning Mode": always provide a short plan (assumptions, steps, required files, and trade-offs) before producing code.
- Prefer domain-specific types over primitives: create small, explicit value types for identifiers and other domain concepts (example: `public readonly record struct OrderId(Guid Value);`). When generating identifiers (GUIDs) in .NET, prefer UUIDv7 (time-ordered) for better indexing and ordering semantics — use `Guid.CreateVersion7()` rather than `Guid.NewGuid()`.
- Prefer native C# discriminated unions for domain events over marker interfaces or an abstract base record: declare a `union` whose cases are nested `sealed record` types (e.g., `public union OrderEvent(OrderEvent.OrderCreated, OrderEvent.ItemAdded, OrderEvent.OrderCompleted)`). Unlike an abstract base — which any assembly can derive from — a union is genuinely closed, and `switch` expressions over it are checked for exhaustiveness, so a new case is reported at every site that has not handled it. Requires .NET 11 with `<LangVersion>preview</LangVersion>`; also set `<WarningsAsErrors>$(WarningsAsErrors);CS8509</WarningsAsErrors>` so a non-exhaustive switch fails the build instead of merely warning. Note that only switch *expressions* are checked — a switch *statement* is not.
- **SonarCloud/SonarQube false positives from `union`**: as of this preview SDK, SonarCloud's C# analyzer does not understand the `union` declaration syntax and misparses it. On every file that declares a `union`, expect false-positive `csharpsquid:S3903` ("types should be defined in named namespaces" — the parser loses namespace scope after the unrecognized syntax, even though the types are correctly namespaced), `csharpsquid:S1186` ("empty methods" — it reads `public union Foo(A, B, C)` as an empty method declaration), and `csharpsquid:S3060` ("offload this type test to a subclass" — flagging the union's `this switch { A => ..., B => ... }` discriminator, which *is* the correct, idiomatic pattern here; "fixing" it would mean tearing out the union architecture). Verify by reading the flagged file yourself before acting on any of these three rules — don't restructure working union code to silence them. The right fix is marking them Won't Fix/False Positive in SonarCloud, not code changes.
- **SonarCloud nullable false positives**: SonarCloud's Automatic Analysis for C# does not run a real `dotnet build`, so it can miss `<Nullable>enable</Nullable>` set via `Directory.Build.props` and flag every null-forgiving `!` as unnecessary (`csharpsquid:S8970`). Before touching a flagged `!`, confirm nullable actually is enabled project-wide — if so, this is almost always a scanner false positive, not a real issue.
- **CQRS parameter-count noise**: handler `Handle` methods and minimal-API endpoint lambdas commonly take 8+ parameters once you count the command/query plus every injected store/service plus `CancellationToken`. SonarCloud's `csharpsquid:S107` ("too many parameters", threshold 7) will flag most of them. That's an architectural consequence of this pattern, not a bug — don't break up a handler's signature just to satisfy the threshold; raise the quality-profile threshold or mark these Won't Fix instead.
- Consequences of unions for event sourcing, which the samples show: put data common to every case (e.g. `OccurredAt`) in an exhaustive projection on the union rather than a shared base record; and because a union is a value type whose boxed `GetType().Name` is always the union's own name, expose explicit `EventType` and `Payload` members for the persistence discriminator and JSON body, and hand the event store the payload rather than the union value.
- Use event sourcing where appropriate: emit immutable domain events, store append-only event streams in a Postgres schema, and provide simple replay/rehydration patterns.
- **`Reverse()` gotcha on read-backward queries**: a paginated event-store read that fetches descending-by-version and then needs to hand callers ascending order back must check the actual declared return type before calling `.Reverse()`. If the query method returns `IReadOnlyList<T>` (typical for a store method backed by an interface contract rather than a concrete `List<T>`), `.Reverse()` resolves to the non-mutating `Enumerable.Reverse()` LINQ extension — not `List<T>`'s in-place mutator — so its result must be used (`return [.. events.Reverse().Select(...)]`), never called as a bare statement (`events.Reverse();` silently no-ops and the caller gets events in the wrong order). SonarCloud's `csharpsquid:S2201` ("use the return value") catches this — treat it as a real bug report for event-store code, not noise.
- Prefer the Result pattern over throwing exceptions for expected, recoverable outcomes: domain/business-rule violations, validation failures, and not-found cases. A method that can fail this way should return `Result` or `Result<T>` (see `samples/Result.cs`) rather than `throw`, so callers handle the failure as a value instead of via `try/catch`. Reserve exceptions for truly exceptional, unrecoverable conditions the caller cannot be expected to handle in-line: programmer errors, corrupt/unmapped data (e.g. `OrderEvent.FromPayload` on an unknown payload type), and infrastructure failures (DB connection loss, etc.) — those should still throw. At the API boundary, map a failed `Result` to the appropriate HTTP status (400/404/409) instead of relying on exception-handling middleware for expected failures.
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

Secrets handling:
- Store secrets and sensitive configuration in a `.env` file for local development (for example `src/backend/.env`). **Do not commit** the `.env` file to source control; include a tracked `src/backend/.env.example` with placeholder values. Generated code should read secrets from environment variables (e.g., `Environment.GetEnvironmentVariable("MY_CONN")`) or via the configuration providers (`Configuration["My:Conn"]`). In documentation, call out secure deployment practices: inject secrets via CI/CD, cloud secret stores (Key Vault, Secrets Manager), or platform-managed app settings.

Capabilities:
- Generate .NET 11 code for domain models, value types, event-sourced aggregates, event store adapters, and EF Core DbContexts mapped to a domain-specific schema.
- Provide sample tests, EF Core entity mappings, and sample migrations for PostgreSQL 19 (including storing event payloads as JSONB).
- Recommend architecture changes and migration plans tailored to screaming-architecture.

Reference material bundled with this skill (read on demand, don't preload):
- `samples/OrderId.cs`, `samples/OrderEvents.cs`, `samples/OrderAggregate.cs` — value-type IDs, event discriminated unions, aggregate rehydration, and Result-returning business-rule methods.
- `samples/Result.cs` — minimal `Result` / `Result<T>` types for expected-failure returns; note on using an error union for richer typed errors.
- `samples/IEventStore.cs`, `samples/PostgresEventStore.cs`, `samples/OrderDbContext.cs` — event store contract, Postgres adapter, DbContext with per-domain schema.
- `samples/efcore-mapping.md` — EF Core entity mappings and JSONB payload notes.
- `samples/dotnet-sample/` — end-to-end src/ layout (Domain, Infrastructure, Web) with DI event-type mapper, design-time DbContext factory, docker-compose and `MIGRATIONS-README.md`.
- `examples/example.txt` — example prompts that drive reproducible outputs.

Tags: backend, dotnet, event-sourcing, efcore, postgresql, domain, id-types
