#!/usr/bin/env bash
# Lands the current branch on a target branch with "rebase + merge --ff-only"
# (Bitbucket's "Rebase and fast-forward"): every commit is replayed onto the
# target as a new non-merge commit, and the target only ever fast-forwards.
#
#   land.sh rebase [target]   rebase the current branch onto target
#   land.sh finish [target]   fast-forward target to the current branch
#
# target defaults to master. Run both from the worktree that has the source
# branch checked out. Nothing is pushed.
#
# Exit codes: 0 done, 1 refused (nothing changed), 2 rebase stopped on a
# conflict (the rebase is left in progress for you to resolve or abort).
set -euo pipefail

# Replayed commits are not new work: keep the post-commit doc-sync agent
# (.devcontainer/git-hooks/post-commit) from firing once per picked commit.
export SKIP_DOC_AGENT=1

die() {
  echo "land.sh: $*" >&2
  exit 1
}

usage() {
  sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

cmd="${1:-}"
target="${2:-master}"
[[ "$cmd" == "rebase" || "$cmd" == "finish" ]] || usage

git rev-parse --is-inside-work-tree >/dev/null 2>&1 || die "not inside a git work tree"
git_dir="$(git rev-parse --git-dir)"
[[ ! -d "$git_dir/rebase-merge" && ! -d "$git_dir/rebase-apply" ]] ||
  die "a rebase is already in progress here; finish it (SKIP_DOC_AGENT=1 git rebase --continue) or abort it (git rebase --abort) first"
[[ ! -f "$git_dir/MERGE_HEAD" ]] || die "a merge is in progress here; resolve or abort it first"

source_branch="$(git symbolic-ref --quiet --short HEAD)" || die "HEAD is detached; check out the branch you want to land"
[[ "$source_branch" != "$target" ]] ||
  die "you are on '$target' itself; commit on a separate branch (git switch -c agent/<topic>) and land that"
git show-ref --verify --quiet "refs/heads/$target" || die "target branch '$target' does not exist locally"

if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
  git status --short --untracked-files=no >&2
  die "uncommitted changes to tracked files on '$source_branch'; commit or drop them first"
fi

# Prints the work tree that has the given branch checked out, if any.
worktree_of() {
  git worktree list --porcelain | awk -v ref="refs/heads/$1" '
    /^worktree / { wt = substr($0, 10) }
    $0 == "branch " ref { print wt; exit }'
}

case "$cmd" in
  rebase)
    old_base="$(git merge-base "$target" HEAD)"
    if [[ "$old_base" == "$(git rev-parse "$target")" ]]; then
      echo "'$source_branch' is already based on the tip of '$target'; nothing to rebase."
    else
      echo "Rebasing '$source_branch' onto '$target' ($(git rev-list --count "$old_base..$target") new commit(s) on '$target')..."
      if ! git rebase "$target"; then
        echo >&2
        echo "land.sh: the rebase stopped on a conflict. Conflicted files:" >&2
        git diff --name-only --diff-filter=U >&2 || true
        echo "Resolve, 'git add' them, then: SKIP_DOC_AGENT=1 git rebase --continue" >&2
        echo "Or give up and restore the branch: git rebase --abort" >&2
        exit 2
      fi
      echo "TARGET_MOVED: '$target' had new commits; re-run the tests for what you touched before 'finish'."
    fi
    ;;

  finish)
    git merge-base --is-ancestor "$target" HEAD ||
      die "'$source_branch' is not based on the tip of '$target' (it moved, or you never rebased); run 'land.sh rebase $target' first"

    merges="$(git rev-list --merges "$target..HEAD")"
    [[ -z "$merges" ]] || die "merge commits between '$target' and '$source_branch' ($merges); rebase again to linearize"

    count="$(git rev-list --count "$target..HEAD")"
    if [[ "$count" == "0" ]]; then
      echo "'$target' already contains '$source_branch'; nothing to land."
      exit 0
    fi
    new_tip="$(git rev-parse HEAD)"
    old_tip="$(git rev-parse "$target")"

    target_wt="$(worktree_of "$target")"
    if [[ -z "$target_wt" ]]; then
      # Not checked out anywhere: move the ref, but only from the tip we checked.
      git update-ref -m "land.sh: fast-forward $target to $source_branch" \
        "refs/heads/$target" "$new_tip" "$old_tip"
    else
      if [[ -n "$(git -C "$target_wt" status --porcelain --untracked-files=no)" ]]; then
        git -C "$target_wt" status --short --untracked-files=no >&2
        die "'$target' is checked out in $target_wt with uncommitted changes; not touching someone else's work tree. Ask the user."
      fi
      git -C "$target_wt" merge --ff-only "$new_tip"
    fi

    echo "Landed $count commit(s) from '$source_branch' on '$target':"
    git log --oneline "$old_tip..$new_tip"
    ;;
esac
