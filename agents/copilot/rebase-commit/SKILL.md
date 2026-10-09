
# Rebase and fast-forward commit

The user's standing preference: when an agent finishes a task, it commits the work and lands it
on the target branch **without asking first**, using rebase + fast-forward:

```
git rebase <target>          # on the work branch: replay each commit as a new non-merge commit
git merge --ff-only <branch> # on the target: move it forward, never create a merge commit
```

`land.sh` (next to this file) does both halves with the safety checks; use it rather than the raw
commands. That standing permission covers **local** commits and the local fast-forward only.

- **Never push** unless the user asks in this conversation. `origin` has two push URLs (personal
  GitHub and the Knowit mirror), so a push publishes to both.
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
- **On the target itself** (e.g. `master` in `/workspaces/kim`): create a branch from where you
  are; uncommitted changes come along: `git switch -c agent/<short-topic>`.

Look at what's in the working tree. Stage only the files that belong to *your* task, by path
(`git add path/a path/b`), never `git add -A` when there are changes you didn't make; mention
the ones you left alone. Never stage `.env` or `appsettings.*.json`.

## 3. Commit

Match the repo's Conventional Commits style (`git log --oneline -15`):
`feat(calendar): sort occurrences by start time, then name`, `fix(...)`, `test(...)`,
`docs(...)`, `refactor(...)`, `chore(...)`. Imperative, lowercase after the colon, scope = the
domain or area. One commit per coherent change; several are fine and each one is replayed by the
rebase. End the message with your tool's attribution trailer, if it uses one.

```bash
git commit -F - <<'EOF'
feat(tasks): let guardians reorder a child's tasks

<optional body: why, not what>

<attribution trailer, if any>
EOF
```

Hooks (`task hooks:install`): the **pre-commit** hook runs Prettier/ESLint, i18n parity and C#
whitespace checks on staged files. If it fails, fix what it reports, re-stage and commit again.
The **post-commit** hook may add a follow-up `docs: sync documentation (auto)` commit on your
branch; that's expected and gets landed with the rest. Add `[skip-docs]` to the message only if
the user asked for it.

## 4. Rebase onto the target

```bash
LAND="$(git rev-parse --path-format=absolute --git-common-dir)/../agents/copilot/rebase-commit/land.sh"
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
- target checked out in another worktree (usually `master` in `/workspaces/kim`) →
  `git -C <that worktree> merge --ff-only`, but **only if that worktree has no uncommitted
  tracked changes**. If it has some, the script refuses (exit 1); that's the user's work in
  progress. Tell the user what's ready to land and let them decide. Don't stash or commit
  their changes.

If it says the branch isn't based on the target's tip, someone landed in the meantime: go back to
step 4.

## 6. Clean up and report

- If you created `agent/<topic>` in the main worktree (step 2), switch back and delete it; it's
  fully merged, so the safe delete works: `git switch master && git branch -d agent/<topic>`.
- Leave `worktree-agent-*` branches and worktrees alone; the harness manages them.
- Report: the commits that landed (`land.sh` prints them), the target branch, whether the target
  had moved and which tests you re-ran, anything you left uncommitted, and that nothing was pushed.
  Offer to push if that seems wanted.

## When the user asks to push

Only after landing, and only on their request: `git push origin master`. If it's rejected, the
remote has commits the local target lacks. Stop and tell the user; never force-push. Pushing a work branch to open a PR instead:
`git push -u origin <branch>`.
