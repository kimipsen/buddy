---
name: sonar-triage
description: Triage SonarCloud/SonarQube findings for Buddy. Take issues pasted by the user, or fetch them from the SonarCloud web API when a project key and SONAR_TOKEN are in the environment. Classify each against the repo's known false positives, verify it by reading the flagged code, fix only the real ones while keeping the tests green, and produce a table of rule, file:line, verdict and action. Use for "triage the Sonar findings", "go through these SonarCloud issues", "are these Sonar warnings real", "fix the real Sonar issues", "SonarCloud flagged X", or a pasted list of csharpsquid:/typescript: rule hits.
---

# Sonar Triage

Goal: decide for every SonarCloud/SonarQube finding whether it's real, fix the real ones with minimal changes, and hand back a table the user can work through in SonarCloud. Most findings in this repo are known analyzer noise, so verification matters more than fixing.

## 1. Get the findings

**Pasted by the user** (rule IDs, file paths, lines, messages, in any format, including a screenshot): use them as they are. If a line number is missing, find the spot from the message.

**Fetched from the API**: the repo has no `sonar-project.properties`, no Sonar workflow and no README badge. The project uses SonarCloud Automatic Analysis, so the project key isn't in the repo. On SonarCloud the key is usually `<organization>_<repo>` (the GitHub remote is `kim-ipsen_kit/buddy`), but that hasn't been confirmed. Take the key from `SONAR_PROJECT_KEY`, or ask the user for it. A project key is not a secret. Tokens are, so read them from the `SONAR_TOKEN` environment variable only. Never ask the user to paste a token into the chat, and never echo it or put it on a command line. If the token isn't set and the project is private, ask the user to export `SONAR_TOKEN` in their shell, or to paste the findings instead.

```bash
node .claude/skills/sonar-triage/fetch-issues.mjs --project <key> [--branch <name> | --pr <number>] [--json]
```

The script calls `api/issues/search` (open issues only, 500 per page) after checking that the project exists, because the search endpoint returns an empty list rather than an error for an unknown or inaccessible key. It prints one line per issue (`rule  path:line  severity  message  [issue key]`) and a count per rule. Use `--from <file>` to parse a saved API response. `SONAR_HOST_URL` overrides `https://sonarcloud.io` for SonarQube.

## 2. Classify against the known issues

For C# (`csharpsquid:*`) findings, read **`.claude/skills/claude-backend/references/sonar-known-issues.md`** first. It is the canonical list of known false positives in this repo, and also lists the rules that look like noise but are real bugs. Don't work from memory or from older copies elsewhere: the file is maintained, and older copies drift.

A match on rule ID is only a *candidate* verdict. Every known-issue entry has a condition (the file declares a `union`; nullable is enabled project-wide; the method is a handler or endpoint; the empty record is a union case). Confirm the condition holds before you apply the verdict.

For frontend findings (`typescript:*`, `javascript:*`, `css:*`, `Web:*`) there is no known-issues list. Use general judgement against the code and the repo's conventions (Angular standalone/zoneless with signals, Vitest specs). Load the relevant frontend skill if one is listed. Rules that are commonly noise in an Angular codebase include decorator-metadata or DI-constructor complaints and "nested template literals" in test fixtures. Even these need a look at the code, not a reflex dismissal.

## 3. Verify every finding against the code

Open the flagged file at the flagged line and read enough around it to judge. Verdicts:

- **real**: the code actually has the problem: a bug, an ignored return value, dead code, a leaked resource, a security issue, or a genuine maintainability problem worth fixing now.
- **false-positive**: the analyzer is wrong about this code. It misparses it, misses project settings, or the pattern is the intended architecture.
- **won't-fix**: the finding is technically accurate, but fixing it would make the code worse or fight a deliberate design choice, e.g. a threshold rule on a pattern the architecture requires.

If you can't tell from the code, say so and mark it **needs-decision**. Don't guess.

## 4. Fix only the real ones

- Keep the change minimal and behaviour-preserving. Follow the area's conventions (load `claude-backend` for backend changes).
- Don't restructure working code to silence a false positive or a won't-fix finding, and don't add `#pragma warning disable` / `// NOSONAR` / `[SuppressMessage]` for them. Marking them in SonarCloud is the right channel (step 5).
- A real finding that's a behaviour bug needs a test that would have caught it. Add an integration test in `src/backend/buddy.IntegrationTests` or a Vitest spec next to the component or service.
- Tests must stay green. After backend fixes, run `task test:backend` from the repo root, or narrow it with `dotnet test --filter "FullyQualifiedName~<Feature>"` from `src/backend/buddy.IntegrationTests` first. Integration tests use Testcontainers, so Docker must be running. After frontend fixes, run `npx ng test --watch=false --include <spec>` and `npx tsc --noEmit -p tsconfig.app.json` from `src/frontend/buddy`, then `task test:frontend`.
- If a fix would be large or touches behaviour the user may rely on, describe it and ask before making it.

## 5. Report

Produce one table, ordered by verdict (real first), then by file:

| Rule | File:line | Verdict | Action |
|---|---|---|---|
| `csharpsquid:S2201` | `src/backend/buddy/.../MartenFooEventStore.cs:88` | real | Fixed: use the result of `Reverse()`. Added an ordering test |
| `csharpsquid:S3903` | `src/backend/buddy/Features/Foo/FooEvent.cs:5` | false-positive | Mark False Positive in SonarCloud (file declares a `union`) |
| `csharpsquid:S107` | `src/backend/buddy/Features/Foo/Foo.Handler.cs:14` | won't-fix | Mark Accepted/Won't Fix (handler DI parameters) |

Keep each Action specific: what you changed, or what the user should do in SonarCloud and why. After the table:

- list the files changed and the test commands you ran, with their results;
- for false positives and won't-fix findings, **recommend marking them in SonarCloud** (issue → "False Positive" or "Accept"/"Won't Fix", with a short comment pointing at the reason) instead of changing code. If the same rule keeps firing on a whole category, suggest a quality-profile change or an exclusion in the project settings. Don't perform those transitions yourself through the API unless the user explicitly asks for that and `SONAR_TOKEN` has the permissions for it;
- if you found a new recurring false positive that isn't in `sonar-known-issues.md`, say so and suggest adding it there. Don't edit that file unless asked;
- list any `needs-decision` items, with the question for the user.

Don't commit unless the user asks.
