# Rebase commit

Copilot skill package for committing finished work and landing it on the target branch (master by default) through a GitHub pull request: commit on a work branch, rebase it onto `origin/master`, re-test if master moved, push the branch and open the PR with `gh`. The user merges the PR; afterwards the package syncs the local master and removes the branch and its worktree. Never merges unless asked.

Files:

- `SKILL.md` — commit conventions, the rebase/push/PR steps, the PR title and body format, conflict handling, post-merge cleanup, and what to report.
- `manifest.json` — skill metadata.
- `land.sh` — `land.sh rebase [target]` (onto the fetched `origin/<target>`), `land.sh push [target]` (`--force-with-lease` to `origin`) and `land.sh cleanup [target] [branch]` (once the PR is merged); refuses on a dirty tree, detached HEAD, a branch not based on the remote tip, or an unmerged branch, and skips the doc-sync hook while replaying commits.
