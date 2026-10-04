---
name: mutation-fix
description: Run the next batch of frontend mutation tests (task test:mutation:frontend:batch), triage the surviving and uncovered mutants, strengthen the Angular/Vitest specs to kill the relevant ones, and re-run Stryker on the same files until nothing relevant survives, with a status update every 5 minutes while Stryker runs, then send a push notification with the outcome. Use for "run the mutation tests and fix survivors", "kill the surviving mutants", "work through the next mutation batch".
---

# Mutation Fix Loop

Goal: take one batch of frontend source files through Stryker, then keep tightening tests and re-running Stryker **on that same batch** until every relevant survivor is killed. Everything below runs from `src/frontend/buddy` unless it says otherwise.

Arguments (optional, free text): `BATCH_SIZE=N`, `CONCURRENCY=N`, `MAX_ROUNDS=N` (default 4), or an explicit list of files to use instead of the next batch (skip step 1 then).

## 1. Run the batch and record which files it covered

From the repo root:

```bash
task test:mutation:frontend:batch [BATCH_SIZE=5] [CONCURRENCY=2] > <scratchpad>/batch.log 2>&1; echo $? > <scratchpad>/batch.log.exit
```

This is slow (every mutant re-runs `npm test`). Run it with `run_in_background: true` and a generous timeout, redirect output to a log file in the scratchpad, write the exit code next to it so the status updates know when it ends, and start the status updates (below). Then wait for the completion notification. Don't poll the log yourself.

**Status updates every 5 minutes.** Right after starting a background Stryker run (here and in step 4), load the `Monitor` tool if it isn't loaded yet (`ToolSearch` with `select:Monitor`) and start it with `timeout_ms: 1800000`, a description such as `mutation-fix batch progress`, and:

```bash
"$(git rev-parse --show-toplevel)/.agents/skills/mutation-fix/progress.sh" <scratchpad>/batch.log
```

Every 5 minutes it prints one line with the elapsed time, the batch (`Running batch 46-50 of 103`) and Stryker's latest progress (`Mutation testing 72% (elapsed: ~1h 4m, remaining: ~25m) 324/450 tested (139 survived, 0 timed out)`). It adds `WARNING: no log output for Nm, may be hung` once the log hasn't changed for 10 minutes, and it exits with a `finished with exit N` line once the `.exit` file appears. Optional arguments: interval and stall threshold in seconds (defaults `300 600`).

- Relay each event to the user as one short line of chat text, e.g. `Mutation run: 72% (324/450 tested, 139 survived), ~25m left, running 64m.` That's the whole reply. Don't start triage or other work from a progress event, and don't send a push notification for it.
- A Monitor ends after 30 minutes. When it expires and the `.exit` file still doesn't exist, start it again with the same command. Elapsed time counts from the log's creation, so it carries on across re-arms.
- On a `may be hung` warning, check that the run's still alive (`ps -ef | grep -c '[s]tryker'`, and the end of the log) and tell the user what you found. Don't kill the run unless they ask.

- Take the batch's file list from the `Running batch X-Y of Z:` lines in the output. **Remember it**: it's the scope for the rest of the loop.
- If the output says `All N files have been covered`, stop and tell the user (and notify, see step 6); they can start over with `task test:mutation:frontend:batch -- --reset`.
- If Stryker fails before testing mutants (initial test run failed, TypeScript errors), stop, report the error and notify (step 6) — don't try to fix unrelated breakage silently.

**Never re-run the batch task to retry.** The script appends the batch to `.mutation-batch-progress` after each run, so a second invocation moves on to the *next* files. Re-runs use Stryker directly (step 4).

## 2. List the survivors for the batch

```bash
node ../../../.agents/skills/mutation-fix/survivors.mjs <file1> <file2> ...
```

It reads `reports/stryker-incremental.json` and prints, per file, the score and each `Survived` / `NoCoverage` mutant as `L<line> <Mutator> #<id>` with the original (`-`) and replacement (`+`) code. Exit code 1 means there are findings. Add `--json` if you want structured output. If the total is 0, go to step 5.

## 3. Triage, then fix

Read the source file and its colocated spec (`foo.ts` → `foo.spec.ts`) before deciding. Classify each finding:

**Relevant — kill it with a test.** The mutation changes observable behaviour: a branch, a comparison boundary, a returned value, an emitted value, a request URL/body/params, a signal update, error handling, a route guard result, rendered output. Most findings are here.

