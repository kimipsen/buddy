---
name: rebase-commit
description: Commit finished work and land it on the target branch (master by default) with a rebase-and-fast-forward workflow (Bitbucket's "Rebase and fast-forward" - git rebase, then merge --ff-only), so history stays linear with no merge commits. Commits on a work branch (creating agent/<topic> if you're on master), rebases it onto the target, re-runs the relevant tests if the target moved, then fast-forwards the target via the bundled land.sh, which handles agent worktrees, a target checked out in another worktree, conflicts and the post-commit doc-sync hook, then removes the landed worktree and deletes its branch. Never pushes unless asked. Use when a task is done and should be committed, and for "commit this", "commit and merge", "land this branch", "rebase onto master and fast-forward", "merge my worktree branch into master", "get this onto master", "clean up the merged worktree". Reviewing a diff is backend-aware-review; production deploys are deploy.
---

# Rebase and fast-forward commit

The user's standing preference: when an agent finishes a task, it commits the work and lands it
on the target branch **without asking first**, using rebase + fast-forward:

```
git rebase <target>          # on the work branch: replay each commit as a new non-merge commit
git merge --ff-only <branch> # on the target: move it forward, never create a merge commit
```

`land.sh` (next to this file) does both halves with the safety checks; use it rather than the raw
commands. That standing permission covers **local** commits and the local fast-forward only.

- **Never push** unless the user asks in this conversation. `origin` has two push URLs (the
  GitHub repo and a mirror), so a push publishes to both.
- Never `git push --force`, `git reset --hard` on the target, `git stash` someone else's changes,
  `--no-verify`, or delete branches you didn't create.
- The target is `master` unless the user names another branch.

## 1. Make sure the task is actually done

Commit only work you'd hand over: the change is complete, and the tests for what you touched pass
(`task test:backend`, `task test:frontend`, a single spec, see CLAUDE.md). If tests fail and you
can't fix them, don't land; report that instead. Also follow the repo's own done-criteria where
they apply (i18n parity, `task docs:openapi` after an API change, screenshots for a visible page
change).

## 2. Get onto a work branch

```bash
git status --short
git branch --show-current
```

- **Agent worktree** (a `worktree-agent-*` or other non-target branch): stay on it.
- **On the target itself** (e.g. `master` in the main checkout): create a branch from where you
  are; uncommitted changes come along: `git switch -c agent/<short-topic>`.

Look at what's in the working tree. Stage only the files that belong to *your* task, by path
(`git add path/a path/b`), never `git add -A` when there are changes you didn't make; mention
the ones you left alone. Never stage `.env` or `appsettings.*.json`.

## 3. Commit

Match the repo's Conventional Commits style (`git log --oneline -15`):
`feat(calendar): sort occurrences by start time, then name`, `fix(...)`, `test(...)`,
`docs(...)`, `refactor(...)`, `chore(...)`. Imperative, lowercase after the colon, scope = the
domain or area. One commit per coherent change; several are fine and each one is replayed by the
rebase. End the message with the attribution trailer from the system reminder, if there is one.

```bash
git commit -F - <<'EOF'
feat(tasks): let guardians reorder a child's tasks

<optional body: why, not what>

Co-Authored-By: <trailer from the system reminder>
EOF
```

Hooks (`task hooks:install`): the **pre-commit** hook runs Prettier/ESLint, i18n parity and C#
whitespace checks on staged files. If it fails, fix what it reports, re-stage and commit again.
The **post-commit** hook may add a follow-up `docs: sync documentation (auto)` commit on your
branch; that's expected and gets landed with the rest. Add `[skip-docs]` to the message only if
the user asked for it.

## 4. Rebase onto the target

```bash
LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../.claude/skills/rebase-commit/land.sh"
"$LAND" rebase master
```

Run it from your work branch's worktree. `$LAND` points at the main checkout's copy, because a
worktree branched before this skill existed doesn't have the file. Shell variables don't survive
between tool calls, so set `LAND` in the same command that uses it. It refuses with exit 1 and changes nothing if the
tree has uncommitted tracked changes, HEAD is detached, you're on the target, or a
rebase/merge is in progress. It sets `SKIP_DOC_AGENT=1`, because git runs the post-commit hook
for every replayed commit and the doc-sync agent must not fire on those.

