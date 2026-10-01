---
name: mutation-fix-backend
description: Run Stryker.NET (dotnet-stryker) on a few backend source files or one feature slice, triage the surviving and uncovered mutants, strengthen the xunit/Alba integration tests in buddy.IntegrationTests to kill the relevant ones, and re-run on the same files until nothing relevant survives, then send a push notification with the outcome. Use for "run the backend mutation tests and fix survivors", "kill the surviving mutants in the CreateChild handler", "mutation-test this feature", "harden the backend tests with Stryker". For frontend (StrykerJS) use mutation-fix instead.
---

# Backend Mutation Fix Loop

Goal: take a small set of backend source files (one feature slice, or the files a change touched) through Stryker.NET, then keep tightening the integration tests and re-running Stryker **on that same set** until every relevant survivor is killed. Everything below runs from `src/backend/buddy.IntegrationTests` unless it says otherwise. Background on the setup: `docs/backend/analysis/mutation-testing-strategy.md`.

Arguments (optional, free text): the files or globs to mutate (paths relative to `src/backend/buddy`, e.g. `Features/Guardians/CreateChild/**/*.cs`), `CONCURRENCY=N` (default 2), `MAX_ROUNDS=N` (default 3). If no scope is given, use the backend `.cs` files changed on the current branch (`git diff --name-only master...HEAD -- src/backend/buddy` plus uncommitted ones). If that is empty too, ask which feature to harden. **Never run an unscoped `dotnet stryker` / `task test:mutation:backend`.** That mutates all of `buddy` (~1500 mutants) and takes hours.

Every mutant re-runs the real integration suite against Postgres/Keycloak/Mailpit via Testcontainers, so **Docker must be running** (`docker info`). Each parallel Stryker session starts its own set of containers. The checked-in config uses `concurrency: 8`, which is too much for the devcontainer, so keep it at 2 unless the user asks for more. Measured cost: 18 mutants in `CreateChild` took about 14.5 minutes at concurrency 1, and roughly 3.5 of that was the initial coverage run. Expect each round to take tens of minutes.

## 0. Preflight

```bash
docker info >/dev/null && echo docker ok
cd src/backend && dotnet tool restore && cd buddy.IntegrationTests   # dotnet-stryker 4.16.0 from src/backend/dotnet-tools.json
```

**Known blocker:** on the .NET 11 preview SDK, Stryker.NET 4.16.0 cannot discover tests. Its bundled net8.0 vstest can't drive the net11.0 testhost. The run then fails about 30 seconds in with `TestDiscoverer: Test discovery has been aborted!` / `did not report any test` / `Stryker.NET failed to mutate your project`. **`dotnet stryker` still exits 0 in that case**, so check the log text, not the exit code. If you see this failure, stop: report the blocker (see the strategy doc's "Known issue" section), notify (step 6), and don't try to work around it. Check whether a newer `dotnet-stryker` exists first (`dotnet tool search dotnet-stryker` or the run's own version-check line). If one does, tell the user rather than bumping `dotnet-tools.json` yourself.

## 1. Scope the run and run Stryker

Stryker.NET ignores `--mutate` on the CLI when the config file sets `mutate`, and the checked-in `stryker-config.json` does. So scope the run with a separate config passed via `-f`. Never edit the checked-in file. Generate the scoped config into the scratchpad:

```bash
node ../../../.claude/skills/mutation-fix-backend/scoped-config.mjs --out <scratchpad>/stryker-scoped.json --concurrency 2 \
  Features/Guardians/CreateChild/CreateChild.Handler.cs Features/Guardians/CreateChild/CreateChild.Validator.cs
```

The script copies `stryker-config.json`, replaces the `mutate` list with your files or globs (it keeps the `!obj`/`!bin`/generated-file excludes), makes the solution path absolute, and adds the `Json` reporter. The checked-in config only has Progress/Html/cleartext, which produce no machine-readable report. Then run:

```bash
dotnet stryker -f <scratchpad>/stryker-scoped.json -O StrykerOutput/mutation-fix --skip-version-check > <scratchpad>/stryker-round-1.log 2>&1
```

Run it with `run_in_background: true` and a generous timeout (at least 60 minutes for a feature slice). Wait for the completion notification; don't poll.