**Equivalent — suppress with a reason.** No test could ever tell the difference (e.g. an `ArrayDeclaration` on a value that's immediately overwritten, a `<` vs `<=` where the equal case can't occur, an optional-chaining removal on a value that's never nullish by type). Add directly above the line:
```ts
// Stryker disable next-line <MutatorName>: <why it's equivalent>
```
Only do this when you can state concretely why it's equivalent. "Hard to test" is not equivalent, and code that can never run is dead code, not an equivalent mutant (see the fixing rules below).

A disable comment applies to the next line's nodes only: list every mutator that survives on that line (`// Stryker disable next-line StringLiteral,CallExpression: ...`), and don't put one above `} catch {` / `} finally {` — it gets attached to the `try` block and has no effect on the catch/finally body.

**Not worth killing — leave and report.** Low-value mutants where a test would only pin incidental detail: `console.*` / logging message text, purely cosmetic strings with no product meaning, framework boilerplate (e.g. `providedIn: 'root'`, decorator metadata) where a test would just restate the config. Don't suppress these; list them in the final report so the user can decide.

When unsure between "relevant" and "not worth it", treat it as relevant.

Fixing rules:

- **Change tests, not production code.** Add or tighten assertions in the existing spec (match its style: TestBed setup, `HttpTestingController`, helpers already in the file). Assert on the precise value the mutant changes — exact URL, exact payload, exact boundary — rather than just "was called" or "is truthy".
- Test boundaries on both sides for `EqualityOperator` / `ConditionalExpression` survivors; test both branches for `LogicalOperator` / `BooleanLiteral`; for `StringLiteral` survivors that matter, assert the exact string; for `BlockStatement` / `NoCoverage`, add a test that actually exercises the code path.
- **Dead or unreachable production code: delete it** (the one production change allowed without asking) rather than suppressing its mutants — e.g. a signal nothing reads, a fallback branch the preceding guards make impossible. Keep the deletion minimal and behaviour-preserving, run the spec and `npx tsc --noEmit -p tsconfig.app.json`, and list each deletion in the report. A `// Stryker disable` is never the answer for dead code.
- If a survivor reveals a real bug, don't paper over it — note it for the user and ask before changing production code.
- Keep new tests meaningful on their own; don't write a test whose only purpose is to name the mutant.

After editing, run just the affected specs to confirm they pass (the real code must still be green):

```bash
npx ng test --watch=false --include src/app/path/to/foo.spec.ts
```

Fix any failing test before moving on — a failing initial test run makes Stryker abort.

## 4. Re-run Stryker on the same files

```bash
npx stryker run --mutate "<file1>,<file2>,..." --incremental --force --concurrency <same as step 1, default 2>
```

Run it in the background as in step 1, with its own log and exit file (`> <scratchpad>/round-N.log 2>&1; echo $? > <scratchpad>/round-N.log.exit`), and start the 5-minute status updates on that log. **`--force` is required.** This project uses the command test runner with coverage analysis off, so Stryker can't see that a spec changed: without `--force` it reuses every cached result ("N of N mutant result(s) are reused"), finishes in seconds and reports the same survivors. `--force` re-tests every mutant in the listed files, and `--incremental` still writes the fresh results to `reports/stryker-incremental.json`. Because every mutant in those files gets retested, pass only the files that still have survivors.

Then go back to step 2 with the same file list.

Stop looping when any of these is true:
- no relevant survivors remain (only suppressed-equivalent or reported "not worth it" ones);
- a round killed nothing new — you're stuck; report what's left and why instead of retrying the same approach;
- `MAX_ROUNDS` (default 4) re-runs have been done.

## 5. Report

Finish with a short summary:

- the batch's files, with mutation score before → after;
- how many survivors were killed, and which specs were changed;
- every `// Stryker disable` added, with its reason;
- any dead code deleted;
- anything left unfixed (not-worth-it or stuck), with the reason, and any possible bugs found;
- that `reports/stryker-incremental.json` changed — it's the committed baseline the nightly CI run starts from, so it should be committed together with the spec changes.

Don't commit unless the user asks. The HTML report for a closer look is at `reports/mutation/index.html`.

## 6. Notify

A cycle takes long enough that the user has usually walked away, so end every cycle with one notification. Load the `PushNotification` tool first if it isn't loaded yet (`ToolSearch` with `select:PushNotification`), then send one after the step 5 report, with `status: "proactive"`. Keep it to one line, under 200 characters, no markdown, and lead with the outcome:

- finished: `mutation-fix done: batch 51-55, 86.5%→100%, 5 killed, 1 left (not worth it). Specs changed, uncommitted.`
- stuck or hit `MAX_ROUNDS`: `mutation-fix stuck: 3 survivors left in foo.service.ts after 4 rounds — needs your call.`

Also notify (same format) whenever the loop stops early and needs the user: every file covered, Stryker failing before it tests mutants, or a survivor that points to a real bug and needs their go-ahead before production code changes.

Send only one notification per cycle, plus these early stops. Don't notify after the individual Stryker runs in steps 1 and 4, or for the 5-minute status updates (those are chat lines only). If the tool says the notification wasn't sent (for example because the user is at the terminal), that's fine; don't retry.
