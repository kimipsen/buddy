#!/usr/bin/env bash
# Runs frontend mutation testing in small batches so it doesn't overwhelm the
# devcontainer (each mutant re-runs the whole `npm test`, and Stryker's
# default concurrency spawns several of those in parallel at once).
#
# Progress is tracked across invocations, so you can just keep re-running
# this script (e.g. `task test:mutation:frontend:batch`) and it will work
# through every mutable file a few at a time. Each batch is run with
# `--incremental`, so results accumulate into reports/stryker-incremental.json
# as you go. Once every file has been covered, that file is a full baseline
# you can commit and reuse for incremental/diff-based runs.
#
# Usage:
#   scripts/mutation-batch.sh [--batch-size N] [--concurrency N] [--status] [--reset] [--dry-run]
#
# Env vars (overridden by flags): BATCH_SIZE, CONCURRENCY
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

BATCH_SIZE="${BATCH_SIZE:-5}"
CONCURRENCY="${CONCURRENCY:-1}"
PROGRESS_FILE=".mutation-batch-progress"
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

if [[ "$MODE" == "reset" ]]; then
  rm -f "$PROGRESS_FILE"
  echo "Progress reset. Next run starts from the beginning."
  exit 0
fi

# Same file set Stryker itself mutates (see stryker.conf.json's "mutate" glob).
mapfile -t all_files < <(find src/app -type f -name '*.ts' ! -name '*.spec.ts' | sort)
total="${#all_files[@]}"

touch "$PROGRESS_FILE"
mapfile -t done_files < "$PROGRESS_FILE"

declare -A done_set=()
for f in "${done_files[@]}"; do
  [[ -n "$f" ]] && done_set["$f"]=1
done

remaining=()
for f in "${all_files[@]}"; do
  [[ -z "${done_set[$f]:-}" ]] && remaining+=("$f")
done

done_count=$(( total - ${#remaining[@]} ))

if [[ "$MODE" == "status" ]]; then
  echo "Mutation batch progress: $done_count/$total files covered."
  if [[ "${#remaining[@]}" -gt 0 ]]; then
    echo "Next up: ${remaining[0]}"
  fi
  exit 0
fi

if [[ "${#remaining[@]}" -eq 0 ]]; then
  echo "All $total files have been covered."
  echo "reports/stryker-incremental.json now holds a full baseline — commit it to reuse across the team/CI."
  exit 0
fi

batch=("${remaining[@]:0:$BATCH_SIZE}")
batch_joined=$(IFS=,; echo "${batch[*]}")

if [[ "$MODE" == "dry-run" ]]; then
  echo "Would run batch ($((done_count + 1))-$((done_count + ${#batch[@]})) of $total):"
  printf '  %s\n' "${batch[@]}"
  exit 0
fi

echo "Running batch $((done_count + 1))-$((done_count + ${#batch[@]})) of $total (concurrency=$CONCURRENCY):"
printf '  %s\n' "${batch[@]}"

npx stryker run --mutate "$batch_joined" --incremental --concurrency "$CONCURRENCY"

printf '%s\n' "${batch[@]}" >> "$PROGRESS_FILE"
new_done=$(( done_count + ${#batch[@]} ))
echo "Done: $new_done/$total files covered. Re-run this script to continue."
