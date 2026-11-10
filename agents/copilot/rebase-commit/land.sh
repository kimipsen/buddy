#!/usr/bin/env bash
# Lands the current branch on a target branch through a GitHub pull request:
# rebase it onto the freshly fetched origin/<target>, push it, open a PR (with
# gh, see SKILL.md), and once the PR is merged on GitHub, sync the local target
# and remove the branch and its worktree.
#
#   land.sh rebase [target]   fetch origin, rebase the current branch onto
#                             origin/<target>
#   land.sh push [target]     push the current branch to origin (with
#                             --force-with-lease, since a rebase rewrites it)
#   land.sh cleanup [target] [branch]
#                             once branch (default: the current one) is merged
#                             on GitHub, fast-forward the local target, remove
#                             the branch's worktree and delete the branch
#
# target defaults to master. Run rebase and push from the worktree that has
# the work branch checked out; cleanup runs from anywhere.
#
# Exit codes: 0 done, 1 refused (nothing changed), 2 rebase stopped on a
# conflict (the rebase is left in progress for you to resolve or abort).
set -euo pipefail

# Replayed commits are not new work: keep the post-commit doc-sync agent
# (.devcontainer/git-hooks/post-commit) from firing once per picked commit.
export SKIP_DOC_AGENT=1

remote=origin

die() {
  echo "land.sh: $*" >&2
  exit 1
}

