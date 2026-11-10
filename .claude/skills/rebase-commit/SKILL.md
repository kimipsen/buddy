---
name: rebase-commit
description: Commit finished work and land it on master (or another target) through a GitHub pull request on kimipsen/buddy. Commits on a work branch (creating agent/<topic> if you're on master), rebases it onto the freshly fetched origin/master, re-runs the relevant tests if master moved, pushes the branch and opens the PR with gh (Conventional Commit title, What changed / Testing body, Closes #N), using the bundled land.sh for the rebase, push and post-merge cleanup. Once the PR is merged on GitHub it fast-forwards the local master, removes the worktree and deletes the branch. Never merges the PR unless asked. Use when a task is done and should be committed, and for "commit this", "open a PR", "create a pull request", "land this branch", "get this onto master", "merge my worktree branch into master", "the PR is merged, clean up", "clean up the merged worktree". Reviewing a diff is backend-aware-review; production deploys are deploy.
---

# Commit and land through a pull request

Code reaches `master` only through a pull request on GitHub (`kimipsen/buddy`). The user's
standing preference: when an agent finishes a task, it commits the work, pushes its work branch
and **opens the PR without asking first**. The user (or CI) reviews and merges it; you don't.

```text
commit on a work branch → land.sh rebase → land.sh push → gh pr create → (user merges) → land.sh cleanup
```

`land.sh` (next to this file) does the git halves with the safety checks; use it rather than the
raw commands. Set it in the same command that uses it (shell variables don't survive between tool
calls), from the main checkout's copy, because an older worktree may not have the current file:

```bash
LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../.claude/skills/rebase-commit/land.sh"
```

Rules:

- **Never merge the PR** (`gh pr merge`, the web UI, auto-merge) unless the user asks in this
  conversation.
- **Never push to `master`** directly, and never commit on it; all work goes through a branch.
- `origin` has two push URLs (the GitHub repo and a mirror), so a push publishes the branch to
  both. Push only your own work branch.
- `git push --force-with-lease` (what `land.sh push` does) is fine on **your own** work branch
  after a rebase. Never a plain `--force`, never force-push someone else's branch, never
  `git reset --hard` on the target, `git stash` someone else's changes, `--no-verify`, or delete
  branches you didn't create.
- The target is `master` unless the user names another branch.

## 1. Make sure the task is actually done

Open a PR only for work you'd hand over: the change is complete, and the tests for what you
touched pass (`task test:backend`, `task test:frontend`, a single spec, see CLAUDE.md). If tests
fail and you can't fix them, don't open a PR; report that instead (or, if the user wants one
anyway, open it as a draft with `--draft` and say what fails). Also follow the repo's own
done-criteria where they apply (i18n parity, `task docs:openapi` after an API change,
`task codegen:backend` after a handler change, screenshots for a visible page change). CI runs
backend, frontend, e2e, CodeQL and security workflows on every PR to `master`, so a red check
there is likely, not hypothetical, if you skip these.

## 2. Get onto a work branch

```bash
git status --short
git branch --show-current
```

- **Worktree** (a `worktree-<feature>`, `worktree-agent-*` or other non-target branch): stay on
  it. The branch name becomes the PR's head branch.
- **On the target itself** (e.g. `master` in the main checkout): create a branch from where you
  are; uncommitted changes come along: `git switch -c agent/<short-topic>`.

Look at what's in the working tree. Stage only the files that belong to _your_ task, by path
(`git add path/a path/b`), never `git add -A` when there are changes you didn't make; mention
the ones you left alone. Never stage `.env` or `appsettings.*.json`.

## 3. Commit

Match the repo's Conventional Commits style (`git log --oneline -15`):
`feat(calendar): sort occurrences by start time, then name`, `fix(...)`, `test(...)`,
`docs(...)`, `refactor(...)`, `chore(...)`, `ci(...)`. Imperative, lowercase after the colon,
scope = the domain or area. One commit per coherent change; several are fine. If the work fixes a
GitHub issue, add `Closes #<n>` to the body. End the message with the attribution trailer from the
system reminder, if there is one.

```bash
git commit -F - <<'EOF'
feat(tasks): let guardians reorder a child's tasks

<optional body: why, not what>

Closes #<n>

Co-Authored-By: <trailer from the system reminder>
EOF
```

Hooks (`task hooks:install`): the **pre-commit** hook runs Prettier/ESLint, i18n parity and C#
whitespace checks on staged files. If it fails, fix what it reports, re-stage and commit again.
The **post-commit** hook may add a follow-up `docs: sync documentation (auto)` commit on your
branch; that's expected and goes into the PR with the rest. Add `[skip-docs]` to the message only
if the user asked for it.

## 4. Rebase onto origin/master

```bash
LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../.claude/skills/rebase-commit/land.sh"
"$LAND" rebase master
```

Run it from your work branch's worktree. It fetches `origin/master` and rebases onto it, so the
PR is based on what GitHub has, not on a possibly stale local `master`. It refuses with exit 1 and
changes nothing if the tree has uncommitted tracked changes, HEAD is detached, you're on the
target, or a rebase/merge is in progress. It sets `SKIP_DOC_AGENT=1`, because git runs the
post-commit hook for every replayed commit and the doc-sync agent must not fire on those.

- Prints `TARGET_MOVED` → `origin/master` had new commits, so your code now sits on a base it was
  never tested on. Re-run the tests for the areas you touched (and for anything the new commits
  changed that your code uses) before step 5. If they fail, fix with a new commit and repeat
  from step 4.
- **Exit 2: conflict.** The rebase is left in progress. Look at each conflicted file
  (`git diff --name-only --diff-filter=U`):
  - If the resolution is clear (both sides' intent can be kept, e.g. adjacent additions, a renamed
    symbol), resolve it, `git add` the files, then `SKIP_DOC_AGENT=1 git rebase --continue`
    (repeat per commit). Re-run the tests afterwards, as for `TARGET_MOVED`.
  - If it's not clear which side should win, or the conflict is in code you didn't write for
    this task, `git rebase --abort` (the branch is restored exactly) and ask the user.
  - Never resolve by blindly taking `--ours`/`--theirs` for a whole file.

## 5. Push the branch

```bash
LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../.claude/skills/rebase-commit/land.sh"
"$LAND" push master
```

It re-checks that the branch sits on the tip of `origin/master` and has no merge commits, then
`git push --force-with-lease -u origin <branch>` and prints the commits it pushed. If it says the
branch isn't based on the tip, master moved since step 4: go back to step 4.

## 6. Open the pull request

If a PR from this branch is already open (`gh pr view --json number,state,url`), the push has
updated it: don't open another. Update its body if what changed or how you tested it changed
(`gh pr edit --body-file -`).

Otherwise create it against the target:

```bash
gh pr create --base master --head "$(git branch --show-current)" \
  --title "<title>" --body-file - <<'EOF'
Closes #<n>

## What changed

<what a reviewer needs to know: the behaviour change, and why; bullets for separate parts>

## Testing

- <each test or check you ran and its result, e.g. "`week-plan-sheet.spec.ts`: 12 tests pass">
- <screenshots regenerated, `task docs:openapi` run, …>

Not tested: <what you didn't or couldn't verify, if anything>

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

- **Title:** for a single commit, its subject line (`feat(print): centre meals and night duty in
the week plan cells`); for several, a Conventional Commit summary of the whole change. Leave
  `[skip-docs]` out of the title.
- **Body:** written for the reviewer. Drop `Closes #<n>` when there's no issue; keep it at the top
  when there is, so GitHub closes the issue on merge. Report only checks you actually ran. End
  with the PR attribution line from the system reminder, if there is one, in place of the one
  above.
- Use `--draft` only when the work isn't ready to merge (failing tests the user wants to see, an
  open question); say why in the body.

