# Rebase commit

Copilot skill package for committing finished work and landing it on the target branch (master by default) with rebase + fast-forward: commit on a work branch, rebase it onto the target, re-test if the target moved, then fast-forward the target. Never pushes unless asked.

Files:
- `SKILL.md` — commit conventions, the rebase/fast-forward steps, conflict handling, and what to report.
- `manifest.json` — skill metadata.
- `land.sh` — `land.sh rebase [target]` / `land.sh finish [target]`; refuses on a dirty tree, detached HEAD, or a target checked out with uncommitted changes, and skips the doc-sync hook while replaying commits.