usage() {
  sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

cmd="${1:-}"
target="${2:-master}"
[[ "$cmd" == "rebase" || "$cmd" == "push" || "$cmd" == "cleanup" ]] || usage

git rev-parse --is-inside-work-tree >/dev/null 2>&1 || die "not inside a git work tree"

fetch_target() {
  git fetch --quiet "$remote" "$target" || die "could not fetch '$target' from $remote"
  git show-ref --verify --quiet "refs/remotes/$remote/$target" || die "no remote branch '$remote/$target'"
}

# Prints the work tree that has the given branch checked out, if any.
worktree_of() {
  git worktree list --porcelain | awk -v ref="refs/heads/$1" '
    /^worktree / { wt = substr($0, 10) }
    $0 == "branch " ref { print wt; exit }'
}

# Prints the lock reason of the given work tree, if it is locked.
lock_of() {
  git worktree list --porcelain | awk -v wt="$1" '
    /^worktree / { cur = substr($0, 10) }
    cur == wt && /^locked/ { print (length($0) > 7 ? substr($0, 8) : "(no reason given)"); exit }'
}

# True when a "claude session <name> (pid N start S)" lock belongs to a process
# that is still running (same pid and start time, so a reused pid doesn't count).
# Any other lock counts as live.
lock_is_live() {
  local pid start
  [[ "$1" =~ \(pid\ ([0-9]+)\ start\ ([0-9]+)\) ]] || return 0
  pid="${BASH_REMATCH[1]}"
  start="${BASH_REMATCH[2]}"
  [[ -r "/proc/$pid/stat" ]] || return 1
  [[ "$(sed 's/^.*) //' "/proc/$pid/stat" | awk '{ print $20 }')" == "$start" ]]
}

if [[ "$cmd" == "cleanup" ]]; then
  branch="${3:-$(git symbolic-ref --quiet --short HEAD || true)}"
  [[ -n "$branch" ]] || die "HEAD is detached; name the branch: land.sh cleanup $target <branch>"
  [[ "$branch" != "$target" ]] || die "refusing to clean up '$target' itself"
  git show-ref --verify --quiet "refs/heads/$branch" || die "no local branch '$branch'"
  fetch_target

  main_wt="$(git worktree list --porcelain | awk '/^worktree / { print substr($0, 10); exit }')"
  tip="$(git rev-parse "refs/heads/$branch")"

  # Safe to delete when every commit is on origin/<target> (a fast-forward or a
  # plain rebase merge), or when GitHub says the PR from this branch was merged
  # with exactly this tip as its head (a squash merge or a rebase merge that
  # rewrote the commits). Either way no local-only work is lost.
  merged=""
  if git merge-base --is-ancestor "$tip" "refs/remotes/$remote/$target"; then
    merged="its commits are on $remote/$target"
  elif command -v gh >/dev/null 2>&1; then
    pr="$(gh pr list --head "$branch" --base "$target" --state merged --limit 1 \
      --json number,headRefOid --jq '.[0] | select(. != null) | "\(.number) \(.headRefOid)"' 2>/dev/null || true)"
    if [[ -n "$pr" ]]; then
      [[ "${pr#* }" == "$tip" ]] ||
        die "PR #${pr%% *} from '$branch' was merged, but the local branch has commits that weren't in it ($tip vs ${pr#* }); push them in a new PR or ask the user"
      merged="PR #${pr%% *} is merged"
    fi
  fi
  [[ -n "$merged" ]] ||
    die "'$branch' is not merged into $remote/$target yet (no merged PR from it either); wait for the PR to be merged, or ask the user before throwing it away"
  echo "'$branch' is landed: $merged."

  # Bring the local target up to date, but never over someone's uncommitted work.
  target_wt="$(worktree_of "$target")"
  if [[ -z "$target_wt" ]]; then
    if git merge-base --is-ancestor "refs/heads/$target" "refs/remotes/$remote/$target"; then
      git update-ref -m "land.sh: sync $target with $remote/$target" \
        "refs/heads/$target" "$(git rev-parse "refs/remotes/$remote/$target")"
    else
      echo "land.sh: local '$target' has commits that aren't on $remote/$target; left it alone." >&2
    fi
  elif [[ -n "$(git -C "$target_wt" status --porcelain --untracked-files=no)" ]]; then
    echo "land.sh: '$target' in $target_wt has uncommitted changes; didn't fast-forward it to $remote/$target." >&2
  elif ! git -C "$target_wt" merge --ff-only --quiet "refs/remotes/$remote/$target"; then
    echo "land.sh: local '$target' has diverged from $remote/$target; left it alone. Ask the user." >&2
  else
    echo "Fast-forwarded '$target' in $target_wt to $remote/$target."
  fi

  wt="$(worktree_of "$branch")"
  if [[ -n "$wt" && "$wt" == "$main_wt" ]]; then
    # A work branch created in the main checkout (agent/<topic>): go back to the target.
    [[ -z "$(git -C "$wt" status --porcelain --untracked-files=no)" ]] ||
      die "uncommitted changes to tracked files in $wt; commit or drop them first"
    other="$(worktree_of "$target")"
    [[ -z "$other" ]] ||
      die "'$target' is checked out in $other, so $wt can't switch to it; switch $wt to another branch yourself, then delete '$branch'"
    git -C "$wt" switch "$target"
    git -C "$wt" merge --ff-only --quiet "refs/remotes/$remote/$target" || true
  elif [[ -n "$wt" ]]; then
    dirty="$(git -C "$wt" status --porcelain)"
    if [[ -n "$dirty" ]]; then
      echo "$dirty" >&2
      die "uncommitted or untracked files in $wt would be lost; commit, move or drop them first (ignored files such as node_modules are fine)"
    fi
    lock="$(lock_of "$wt")"
    if [[ -n "$lock" ]]; then
      lock_is_live "$lock" &&
        die "$wt is locked: $lock. Only that session may remove it: ExitWorktree with action \"remove\" from inside it, or \"remove\" when the session exits"
      echo "Unlocking $wt: its lock belongs to a session that is no longer running ($lock)."
      git worktree unlock "$wt"
    fi
    git -C "$main_wt" worktree remove "$wt"
    echo "Removed the worktree $wt."
    [[ "$PWD/" != "$wt/"* ]] || echo "Your shell was inside it; continue from $main_wt."
  fi

  # -D because a squash or rewriting rebase merge leaves the local commits
  # unmerged as far as git can tell; the checks above proved they are landed.
  git -C "$main_wt" branch -D "$branch"
  # GitHub deletes the head branch on merge; drop our stale tracking ref too.
  git -C "$main_wt" fetch --quiet --prune "$remote" || true
  exit 0
fi

git_dir="$(git rev-parse --git-dir)"
[[ ! -d "$git_dir/rebase-merge" && ! -d "$git_dir/rebase-apply" ]] ||
  die "a rebase is already in progress here; finish it (SKIP_DOC_AGENT=1 git rebase --continue) or abort it (git rebase --abort) first"
[[ ! -f "$git_dir/MERGE_HEAD" ]] || die "a merge is in progress here; resolve or abort it first"

source_branch="$(git symbolic-ref --quiet --short HEAD)" || die "HEAD is detached; check out the branch you want to land"
[[ "$source_branch" != "$target" ]] ||
  die "you are on '$target' itself; commit on a separate branch (git switch -c agent/<topic>) and land that"

if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
  git status --short --untracked-files=no >&2
  die "uncommitted changes to tracked files on '$source_branch'; commit or drop them first"
fi

fetch_target
upstream="refs/remotes/$remote/$target"

case "$cmd" in
  rebase)
    old_base="$(git merge-base "$upstream" HEAD)"
    if [[ "$old_base" == "$(git rev-parse "$upstream")" ]]; then
      echo "'$source_branch' is already based on the tip of $remote/$target; nothing to rebase."
    else
      echo "Rebasing '$source_branch' onto $remote/$target ($(git rev-list --count "$old_base..$upstream") new commit(s) there)..."
      if ! git rebase "$upstream"; then
        echo >&2
        echo "land.sh: the rebase stopped on a conflict. Conflicted files:" >&2
        git diff --name-only --diff-filter=U >&2 || true
        echo "Resolve, 'git add' them, then: SKIP_DOC_AGENT=1 git rebase --continue" >&2
        echo "Or give up and restore the branch: git rebase --abort" >&2
        exit 2
      fi
      echo "TARGET_MOVED: $remote/$target had new commits; re-run the tests for what you touched before 'push'."
    fi
    ;;

  push)
    git merge-base --is-ancestor "$upstream" HEAD ||
      die "'$source_branch' is not based on the tip of $remote/$target (it moved, or you never rebased); run 'land.sh rebase $target' first"

    merges="$(git rev-list --merges "$upstream..HEAD")"
    [[ -z "$merges" ]] || die "merge commits between $remote/$target and '$source_branch' ($merges); rebase again to linearize"

    count="$(git rev-list --count "$upstream..HEAD")"
    [[ "$count" != "0" ]] || die "$remote/$target already contains '$source_branch'; nothing to push"

    # --force-with-lease: a rebase rewrites the branch, but never overwrite
    # commits someone else pushed to it since our last fetch of it. Pin the
    # expected value (empty: the branch must not exist yet) before pushing:
    # origin has two push URLs, and after the first one git updates the
    # tracking ref, so an unpinned lease would reject the second as stale.
    expected="$(git rev-parse --verify --quiet "refs/remotes/$remote/$source_branch" || true)"
    git push --force-with-lease="$source_branch:$expected" -u "$remote" "$source_branch"
    echo "Pushed $count commit(s) from '$source_branch' (base: $remote/$target):"
    git log --oneline "$upstream..HEAD"
    ;;
esac
