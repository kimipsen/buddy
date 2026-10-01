---
name: backend-aware-review
description: Review a diff (staged changes by default, or a given commit/branch/PR) for correctness bugs and reuse/simplification/efficiency cleanups. Grounds any .NET/backend files (src/backend/**, *.cs, *.csproj) in the claude-backend skill plus the dotnet-skills plugin skills when installed (inline checklist otherwise); reviews frontend/other files with a general pass. Use for "review the staged changes", "review this diff/PR", "review my backend changes".
---

# Backend-Aware Review

Purpose: review a diff for correctness bugs and reuse/simplification/efficiency cleanups, the same way `code-review` does — but for any `.NET`/backend file in the diff, ground the review in `claude-backend` and, when installed, the `dotnet-skills` plugin, instead of relying on generic judgment.

## 1. Resolve the target diff

- No argument → `git diff --cached` (staged changes). If that's empty, say so explicitly and ask whether to review the last commit, the working-tree diff, or a different target — do not silently substitute one.
- Argument given → a commit SHA, branch name, PR number, or path; resolve it to a diff the same way the `code-review` skill would (e.g. `git diff <base>...<target>`, or `gh pr diff <number>`).

## 2. Split changed files into two lanes

- **Backend/.NET lane**: anything under `src/backend/**`, or matching `*.cs`, `*.csproj`, `*.sln`, `*.slnx`.
- **Everything else**: frontend (`src/frontend/**`), docs, config, infra, etc.

Skip a lane entirely if it has no changed files — don't spawn agents for empty work.

## 3. Backend/.NET lane - grounded review, not generic judgment

Always load the project's `claude-backend` skill first for any backend file - it holds Buddy's conventions (vertical slices, Marten event stores and snapshot projections, Wolverine handlers, `Result<T>`, test layout).

Then check whether any `dotnet-skills:*` skills appear in the available skills list for this session.

- **Present** -> use 3a.
- **Absent** -> say once in the report that the `dotnet-skills` plugin isn't installed and the backend lane used `claude-backend` + the inline checklist, then use 3b. Don't try to load `dotnet-skills:*` names that aren't listed.

Pick checks by what the diff actually touches. This repo uses Marten and WolverineFx, not EF Core or Akka.NET.

### 3a. dotnet-skills routing (plugin installed)

- Any `.cs` change -> `dotnet-skills:csharp-coding-standards` and `dotnet-skills:csharp-nullable-reference-types` as the baseline.
- Query/read-model/persistence code (Marten sessions, LINQ queries, projections) -> `dotnet-skills:database-performance`.
- New/changed types (records, structs, sealed classes) -> `dotnet-skills:csharp-type-design-performance`.
- `async`/`Task`/channels/concurrency-shaped code -> `dotnet-skills:csharp-concurrency-patterns` (and the `dotnet-skills:dotnet-concurrency-specialist` agent if the change is timing/thread-safety sensitive).
- `.csproj`/`Directory.Packages.props`/package version changes -> `dotnet-skills:package-management`.
- Any non-trivial backend logic change -> close with `dotnet-skills:slopwatch`.

Load each applicable skill with `Skill` before judging that file - don't rely on memory of what it says.

### 3b. Inline checklist (plugin not installed)

- **Coding standards / nullable** (any `.cs`): matches `claude-backend` conventions and neighbouring files; no new `!` without a reason the compiler can't see; nullable returns checked before use; `switch` expressions (not statements) over unions so CS8509 catches missing arms; no `Guid.NewGuid()` for domain IDs; no exceptions for expected outcomes.
- **Marten query/projection performance** (event stores, handlers, projections): sessions disposed (`await using`); `QuerySession` for reads, one `LightweightSession` + one `SaveChangesAsync` per write; no stream read/rehydrate inside a loop (N+1) - batch or use a lookup document/snapshot; filters and `Take` applied in the LINQ query, not after `ToListAsync`; read-only handlers use `FindSnapshotAsync`; new event types added to the feature's `EventTypes` and the snapshot projection's `Apply`; snapshot registered `Inline` in schema `snapshots`.
- **Type design**: IDs are top-level `sealed record X(Guid Value)` with a `CreateVersion7` factory; aggregates and events immutable records; collections `Immutable*`/`IReadOnly*`; renamed/removed fields on persisted event records (breaks existing streams and golden files).
- **Async / concurrency**: `CancellationToken` passed through every await; no `.Result`/`.Wait()`/`async void`; no shared mutable static state; create races guarded by a DB constraint (`Insert` + `DocumentAlreadyExistsException`), not an in-memory lock; read-modify-append races considered when the diff adds a new invariant.
- **Package management**: versions only in `src/backend/Directory.Packages.props`, none in `.csproj`; packages within one family (`WolverineFx` + `WolverineFx.*`, `FluentValidation` + `FluentValidation.*`) on the same version; no new package where an existing one covers it.
- **Slop check**: disabled or skipped tests (`Skip =`, commented-out `[Fact]`), deleted assertions, `#pragma warning disable` / `[SuppressMessage]` / `NoWarn` additions, empty `catch` or catch-and-ignore, `TODO`/`HACK` workarounds, a missing `[CoversEndpoint]` on a new endpoint's tests, missing golden-file or snapshot test for a new event/aggregate.

### Known SonarCloud findings

If a SonarCloud/SonarQube report is part of the review, triage it with `.claude/skills/claude-backend/references/sonar-known-issues.md`: don't flag the listed false positives, and treat `S2201` on an event store's `Reverse()` as a real bug.

## 4. Everything-else lane — general review

Review frontend/docs/config changes the way `code-review` would at the equivalent effort level: correctness bugs, and reuse/simplification/efficiency cleanups. No backend routing needed here.

## 5. Effort level

Same convention as `code-review`: low/medium → fewer, high-confidence findings only; high→max → broader coverage, may surface lower-confidence findings too. Default to the level last used in this conversation if the user doesn't specify one; otherwise default to medium.

## 6. Report

Merge findings from both lanes into one list, ranked most-severe first, and report with the `ReportFindings` tool — same shape `code-review` uses (file, summary, failure_scenario, category, short_summary). Don't print findings as plain text when `ReportFindings` is available.