`gh pr create` prints the PR URL. After that, check that CI started:
`gh pr checks --watch=false` (pending is fine; you don't have to wait for it unless the user asks).

## 7. After the PR is merged: clean up

Merging is the user's step. When they say the PR is merged (or ask you to clean up), or you see
`gh pr view <branch> --json state` report `MERGED`:

- **This session's own worktree, entered with `EnterWorktree`** (or `claude --worktree`, if
  `ExitWorktree` accepts it): first sync the local target from the main checkout
  (`git -C <main checkout> fetch origin master` then `git -C <main checkout> merge --ff-only origin/master`,
  only if it has no uncommitted tracked changes), then call `ExitWorktree` with
  `action: "remove"`. It deletes the worktree and its branch and moves the session back to the
  main checkout. It refuses if there are uncommitted files; never pass `discard_changes: true`
  without asking the user.
- **Anything else** (an `agent/<topic>` branch in the main checkout, a subagent's
  `worktree-agent-*` worktree once that agent has returned, a stale worktree from an old session):

  ```bash
  LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../.claude/skills/rebase-commit/land.sh"
  "$LAND" cleanup master [branch]   # branch defaults to the current one
  ```

  It refuses (exit 1, nothing changed) unless the branch is landed: all its commits are on
  `origin/master`, or GitHub has a merged PR from it whose head is exactly the local tip (that
  covers squash and rebase merges, which rewrite the commits). Then it fast-forwards the local
  `master` to `origin/master` (skipped, with a warning, if that checkout has uncommitted tracked
  changes or has diverged). In the main checkout it switches back to the target; in a linked
  worktree it refuses if there are uncommitted or untracked files (ignored ones such as
  `node_modules` and the `.worktreeinclude` copies are fine to lose), then `git worktree remove`s
  it. Last, it deletes the local branch and prunes the remote-tracking ref (GitHub already deleted
  the remote branch on merge). If you ran it from inside the removed worktree, your shell's
  directory is gone: run later commands with absolute paths in the main checkout.

Until the PR is merged, keep the worktree and branch: review feedback lands as new commits on the
same branch (commit, `land.sh rebase`, `land.sh push`; the PR updates itself).

Worktrees that a Claude session holds are locked (`claude session <name> (pid … start …)`).
`cleanup` refuses a lock whose process is still running: only that session may remove its
worktree (ExitWorktree, or "remove" when it exits). A subagent therefore doesn't remove its own
isolated worktree; the parent does it after the agent returns. If the session is gone, the lock is
stale and `cleanup` unlocks it. Never `git worktree remove --force`, and never clean up worktrees
or branches of other features that haven't been merged.

## 8. Report

Report: the PR URL and title, the commits in it (`land.sh push` prints them), whether
`origin/master` had moved and which tests you re-ran, the CI status if you checked it, anything you
left uncommitted, and that the PR is waiting for the user to merge (the worktree stays until
then). After a cleanup, report the worktree and branch you removed (or why you kept them) and
whether the local `master` was fast-forwarded.

## When the user asks you to merge

Only on their request: `gh pr merge <number> --rebase` keeps history linear like the rest of
`master` (or `--squash` if they ask for it; never a merge commit). If checks are still running,
`gh pr merge <number> --rebase --auto` merges once they pass. Then do step 7.
