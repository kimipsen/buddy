#!/usr/bin/env bash
# Runs frontend mutation testing a few files at a time, so neither the
# devcontainer nor a CI job has to survive one huge Stryker run.
#
# Each batch only runs the specs that belong to its files: the colocated
# `foo.spec.ts` plus every spec that imports `foo` directly (passed to
# `ng test --include` through MUTATION_SPECS, see stryker.conf.json). That is
# far cheaper than the whole suite per mutant. A mutant only some unrelated
# spec would kill shows up as Survived; that's the spec to strengthen anyway.
# A file no spec imports is run alone against the whole suite.
#
# Progress is tracked across invocations, so you can just keep re-running
# this script (e.g. `task test:mutation:frontend:batch`) and it will work
# through every mutable file a few at a time. Each batch is run with
# `--incremental`, so results accumulate into reports/stryker-incremental.json
# as you go. Once every file has been covered, that file is a full baseline
# you can commit and reuse for incremental/diff-based runs.
#
# Usage:
#   scripts/mutation-batch.sh [--batch-size N] [--concurrency N] [--max-minutes N]
#                             [--rerun-changed] [--status] [--reset] [--dry-run]
#
#   --max-minutes N   Keep running batches until N minutes have passed. A batch
#                     still running at the deadline is stopped and not recorded,
#                     so it's picked up again next time. CI uses this to stay
#                     inside the job timeout and keep what it finished.
#   --rerun-changed   Also re-run files whose source or specs changed since
#                     they were last covered (CI's nightly mode). Without it a
#                     covered file stays covered until --reset.
#
# Env vars (overridden by flags): BATCH_SIZE, CONCURRENCY
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

BATCH_SIZE="${BATCH_SIZE:-5}"
CONCURRENCY="${CONCURRENCY:-1}"
MAX_MINUTES=""
RERUN_CHANGED=0
PROGRESS_FILE=".mutation-batch-progress"
INCREMENTAL_FILE="reports/stryker-incremental.json"
MODE="run"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --batch-size)
      BATCH_SIZE="$2"
      shift 2
      ;;
    --concurrency)
      CONCURRENCY="$2"
      shift 2
      ;;
    --max-minutes)
      MAX_MINUTES="$2"
      shift 2
      ;;
    --rerun-changed)
      RERUN_CHANGED=1
      shift
      ;;
    --status)
      MODE="status"
      shift
      ;;
    --reset)
      MODE="reset"
      shift
      ;;
    --dry-run)
      MODE="dry-run"
      shift
      ;;
    -h|--help)
      grep '^#' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      exit 1
      ;;
  esac
done

if ! [[ "$BATCH_SIZE" =~ ^[0-9]+$ ]] || [[ "$BATCH_SIZE" -lt 1 ]]; then
  echo "--batch-size must be a positive integer (got '$BATCH_SIZE')" >&2
  exit 1
fi
if [[ -n "$MAX_MINUTES" ]] && { ! [[ "$MAX_MINUTES" =~ ^[0-9]+$ ]] || [[ "$MAX_MINUTES" -lt 1 ]]; }; then
  echo "--max-minutes must be a positive integer (got '$MAX_MINUTES')" >&2
  exit 1
fi

if [[ "$MODE" == "reset" ]]; then
  rm -f "$PROGRESS_FILE"
  echo "Progress reset. Next run starts from the beginning."
  exit 0
fi

# Same file set Stryker itself mutates (see stryker.conf.json's "mutate" glob).
mapfile -t all_files < <(find src/app -type f -name '*.ts' ! -name '*.spec.ts' ! -path 'src/app/core/i18n/translations/*' ! -path src/app/core/api/buddy-api.ts | sort)
mapfile -t all_specs < <(find src/app -type f -name '*.spec.ts' | sort)
total="${#all_files[@]}"

# Files among the given ones that import $1 by path (`.../foo'` or
# `.../foo.ts'`). A shared basename can match an extra file; that only costs
# time.
importers_of() {
  local file="$1" base
  shift
  base="$(basename "$file" .ts)"
  grep -lE "from '[^']*/${base//./\\.}(\\.ts)?'" "$@" | grep -vxF "$file" || true
}

# Its colocated spec plus every spec importing it.
direct_specs() {
  local file="$1" colocated="${1%.ts}.spec.ts"
  { [[ -f "$colocated" ]] && echo "$colocated"; importers_of "$file" "${all_specs[@]}"; } | sort -u
}

# The specs that exercise a source file: its direct specs, or else (a child
# component, a pipe used in a template) the direct specs of the files that
# import it.
declare -A specs_cache=()
specs_for() {
  local file="$1"
  if [[ -z "${specs_cache[$file]+set}" ]]; then
    local found importer
    found="$(direct_specs "$file")"
    if [[ -z "$found" ]]; then
      found="$(while IFS= read -r importer; do
        [[ -n "$importer" ]] && direct_specs "$importer"
      done < <(importers_of "$file" "${all_files[@]}") | sort -u)"
    fi
    specs_cache[$file]="$(echo $found)"
  fi
  echo "${specs_cache[$file]}"
}

# Fingerprint of a file and its specs; a change to either re-runs the file
# under --rerun-changed.
fingerprint() {
  local file="$1" specs
  specs="$(specs_for "$file")"
  # shellcheck disable=SC2086 # word-splitting the spec list is intended
  cat "$file" $specs | sha1sum | cut -c1-12
}