- `-O StrykerOutput/mutation-fix` gives every round the same output folder, so the report is always at `StrykerOutput/mutation-fix/reports/mutation-report.json`, with the HTML report next to it in `mutation-report.html`. `StrykerOutput/` is git-ignored. Each run overwrites the folder, so copy the report after round 1 (`cp StrykerOutput/mutation-fix/reports/mutation-report.json <scratchpad>/round-1.json`). You need it for the before score.
- Read the end of the log. If it says the run failed to mutate the project, the initial test run failed, or the build failed, stop, report the error and notify (step 6). Don't try to fix unrelated breakage silently.
- Other CLI flags, verified in `dotnet stryker --help` (4.16.0): `-c|--concurrency`, `-f|--config-file`, `-O|--output`, `-r|--reporter` (Json, Html, ClearText, Markdown, …), `-l|--mutation-level`, `-V|--verbosity`, `--since[:<committish>]` ("only test changed files", diffs against git), `--with-baseline[:<committish>]` (experimental, needs a dashboard or disk baseline, not set up here), `-b|--break-at`, `--break-on-initial-test-failure`, `-t|--test-runner vstest|mtp`. There's no CLI flag for a test-case filter (that's the config's `test-case-filter`). An explicit file list in the scoped config is the verified way to scope. `--since:master` is an alternative for "everything changed on this branch", but it hasn't been verified here.

## 2. List the survivors

```bash
node ../../../.claude/skills/mutation-fix-backend/survivors.mjs Features/Guardians/CreateChild/CreateChild.Handler.cs ...
```

It reads `StrykerOutput/mutation-fix/reports/mutation-report.json`, or the newest `StrykerOutput/*/reports/mutation-report.json`. Use `--report <path>` to read another one, such as a saved round. Per file it prints the score and each `Survived` / `NoCoverage` mutant as `L<line> <mutator display name> #<id> (disable as: <Mutator>)`, with the original (`-`) and replacement (`+`) code, plus counts of `Ignored` and `CompileError` mutants. Stryker.NET keys files by absolute path, and the script matches the paths you pass by suffix, so `Features/...`, `buddy/Features/...` and absolute paths all work. Exit code 1 means there are findings. Add `--json` for structured output. If the total is 0, go to step 5.

## 3. Triage, then fix

Before deciding, read the source file and the tests that exercise it. Tests live in `buddy.IntegrationTests/Features/<Domain>/<UseCase>/*Tests.cs` and mirror `buddy/Features/<Domain>/<UseCase>/`. They share `BuddyApiFixture` (one Postgres/Keycloak/Mailpit trio per test process) and per-domain helpers (`<Domain>TestHelpers.cs`, `<Domain>TestDtos.cs`). The `claude-backend` skill covers the slice layout (command, handler, endpoint, validator) and test conventions. Load it if you need them. Classify each finding:

**Relevant: kill it with a test.** The mutation changes observable behaviour: a validation rule or its limit, an authorization check, a Result → HTTP status mapping, a response body field, an appended domain event or its payload, a comparison boundary, a branch in a handler, a LINQ filter or ordering in a read model or projection. Most findings belong here. `NoCoverage` means no test runs that code at all. That usually points to a missing test for an endpoint path or error branch.

**Equivalent: suppress with a reason.** Use this only when no test could ever tell the difference. Examples: a `Statement` removal of a call whose effect is immediately overwritten, a `<` vs `<=` where the equal case can't occur, a `NullCoalescing` mutation on a value that's never null by type. Stryker.NET's comment syntax is a `//` comment placed on the line directly above the statement:
```csharp
// Stryker disable once Equality,Boolean : <why it's equivalent>
```
`once` limits it to the next statement/syntax node. Without `once`, it applies until a matching `// Stryker restore <Mutator>` (or `restore all`). The mutator list takes the names printed as `disable as:`, comma-separated, or `all`. A `:` and a reason are required by house rule here, even though Stryker treats the reason as optional. Suppressed mutants show up as `Ignored` in the next report. Check that they did: a misspelled mutator name or a misplaced comment is silently ignored, and the mutant stays `Survived`. Only suppress when you can state concretely why the mutant is equivalent. "Hard to test" is not a reason, and unreachable code is dead code, not an equivalent mutant (see below). Logger calls and `ToString` are already excluded by `ignore-methods` in the config.

