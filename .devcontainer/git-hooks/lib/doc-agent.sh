#!/usr/bin/env bash
# Shared logic for the post-commit documentation-sync hook.
# Invoked by .devcontainer/git-hooks/post-commit.

DOC_AGENT_COMMIT_PREFIX="docs: sync documentation (auto)"
DOC_AGENT_SKIP_MARKER="[skip-docs]"

doc_agent_build_prompt() {
  local prompt_file="$1" commit_msg="$2" diff="$3"
  cat >"$prompt_file" <<PROMPT
You are updating project documentation to stay in sync with a git commit that
was just made in this repository.

Documentation lives under docs/ (docs/backend/<domain>/flow.md,
docs/backend/<domain>/analysis/*.md or docs/backend/analysis/*.md,
docs/frontend/, docs/frontend/analysis/*.md) and the root README.md.

Review the diff below and update only the documentation files that describe
behavior this commit changed: new/changed API endpoints, domain flows, UI
screens, config, or scripts. Do not touch source code. Match the existing
structure and tone of nearby docs; only add a new doc file if the change
clearly needs one and no existing file covers it. Keep edits minimal and
factual.

If nothing in the diff affects documented behavior, make no changes.

Commit message:
$commit_msg

Diff:
$diff
PROMPT
}

doc_agent_run() {
  local repo_root commit_msg agent diff prompt_file instruction

  repo_root="$(git rev-parse --show-toplevel)"
  cd "$repo_root" || return 0

  # Nothing to diff against on the very first commit in the repo.
  if ! git rev-parse --verify -q HEAD~1 >/dev/null; then
    return 0
  fi

  commit_msg="$(git log -1 --pretty=%B)"

  # Guard against recursion: never react to our own doc-sync commit, and let
  # callers opt out per-commit (message marker) or globally (env var).
  case "$commit_msg" in
    "$DOC_AGENT_COMMIT_PREFIX"*) return 0 ;;
    *"$DOC_AGENT_SKIP_MARKER"*) return 0 ;;
  esac
  if [[ "${SKIP_DOC_AGENT:-}" == "1" ]]; then
    return 0
  fi

  agent="$(git config --get hooks.doc-agent || true)"
  if [[ -z "$agent" ]]; then
    return 0
  fi

  if ! command -v "$agent" >/dev/null 2>&1; then
    echo "post-commit: doc agent '$agent' is not installed — skipping documentation update." >&2
    return 0
  fi

  diff="$(git diff HEAD~1..HEAD)"
  if [[ -z "$diff" ]]; then
    return 0
  fi

  # Large commits can produce diffs that exceed the OS argument-length limit,
  # so the prompt (which embeds the full diff) is written to a temp file
  # instead of being passed inline as a CLI argument.
  prompt_file="$(mktemp -t doc-agent-prompt.XXXXXX)"
  trap 'rm -f "$prompt_file"' RETURN
  doc_agent_build_prompt "$prompt_file" "$commit_msg" "$diff"

  echo "post-commit: running $agent to check documentation for the last commit..."

  case "$agent" in
    claude)
      instruction="Read the file at $prompt_file — it contains the commit message, diff, and instructions for updating documentation. Follow those instructions exactly, then stop."
      claude -p "$instruction" \
        --permission-mode acceptEdits \
        --allowedTools "Read,Edit,Glob,Grep" \
        --add-dir "$repo_root" \
        --add-dir "$(dirname "$prompt_file")" \
        >/dev/null
      ;;
    codex)
      # codex exec reads its instructions from stdin when no prompt argument
      # (or "-") is given, so the prompt file is streamed in rather than
      # passed as an argument.
      codex exec - \
        --sandbox workspace-write \
        --skip-git-repo-check \
        -C "$repo_root" \
        <"$prompt_file" \
        >/dev/null
      ;;
    copilot)
      instruction="Read the file at $prompt_file — it contains the commit message, diff, and instructions for updating documentation. Follow those instructions exactly, then stop."
      copilot -p "$instruction" -s \
        --allow-tool write \
        --deny-tool shell \
        --add-dir "$(dirname "$prompt_file")" \
        >/dev/null
      ;;
    *)
      echo "post-commit: unknown doc agent '$agent' (expected claude, codex, or copilot) — skipping." >&2
      return 0
      ;;
  esac

  local doc_paths=()
  [[ -e docs ]] && doc_paths+=(docs)
  [[ -e README.md ]] && doc_paths+=(README.md)
  if [[ ${#doc_paths[@]} -eq 0 ]]; then
    return 0
  fi

  if ! git diff --quiet -- "${doc_paths[@]}"; then
    git add -- "${doc_paths[@]}"
    git commit -m "$DOC_AGENT_COMMIT_PREFIX" >/dev/null
    echo "post-commit: documentation updated and committed."
  fi
}