# Progress lines are `path fingerprint`; older lines with only a path count
# as covered, with an unknown fingerprint.
declare -A done_hash=()
load_progress() {
  done_hash=()
  touch "$PROGRESS_FILE"
  local path hash
  while read -r path hash; do
    [[ -n "$path" ]] && done_hash["$path"]="${hash:--}"
  done < "$PROGRESS_FILE"
}

# Records files as covered, replacing any earlier line for them.
record_progress() {
  local tmp f
  tmp="$(mktemp)"
  awk 'NR == FNR { drop[$0] = 1; next } !drop[$1]' <(printf '%s\n' "$@") "$PROGRESS_FILE" > "$tmp"
  for f in "$@"; do
    echo "$f $(fingerprint "$f")" >> "$tmp"
  done
  mv "$tmp" "$PROGRESS_FILE"
}

compute_remaining() {
  remaining=()
  local f
  for f in "${all_files[@]}"; do
    if [[ -z "${done_hash[$f]:-}" ]]; then
      remaining+=("$f")
    elif [[ "$RERUN_CHANGED" -eq 1 && "${done_hash[$f]}" != "$(fingerprint "$f")" ]]; then
      remaining+=("$f")
    fi
  done
  done_count=$(( total - ${#remaining[@]} ))
}

# Fills `batch` and `batch_specs` from the front of `remaining`. A file
# without specs gets a batch of its own, run against the whole suite
# (empty batch_specs).
next_batch() {
  batch=()
  batch_specs=()
  local f specs
  declare -A seen=()
  for f in "${remaining[@]}"; do
    [[ "${#batch[@]}" -ge "$BATCH_SIZE" ]] && break
    specs="$(specs_for "$f")"
    if [[ -z "$specs" ]]; then
      if [[ "${#batch[@]}" -eq 0 ]]; then
        batch=("$f")
      fi
      break
    fi
    batch+=("$f")
    for s in $specs; do
      [[ -z "${seen[$s]:-}" ]] && seen[$s]=1 && batch_specs+=("$s")
    done
  done
}

# Drops the batch's files from the incremental report, so they're re-tested
# rather than reused: the command runner can't tell Stryker which tests
# changed, so a changed spec alone wouldn't invalidate old results.
forget_results() {
  [[ -f "$INCREMENTAL_FILE" ]] || return 0
  node -e '
    const fs = require("node:fs");
    const [path, ...files] = process.argv.slice(1);
    const report = JSON.parse(fs.readFileSync(path, "utf8"));
    for (const f of files) delete report.files[f];
    fs.writeFileSync(path, JSON.stringify(report, null, 2));
  ' "$INCREMENTAL_FILE" "$@"
}

load_progress
compute_remaining

if [[ "$MODE" == "status" ]]; then
  echo "Mutation batch progress: $done_count/$total files covered."
  if [[ "${#remaining[@]}" -gt 0 ]]; then
    echo "Next up: ${remaining[0]}"
  fi
  exit 0
fi

if [[ "$MODE" == "dry-run" ]]; then
  if [[ "${#remaining[@]}" -eq 0 ]]; then
    echo "All $total files have been covered."
    exit 0
  fi
  next_batch
  echo "Would run batch ($((done_count + 1))-$((done_count + ${#batch[@]})) of $total):"
  printf '  %s\n' "${batch[@]}"
  if [[ "${#batch_specs[@]}" -gt 0 ]]; then
    echo "against:"
    printf '  %s\n' "${batch_specs[@]}"
  else
    echo "against the whole suite (no spec imports it)"
  fi
  exit 0
fi

deadline=""
[[ -n "$MAX_MINUTES" ]] && deadline=$(( $(date +%s) + MAX_MINUTES * 60 ))

while true; do
  if [[ "${#remaining[@]}" -eq 0 ]]; then
    echo "All $total files have been covered."
    echo "reports/stryker-incremental.json now holds a full baseline — commit it to reuse across the team/CI."
    exit 0
  fi

  next_batch
  batch_joined=$(IFS=,; echo "${batch[*]}")

  echo "Running batch $((done_count + 1))-$((done_count + ${#batch[@]})) of $total (concurrency=$CONCURRENCY):"
  printf '  %s\n' "${batch[@]}"
  if [[ "${#batch_specs[@]}" -gt 0 ]]; then
    echo "against ${#batch_specs[@]} spec(s): ${batch_specs[*]}"
    MUTATION_SPECS="$(printf -- '--include %s ' "${batch_specs[@]}")"
  else
    echo "against the whole suite (no spec imports it)"
    MUTATION_SPECS=""
  fi
  export MUTATION_SPECS

  forget_results "${batch[@]}"

  if [[ -n "$deadline" ]]; then
    left=$(( deadline - $(date +%s) ))
    status=0
    timeout --kill-after=60 "$left" \
      npx stryker run --mutate "$batch_joined" --incremental --concurrency "$CONCURRENCY" || status=$?
    if [[ "$status" -eq 124 || "$status" -eq 137 ]]; then
      echo "Time budget of $MAX_MINUTES minutes used up mid-batch; it will be re-run next time."
      echo "Stopped at $done_count/$total files covered."
      exit 0
    fi
    [[ "$status" -eq 0 ]] || exit "$status"
  else
    npx stryker run --mutate "$batch_joined" --incremental --concurrency "$CONCURRENCY"
  fi

  record_progress "${batch[@]}"
  load_progress
  compute_remaining
  echo "Done: $done_count/$total files covered."

  if [[ -z "$deadline" ]]; then
    echo "Re-run this script to continue."
    exit 0
  fi
  if [[ "$(date +%s)" -ge "$deadline" ]]; then
    echo "Time budget of $MAX_MINUTES minutes used up; re-run to continue."
    exit 0
  fi
done
