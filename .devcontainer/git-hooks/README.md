# Documentation-sync git hook

A `post-commit` hook that asks an AI coding agent to update `docs/` and
`README.md` based on the diff of the commit that was just made, then commits
any resulting doc changes as a separate `docs: sync documentation (auto)`
commit.

## Install

```bash
task hooks:install AGENT=claude   # or: codex, copilot
```

This points git at this directory (`git config core.hooksPath
.devcontainer/git-hooks`) and records the chosen agent (`git config
hooks.doc-agent <agent>`). Both settings are local to your clone (stored in
`.git/config`, not committed) — everyone on the team picks their own agent.

```bash
task hooks:uninstall
```

removes both settings and restores git's default hook behavior.

## What runs

`post-commit` sources [`lib/doc-agent.sh`](lib/doc-agent.sh), which:

1. Skips the very first commit in the repo (nothing to diff against), the
   hook's own auto-commits, and any commit whose message contains
   `[skip-docs]`, or if `SKIP_DOC_AGENT=1` is set in the environment.
2. Reads the configured agent from `hooks.doc-agent` and exits quietly if it
   isn't installed on `PATH`.
3. Builds a prompt from the last commit's message and diff, restricted to
   updating files under `docs/` and `README.md`.
4. Runs the agent non-interactively, restricted to read/edit-style tools (no
   arbitrary shell commands):
   - `claude -p ... --permission-mode acceptEdits --allowedTools Read,Edit,Glob,Grep`
   - `codex exec ... --sandbox workspace-write`
   - `copilot -p ... --allow-tool write --deny-tool shell`
5. If the agent changed anything under `docs/` or `README.md`, commits those
   changes as `docs: sync documentation (auto)`.

If the diff doesn't touch documented behavior, the agent is expected to make
no changes and no extra commit is created.

## Requirements

The chosen agent's CLI must be installed and authenticated in the dev
container: `claude`, `codex`, and `copilot` are separate tools with their own
login flows. If the CLI is missing, the hook logs a warning and skips the doc
update — it never blocks or fails your commit.

## Skipping it for one commit

```bash
git commit -m "wip: quick fix [skip-docs]"
# or
SKIP_DOC_AGENT=1 git commit -m "wip: quick fix"
```
