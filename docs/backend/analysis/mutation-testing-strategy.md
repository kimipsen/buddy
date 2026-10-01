# Mutation testing strategy

The integration test suite ([integration-testing-strategy.md](integration-testing-strategy.md))
proves that endpoints are covered, but coverage alone doesn't prove the assertions are
meaningful — a test that calls an endpoint and only checks the status code will show as "covered"
while missing a broken response body. Mutation testing closes that gap: Stryker.NET rewrites
small pieces of the production code (a `==` to `!=`, a boundary `<` to `<=`, a string literal to
`""`) one at a time and reruns the test suite; a mutant that still passes ("survived") marks a
spot the tests don't actually pin down. Status: implemented and working again as of 2026-10-01,
on Stryker.NET 5.0.0 with two workarounds for the .NET 11 preview SDK, both wrapped in
[`buddy.IntegrationTests/run-stryker.sh`](../../../src/backend/buddy.IntegrationTests/run-stryker.sh).
See "Running on the net11.0 preview SDK" below for what broke and why, and "Verification status"
for the evidence.

## Why Stryker.NET

The de-facto mutation testing tool for .NET — actively maintained, understands Roslyn syntax
trees directly (no IL-rewriting fragility), and ships a `dotnet-stryker` local tool so no global
install is required. No real alternative exists in the .NET ecosystem worth considering instead.

## Why it runs from `buddy.IntegrationTests`, not `buddy`

Stryker.NET mutates a "project under test" (`buddy.csproj`) using a test project's own test run to
judge each mutant. `buddy` has no separate unit test project — `buddy.IntegrationTests` is the only
test project in the solution, so it's both the coverage source and the natural place to run
Stryker from. This does mean mutation runs pay the same cost the integration suite already pays
(Postgres/Keycloak/mailpit via Testcontainers), not a fast in-memory unit-test cost — see
"Performance characteristics" below.

## Configuration (`buddy.IntegrationTests/stryker-config.json`)

- `solution: "../backend.slnx"` — Stryker resolves the project under test and the
  `buddy.IntegrationTests` test project from the solution instead of the explicit `project` /
  `test-projects` pins used previously (both are now `null`/empty). The path is relative to the
  directory Stryker runs from (`buddy.IntegrationTests`). It used to say `backend.slnx`, which
  made every unscoped run fail at once with `Given path does not exist: backend.slnx`.
- `language-version: "Preview"` — must match the project's `<LangVersion>preview</LangVersion>`
  (`Directory.Build.props`). With `latest`, Stryker parses the code as C# 14, can't see the
  `union` `Result<T>`, and its compile-error rollback crashes (`ArgumentOutOfRangeException` in
  `CSharpRollbackProcess`).
- `concurrency: 8` — raised from the previous `1`. `BuddyApiFixture` starts one
  Postgres/Keycloak/mailpit trio and shares it across the whole test run *within one process*
  (see the integration test doc); Stryker's concurrency setting spawns that many independent test
  host processes in parallel, each starting its own trio of containers. Adjust based on the
  Docker headroom available in the environment running mutation testing.
- `mutation-level: "Standard"`, `coverage-analysis: "perTest"` — Stryker's default mutation
  breadth and per-test coverage capture, made explicit rather than left implicit.
- `thresholds` — `high: 80`, `low: 60`, `break: 0`; a mutation score break threshold still isn't
  enforced (`break: 0`) for the reason described under "Performance characteristics" below.
- `mutate` — `**/*.cs` excluding `obj/`/`bin/`/`*.Designer.cs`/`*.g.cs`, i.e. all of `buddy`'s
  source. Nothing feature specific is excluded by default.
- `reporters` — `Progress`, `Html` (browsable report under the output folder), and `cleartext` for
  terminal feedback during a run.

## Running it

```bash
task test:mutation:backend            # = dotnet tool restore + ./run-stryker.sh, from the repo root
# or, by hand:
cd src/backend && dotnet tool restore && cd buddy.IntegrationTests
./run-stryker.sh [stryker args...]    # e.g. -f <scoped-config.json> -c 2
```

Don't call `dotnet stryker` directly: on this SDK it can't compile the mutated code (see below),
and it can exit 0 without having tested anything. `run-stryker.sh` passes its arguments through
to Stryker, runs it with the SDK's Roslyn, keeps the full log at
`StrykerOutput/stryker-run.log` (override with `STRYKER_LOG`), and exits 1 unless the log shows
that mutants were actually tested. It fails on: test discovery aborted / no tests reported, the
initial test run failing, "Failed to restore the project to a buildable state" / any `FTL` line,
unhandled exceptions, a non-zero Stryker exit code, or a log without `<N> total mutants will be
tested` (N > 0) and a final score line. On GitHub Actions it also emits an `::error` annotation.

