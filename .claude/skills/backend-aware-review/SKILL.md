---
name: backend-aware-review
description: Review a diff (staged changes by default, or a given commit/branch/PR) for correctness bugs and reuse/simplification/efficiency cleanups. Routes any .NET/backend files (src/backend/**, *.cs, *.csproj) through the relevant dotnet-skills plugin skills and agents instead of generic judgment; reviews frontend/other files with a general pass. Use for "review the staged changes", "review this diff/PR", "review my backend changes".
---

# Backend-Aware Review

Purpose: review a diff for correctness bugs and reuse/simplification/efficiency cleanups, the same way `code-review` does — but for any `.NET`/backend file in the diff, ground the review in the `dotnet-skills` plugin instead of relying on generic judgment.

## 1. Resolve the target diff

- No argument → `git diff --cached` (staged changes). If that's empty, say so explicitly and ask whether to review the last commit, the working-tree diff, or a different target — do not silently substitute one.
- Argument given → a commit SHA, branch name, PR number, or path; resolve it to a diff the same way the `code-review` skill would (e.g. `git diff <base>...<target>`, or `gh pr diff <number>`).

## 2. Split changed files into two lanes

- **Backend/.NET lane**: anything under `src/backend/**`, or matching `*.cs`, `*.csproj`, `*.sln`, `*.slnx`.
- **Everything else**: frontend (`src/frontend/**`), docs, config, infra, etc.

Skip a lane entirely if it has no changed files — don't spawn agents for empty work.

## 3. Backend/.NET lane — route through dotnet-skills, don't wing it

For each changed backend file, identify which `dotnet-skills:*` skills actually apply based on what the diff touches, then review with those skills loaded rather than from general C# knowledge. This repo uses Marten (event sourcing) and WolverineFx, not raw EF Core or Akka.NET — pick skills by what's actually in the diff, not by assumption:

- Any `.cs` change → `dotnet-skills:csharp-coding-standards` and `dotnet-skills:csharp-nullable-reference-types` as the baseline.
- Domain models, aggregates, value/ID types, event definitions, projections → also the project's own `claude-backend` skill (screaming architecture, event sourcing, Result-pattern conventions already established in this repo).
- Query/read-model/persistence code (Marten sessions, LINQ queries, projections) → `dotnet-skills:database-performance`.
- New/changed types (records, structs, sealed classes) → `dotnet-skills:csharp-type-design-performance`.
- `async`/`Task`/channels/concurrency-shaped code → `dotnet-skills:csharp-concurrency-patterns` (and `dotnet-skills:dotnet-concurrency-specialist` agent if the change is timing/thread-safety sensitive enough to warrant it).
- `.csproj`/`Directory.Packages.props`/package version changes → `dotnet-skills:package-management`.
- Any non-trivial backend logic change → close the pass with `dotnet-skills:slopwatch` to catch disabled tests, suppressed warnings, empty catch blocks, or other shortcuts the diff might be hiding.

Load each applicable skill with `Skill` before judging that file — don't rely on memory of what the skill says.

### Known SonarCloud false positives in this repo — don't flag or "fix" these

If a SonarCloud/SonarQube report (or its findings) is part of what's being reviewed, these rules are confirmed false positives or architectural noise here, not real defects — see `claude-backend`'s skill doc for the full explanation of each:

- `csharpsquid:S3903`, `csharpsquid:S1186`, `csharpsquid:S3060` on any file declaring a `union` — the analyzer doesn't understand the preview `union` syntax and misparses it.
- `csharpsquid:S8970` (unneeded null-forgiving operator) — SonarCloud's Automatic Analysis often misses `<Nullable>enable</Nullable>` from `Directory.Build.props`; verify nullable is actually enabled before treating this as real.
- `csharpsquid:S107` (too many parameters) on CQRS handlers/minimal-API endpoints — expected given DI-injected dependencies plus `CancellationToken`.
- `csharpsquid:S2094` (empty record) on a no-payload case inside a `union` (e.g. `Result<T>.NotFound`) — intentional marker type.

Conversely, `csharpsquid:S2201` ("use the return value") on an event-store's `events.Reverse()` is worth taking seriously — `IReadOnlyList<T>.Reverse()` is the non-mutating LINQ extension, not `List<T>`'s in-place mutator, and a bare `events.Reverse();` statement silently does nothing. This exact bug shipped once already (`MartenUserEventStore.ReadBackwardAsync`); treat this specific rule as a real correctness check on any event-store code, not noise.

## 4. Everything-else lane — general review

Review frontend/docs/config changes the way `code-review` would at the equivalent effort level: correctness bugs, and reuse/simplification/efficiency cleanups. No dotnet-skills routing needed here.

## 5. Effort level

Same convention as `code-review`: low/medium → fewer, high-confidence findings only; high→max → broader coverage, may surface lower-confidence findings too. Default to the level last used in this conversation if the user doesn't specify one; otherwise default to medium.

## 6. Report

Merge findings from both lanes into one list, ranked most-severe first, and report with the `ReportFindings` tool — same shape `code-review` uses (file, summary, failure_scenario, category, short_summary). Don't print findings as plain text when `ReportFindings` is available.