- Prints `TARGET_MOVED` → the target had new commits, so your code now sits on a base it was never
  tested on. Re-run the tests for the areas you touched (and for anything the new target commits
  changed that your code uses) before step 5. If they fail, fix with a new commit on the branch and
  repeat from step 4.
- **Exit 2: conflict.** The rebase is left in progress. Look at each conflicted file
  (`git diff --name-only --diff-filter=U`):
  - If the resolution is clear (both sides' intent can be kept, e.g. adjacent additions, a renamed
    symbol), resolve it, `git add` the files, then `SKIP_DOC_AGENT=1 git rebase --continue`
    (repeat per commit). Re-run the tests afterwards, as for `TARGET_MOVED`.
  - If it's not clear which side should win, or the conflict is in code you didn't write for
    this task, `git rebase --abort` (the branch is restored exactly) and ask the user.
  - Never resolve by blindly taking `--ours`/`--theirs` for a whole file.

## 5. Fast-forward the target

```bash
"$LAND" finish master
```

It re-checks that the branch sits on the current tip of the target and contains no merge
commits, then moves the target forward:

- target not checked out anywhere → `git update-ref` from the exact tip it checked (no race);
- target checked out in another worktree (usually `master` in the main checkout) →
  `git -C <that worktree> merge --ff-only`, but **only if that worktree has no uncommitted
  tracked changes**. If it has some, the script refuses (exit 1); that's the user's work in
  progress. Tell the user what's ready to land and let them decide. Don't stash or commit
  their changes.

If it says the branch isn't based on the target's tip, someone landed in the meantime: go back to
step 4.

## 6. Clean up the landed branch and its worktree

Once `finish` has landed the branch, its worktree and branch are dead weight: remove them without
asking. Only clean up a branch whose commits are all on the target; never throw away unlanded work.
If `finish` refused, or landing was left to the user, skip this step and keep the worktree.

Pick the case that matches where the branch lives:

- **This session's own worktree, entered with `EnterWorktree`** (or `claude --worktree`, if
  `ExitWorktree` accepts it): call `ExitWorktree` with `action: "remove"`. It deletes the worktree
  and its branch and moves the session back to the main checkout. It refuses if there are
  uncommitted files; never pass `discard_changes: true` without asking the user.
- **Anything else** (an `agent/<topic>` branch in the main checkout, a subagent's
  `worktree-agent-*` worktree once that agent has returned, a stale worktree from an old session):

  ```bash
  LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../.claude/skills/rebase-commit/land.sh"
  "$LAND" cleanup master [branch]   # branch defaults to the current one
  ```

  It refuses (exit 1, nothing changed) unless every commit of the branch is on the target. Then:
  in the main checkout it switches back to the target; in a linked worktree it refuses if there
  are uncommitted or untracked files (ignored ones such as `node_modules` and the
  `.worktreeinclude` copies are fine to lose), then `git worktree remove`s it. Last, it deletes
  the branch with the safe `git branch -d`. If you ran it from inside the removed worktree, your
  shell's directory is gone: run later commands with absolute paths in the main checkout.

Worktrees that a Claude session holds are locked (`claude session <name> (pid … start …)`).
`cleanup` refuses a lock whose process is still running: only that session may remove its
worktree (ExitWorktree, or "remove" when it exits). A subagent therefore doesn't remove its own
isolated worktree; the parent does it after the agent returns. If the session is gone, the lock is
stale and `cleanup` unlocks it. Never `git worktree remove --force`, and never clean up worktrees
or branches of other features that haven't landed.

## 7. Report

Report: the commits that landed (`land.sh` prints them), the target branch, whether the target
had moved and which tests you re-ran, the worktree and branch you removed (or why you kept them),
anything you left uncommitted, and that nothing was pushed. Offer to push if that seems wanted.

## When the user asks to push

Only after landing, and only on their request: `git push origin master`. If it's rejected, the
remote has commits the local target lacks. Stop and tell the user; never force-push. Pushing a work branch to open a PR instead:
`git push -u origin <branch>`.