Scope to one feature while iterating (full-solution runs are slow — see below) with a separate
config passed via `-f` whose `mutate` lists only those files (the `mutation-fix-backend` skill's
`scoped-config.mjs` generates one). The `--mutate` CLI flag doesn't help: with a config file that
sets `mutate`, the config's list wins (verified on 4.16). A `test-case-filter` in the scoped
config (e.g. `FullyQualifiedName~CreateChild`) also shrinks the initial test run, at the cost of
mutants that only other tests would kill showing as survivors.

Reports are written to `buddy.IntegrationTests/StrykerOutput/` (git-ignored).

## Performance characteristics

Every mutant reruns (a filtered slice of) the real integration suite — real HTTP calls through
Alba, a real Postgres/Marten round-trip, real Keycloak token issuance. That's the same trade-off
the integration suite itself makes deliberately (see "Goals" in the integration testing doc): true
infrastructure over mocks, at the cost of wall-clock time. For mutation testing specifically this
cost multiplies by mutant count, so:

- Don't run a full, unscoped run in CI on every PR — it's a deliberately slow,
  thorough check, not a fast feedback loop. It's wired as an opt-in workflow
  (`.github/workflows/mutation-testing.yml`: `workflow_dispatch` with a `scope` input, or nightly
  when the `MUTATION_SCOPE` repository variable is `backend`/`both`), not a required PR check.
- Prefer scoping the `mutate` config (see above) to the feature slice you're actively hardening
  tests for while iterating.
- A mutation score threshold (`--break-at`) isn't configured yet — the suite doesn't have enough
  of a mutation-testing track record to know what score is realistic per feature. Add one once a
  few real runs establish a baseline instead of guessing a number now.