**Not worth killing: leave it and report it.** These are mutants where a test would only pin incidental detail: log or exception message text nobody reads, OpenAPI metadata (`.WithSummary(...)`, `.WithTags(...)`, `.WithName(...)` strings), DI or registration boilerplate where a test would only restate the config. Don't suppress them. List them in the final report so the user can decide. When unsure between "relevant" and "not worth it", treat the mutant as relevant.

Fixing rules:

- **Change tests, not production code.** Add or tighten assertions in the existing test class, in its style (the fixture, the domain helpers, Alba scenarios). Assert on the precise value the mutant changes: the exact status code, the exact field in the response body or in the persisted/projected state, the exact validation boundary (e.g. a 200-character name passes and a 201-character name fails), the exact event appended. "Returns 2xx" or "is not null" isn't enough.
- For `Equality` / `Conditional` survivors, test the boundary from both sides. For `Logical` / `Boolean`, test both branches. For `String` survivors that matter, assert the exact string. For `Block` / `Statement` / `NoCoverage`, add a test that actually goes down that path (the error branch, the "already exists" branch, the other role).
- **Dead or unreachable production code: delete it** rather than suppressing its mutants. This is the only production change allowed without asking. Keep the deletion minimal and behaviour-preserving, rebuild, run the affected tests, and list each deletion in the report. A `// Stryker disable` is never the answer for dead code.
- If a survivor reveals a real bug, don't paper over it. Note it for the user and ask before changing production code.
- Each new test must make sense on its own. Don't write a test whose only purpose is to name the mutant.

After editing, run just the affected tests to confirm they pass (Docker required):

```bash
dotnet test --filter "FullyQualifiedName~CreateChild"
```

Fix any failing test before you move on. Then run the whole suite once (`dotnet test` here, or `task test:backend` from the repo root), because Stryker's initial run uses all of it.

## 4. Re-run Stryker on the same files

Regenerate the scoped config with only the files that still have relevant survivors. Then run step 1's `dotnet stryker -f ... -O StrykerOutput/mutation-fix` command again in the background, logging to `stryker-round-N.log`. Stryker.NET has no incremental cache in this setup (the baseline is disabled), so each run retests every mutant in scope. That is why the scope should shrink each round. Then go back to step 2 with the same file list. A file you dropped from scope will show as "not in report", which is expected. Take its final numbers from the round where it was last run.

Stop looping when any of these is true:
- no relevant survivors remain (only suppressed equivalent mutants or reported "not worth it" ones);
- a round killed nothing new, which means you're stuck. Report what's left and why instead of retrying the same approach;
- `MAX_ROUNDS` (default 3) re-runs have been done.

## 5. Report

Finish with a short summary:

- the files in scope, with mutation score before → after (round 1 vs the last round each file was in);
- how many survivors were killed, and which test files were changed;
- every `// Stryker disable` added, with its reason, and confirmation that each one now shows as `Ignored`;
- any dead code deleted;
- anything left unfixed (not worth it, or stuck), with the reason, and any possible bugs found;
- total wall-clock time of the Stryker rounds, so the next run can be sized.

Don't commit unless the user asks. Nothing in `StrykerOutput/` is committed. The HTML report for a closer look is `StrykerOutput/mutation-fix/reports/mutation-report.html`.

## 6. Notify

A cycle takes long enough that the user has usually walked away, so end every cycle with one notification. Load the `PushNotification` tool first if it isn't loaded yet (`ToolSearch` with `select:PushNotification`). Send it after the step 5 report, with `status: "proactive"`. Keep it to one line, under 200 characters, no markdown, and lead with the outcome:

- finished: `mutation-fix-backend done: CreateChild 85.0%→100%, 3 killed, 1 left (not worth it). Tests changed, uncommitted.`
- stuck or hit `MAX_ROUNDS`: `mutation-fix-backend stuck: 2 survivors left in CreateChild.Handler.cs after 3 rounds, needs your call.`
- blocked: `mutation-fix-backend blocked: Stryker.NET 4.16 can't discover tests on the net11 preview SDK. Nothing run.`

Also notify, in the same format, whenever the loop stops early and needs the user: Docker isn't running, Stryker fails before it tests mutants (including the known discovery blocker), or a survivor points to a real bug and needs their go-ahead before production code changes.

Send only one notification per cycle, plus these early stops. Don't notify after the individual Stryker runs in steps 1 and 4. If the tool says the notification wasn't sent (for example because the user is at the terminal), that's fine. Don't retry.
