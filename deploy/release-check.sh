#!/usr/bin/env bash
# Warns (never fails) when the commit being deployed doesn't contain the newest
# release tag, so the operator learns about a release at the moment they can
# take it. Called by preflight.sh (`task deploy`) and azure/deploy.sh. Nothing
# is sent from the running installation; see
# docs/backend/analysis/update-check.md.
#
# Fetches tags from the default remote first. A failed fetch (offline, no SSH
# key on the VM, a clobbered local tag) falls back to the local tags.
# Pre-release tags (v1.3.0-rc.1) are ignored.
#
# Usage: deploy/release-check.sh [repository-url]
#   repository-url  REPOSITORY_URL from .env, used for the release-notes link;
#                   blank means https://github.com/kimipsen/buddy.
set -uo pipefail

cd "$(dirname "$0")/.."

repository_url=${1:-}
repository_url=${repository_url:-https://github.com/kimipsen/buddy}

warn() { echo "release check: $*" >&2; }

# No credential or passphrase prompts: a deploy shouldn't stop to ask. BatchMode
# is only added when the operator hasn't configured their own ssh command.
export GIT_TERMINAL_PROMPT=0
if [[ -z "${GIT_SSH_COMMAND:-}" && -z "$(git config core.sshCommand)" ]]; then
  export GIT_SSH_COMMAND="ssh -o BatchMode=yes"
fi
fetch=(git fetch --tags --quiet)
command -v timeout >/dev/null && fetch=(timeout 30 "${fetch[@]}")
if ! "${fetch[@]}" 2>/dev/null; then
  warn "couldn't fetch tags from the remote; comparing against local tags only."
fi

latest=$(git tag --list 'v[0-9]*.[0-9]*.[0-9]*' --sort=-v:refname | grep -v -- '-' | head -n1)

if [[ -z "$latest" ]]; then
  exit 0
fi

if git merge-base --is-ancestor "$latest" HEAD; then
  echo "release check: ok (includes $latest)"
  exit 0
fi

warn "WARNING: a newer release exists. You're deploying $("$PWD/deploy/version.sh"), but $latest isn't in it."
warn "  Release notes: ${repository_url%/}/releases/tag/$latest"
warn "  To deploy it: git checkout $latest (or merge it into your branch), then deploy again."
warn "  Read the notes first: events don't roll back (deploy/README.md, step 8)."
exit 0