- 18 mutants scoped to one feature (`CreateChild`) took ~14.5 minutes end to end (~3.5 minutes for
  the coverage-capture dry run against the full 145-test suite, then ~11 minutes for the 18
  mutants themselves) at `concurrency: 1` (the config now defaults to `concurrency: 8`; these
  numbers predate that change and don't reflect the higher parallelism). Extrapolating linearly,
  an unscoped run would take multiple hours — plan CI runs accordingly (e.g. overnight, or scoped
  to the area of a specific PR) rather than expecting a quick turnaround. Stryker 5.0.0 creates
  4734 mutants across all of `buddy` (2026-10-01); 1277 of them are dropped as `CompileError` by
  its Safe Mode rollback (mostly "use of unassigned local variable" around `out`/pattern
  variables), leaving roughly 3450 to test.
- Stryker instruments and compiles the whole project even for a one-file scope, so a scoped run
  has a fixed overhead of ~3 minutes (build, initial test run, mutating and compiling ~4700
  mutants) before the first mutant is tested.

## Running on the net11.0 preview SDK

The backend targets `net11.0` with `LangVersion preview` on the .NET 11 preview SDK
(`11.0.100-preview.7.26381.103` here, unpinned by `global.json`). Getting Stryker.NET to test
mutants on it took three changes, all verified on 2026-10-01:

1. **dotnet-stryker 4.16.0 → 5.0.0** (`src/backend/dotnet-tools.json`). 4.16.0 bundles a net8.0
   `vstest.console` that can't start the net11.0 testhost. Discovery fails about 25 seconds in
   with `TestDiscoverer: Test discovery has been aborted!` / `did not report any test`, and at
   `--verbosity debug` the cause is a `NullReferenceException` in
   `DotnetTestHostManager.GetTestHostPath`. 5.0.0 discovers and runs the
   xunit v2 tests through VSTest with no test-project changes. No move to xunit v3 or the
   Microsoft Testing Platform runner was needed, so `--test-runner mtp` wasn't pursued.
2. **The SDK's Roslyn instead of Stryker's own.** 5.0.0 bundles Roslyn 5.9
   (`Microsoft.CodeAnalysis.CSharp` 5.9.0). That version can't compile this codebase's C#
   preview `union` `Result<T>`, so instrumenting the mutants produces thousands of follow-on errors
   (`Result<>` not found, and so on). Stryker's Safe Mode rollback then gives up with `Failed to restore
   the project to a buildable state ... Stryker can not proceed further` / `FTL Compilation
   failed`, and still prints `The final mutation score is 0.00 %` and exits 0. The SDK ships
   Roslyn 5.10 (`sdk/<version>/Roslyn/bincore`). `run-stryker.sh` copies the restored tool (from
   `~/.nuget/packages/dotnet-stryker/<version>/tools/net10.0/any`) to a temp folder, overwrites
   `Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll` with the SDK's, and runs
   `dotnet <copy>/Stryker.CLI.dll`. The copy is cached per tool and SDK version. The global NuGet
   cache isn't touched. The tool targets net10.0 and rolls forward onto the 11 preview runtime by
   itself. Set `STRYKER_SDK_ROSLYN=0` to run the tool as shipped, for example to check whether a
   later Stryker release bundles a new enough Roslyn.
3. **`language-version: "Preview"`** in `stryker-config.json` (it was `latest`). With the SDK
   Roslyn but `latest`, the run crashes with `ArgumentOutOfRangeException` in
   `CSharpRollbackProcess.IdentifyMutationsAndFlagForRollback`, because unions are a preview
   feature.

The checked-in `solution` path was also broken (`backend.slnx` instead of `../backend.slnx`), so
unscoped runs failed at once regardless of the SDK.

**Why the guard exists.** Every one of these failure modes can exit 0, or print a final score
without testing anything. `run-stryker.sh` (used by `task test:mutation:backend`, the
mutation-testing workflow and the `mutation-fix-backend` skill) therefore checks the log, not
just the exit code. See "Running it" above. It was checked against the recorded logs of each failure
above (4.16 discovery abort with exit 0 or 1, the 5.0 rollback crash, the "can not proceed" run
that printed a 0.00 % score, an initial-test-run failure, a missing solution, and a killed run).
All of them fail the guard, and the successful runs pass it.

**Remaining rough edges:**

- Overlaying a newer Roslyn into a tool built against an older one is unsupported by Stryker. It
  works because Roslyn keeps its public API backwards compatible, but a future SDK could break
  it. Drop the overlay (`STRYKER_SDK_ROSLYN=0`, or remove that block from the script) once a
  Stryker.NET release bundles a Roslyn that understands unions.
- Safe Mode marks 1277 of the 4734 project-wide mutants as `CompileError`. Those mutants are
  never tested. Most are "use of unassigned local variable" (CS0165) around `out`/pattern
  variables, plus a few CS0161/CS0266. That's a Stryker instrumentation limitation, not a test
  gap, but it means whole methods (e.g. `IdempotencyKeyRepository.DeleteExpiredAsync`,
  `CalendarOccurrenceExpansion.ExpandAsync`) currently get no mutation coverage. Look for
  `Safe Mode! Stryker will remove all mutations in <Method>` in the log.
- Stryker logs `Failed to load analyzer 'Microsoft.CodeAnalysis.Razor.Compiler' ... references a
  newer version (5.10.0.0) of the compiler`. That's harmless here: the backend has no Razor.
- Running two Stryker or `dotnet build`/`dotnet test` processes against the same checkout at
  once corrupts `bin/`. One run then failed its initial test run with `Could not load file or
  assembly 'Microsoft.AspNetCore.Mvc.Testing'`. Run one at a time per checkout.

## Verification status

This environment has Docker available. History:

- 2026-08-22, Stryker.NET 4.16 on an older preview SDK: `CreateChild/**/*.cs`, 18 mutants, 17
  killed / 1 survived (85.00%), 14m25s at `concurrency: 1`.
- 2026-09-09 to 2026-09-30: blocked. 4.16 couldn't discover tests on the preview.7 SDK (see
  above).
- 2026-10-01, Stryker.NET 5.0.0 plus the SDK Roslyn plus `language-version: Preview`, through
  `run-stryker.sh`: scope `Features/Guardians/CreateChild/CreateChild.Validator.cs` with
  `test-case-filter: FullyQualifiedName~CreateChild` (4 tests) at `concurrency: 2`. 3 mutants
  tested: 1 killed, 2 survived (33.33%), 3m11s, guard exit 0. The survivors are the `Statement`
  removals of the `FamilyName` and `Username` rules: no CreateChild test sends a blank family
  name or username. An earlier identical run reported the `Username` one as `Timeout` instead,
  so expect some timing-dependent `Timeout`/`Survived` flips on this suite. The JSON report's
  shape (absolute file keys, `projectRoot`, string ids, `mutatorName` like `"Statement
  mutation"`, 1-based `location`, `statusReason`) matches what the skill's `survivors.mjs`
  reads, and its score matched Stryker's.

A full, unscoped run hasn't been executed on 5.0.0. It's still multiple hours, so it's left
for CI or an overnight local run.
