# Mutation testing strategy

The integration test suite ([integration-testing-strategy.md](integration-testing-strategy.md))
proves that endpoints are covered, but coverage alone doesn't prove the assertions are
meaningful — a test that calls an endpoint and only checks the status code will show as "covered"
while missing a broken response body. Mutation testing closes that gap: Stryker.NET rewrites
small pieces of the production code (a `==` to `!=`, a boundary `<` to `<=`, a string literal to
`""`) one at a time and reruns the test suite; a mutant that still passes ("survived") marks a
spot the tests don't actually pin down. Status: implemented —
[`buddy.IntegrationTests/stryker-config.json`](../../../src/backend/buddy.IntegrationTests/stryker-config.json)
exists and was smoke-tested against a real feature slice in this environment (see "Verification
status").

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

- `project` / `test-projects` — pins Stryker to `buddy.csproj` mutated, tested by
  `buddy.IntegrationTests.csproj`, rather than relying on solution-wide auto-detection.
- `concurrency: 1` — the single biggest tuning decision. `BuddyApiFixture` starts one
  Postgres/Keycloak/mailpit trio and shares it across the whole test run *within one process*
  (see the integration test doc). Stryker's concurrency setting spawns that many independent test
  host processes in parallel, each of which would start its own trio of containers. Given the
  Keycloak JVM's startup cost, running several of those concurrently is a resource trade worth
  making deliberately, not defaulting into via Stryker's normal "one process per CPU core"
  behavior. Raise it locally if you have the Docker headroom and want faster wall-clock time.
- `mutate` — `**/*.cs` excluding `obj/`/`bin/`, i.e. all of `buddy`'s source. Nothing feature
  specific is excluded by default.
- `reporters` — `html` (browsable report under the output folder), `progress` and `cleartext` for
  terminal feedback during a run.

## Running it

```bash
cd src/backend/buddy.IntegrationTests
dotnet tool restore
dotnet stryker
```

Scope to one feature while iterating (full-solution runs are slow — see below) by editing the
`mutate` array in `stryker-config.json` directly, e.g. to
`["Features/Guardians/CreateChild/**/*.cs"]`, then reverting it before committing. The `--mutate`
CLI flag looks like it should do this instead without touching the checked-in file, but in
practice (Stryker.NET 4.16, config file present) it did not narrow anything in testing here — the
config file's `mutate` list won regardless of the flag. Editing the config's `mutate` array (or
pointing `-f` at a separate scoped config file) is the way that was actually verified to work.

Reports are written to `buddy.IntegrationTests/StrykerOutput/` (git-ignored).

## Performance characteristics

Every mutant reruns (a filtered slice of) the real integration suite — real HTTP calls through
Alba, a real Postgres/Marten round-trip, real Keycloak token issuance. That's the same trade-off
the integration suite itself makes deliberately (see "Goals" in the integration testing doc): true
infrastructure over mocks, at the cost of wall-clock time. For mutation testing specifically this
cost multiplies by mutant count, so:

- Don't run the full, unscoped `dotnet stryker` in CI on every PR — it's a deliberately slow,
  thorough check, not a fast feedback loop. It's wired as a manually-triggered workflow
  (`.github/workflows/mutation-testing.yml`, `workflow_dispatch`), not a required PR check.
- Prefer scoping the `mutate` config (see above) to the feature slice you're actively hardening
  tests for while iterating.
- A mutation score threshold (`--break-at`) isn't configured yet — the suite doesn't have enough
  of a mutation-testing track record to know what score is realistic per feature. Add one once a
  few real runs establish a baseline instead of guessing a number now.
- 18 mutants scoped to one feature (`CreateChild`) took ~14.5 minutes end to end (~3.5 minutes for
  the coverage-capture dry run against the full 145-test suite, then ~11 minutes for the 18
  mutants themselves) at `concurrency: 1`. Extrapolating linearly, an unscoped run across all of
  `buddy`'s ~1500 mutants would take multiple hours — plan CI runs accordingly (e.g. overnight, or
  scoped to the area of a specific PR) rather than expecting a quick turnaround.

## Verification status

This environment has Docker available. A real run, scoped to `Features/Guardians/CreateChild/**/*.cs`
via a temporary edit to the config's `mutate` array, executed end to end: built the project,
instrumented the code, ran the coverage-capture dry run (145 tests) against the real
Postgres/Keycloak/mailpit fixture, then tested the 18 mutants that survived filtering. Result: 17
killed, 1 survived, final mutation score 85.00%, in 14m25s. This confirms the whole pipeline works
against the real fixture, not just that it builds. A full, unscoped run across all of `buddy`'s
source was not executed here — per the extrapolation above, that would take multiple hours, well
past what's reasonable to burn in this session — and is left for a real CI or local run.
