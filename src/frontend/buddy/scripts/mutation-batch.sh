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
# reports/mutation-fingerprints.json (committed, like the report) holds the
# fingerprint of each file and its specs as of its recorded results. A file
# whose results have no survivors and whose fingerprint still matches is
# counted as covered without running Stryker: a re-run can't change anything.
#
# Usage:
#   scripts/mutation-batch.sh [--batch-size N] [--concurrency N] [--max-minutes N]
#                             [--rerun-changed] [--baseline FILE]
#                             [--status] [--reset] [--dry-run]
#   scripts/mutation-batch.sh --record FILE...
#   scripts/mutation-batch.sh --stale
#
#   --max-minutes N   Keep running batches until N minutes have passed. A batch
#                     still running at the deadline is stopped and not recorded,
#                     so it's picked up again next time. CI uses this to stay
#                     inside the job timeout and keep what it finished.
#   --rerun-changed   Also re-run files whose source or specs changed since
#                     they were last covered (CI's nightly mode). Without it a
#                     covered file stays covered until --reset.
#   --baseline FILE   A report whose results match reports/mutation-fingerprints.json
#                     (CI passes the committed report, saved before its cache
#                     restore replaces it). A file whose recorded fingerprint
#                     there is its current one, but whose progress line has
#                     another or none, takes its results from FILE and counts as
#                     covered, so a fix committed with /mutation-fix isn't
#                     shadowed by older cached results.
#   --record FILE...  Records the files as covered at their current fingerprint,
#                     after re-running Stryker on them yourself (e.g. with
#                     --force once their specs are fixed).
#   --stale           Prints the files whose results in the report are out of
#                     date: recorded at a fingerprint (the progress line's, else
#                     the committed one) other than their current one. Files
#                     with no recorded fingerprint count as up to date.
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
FINGERPRINT_FILE="reports/mutation-fingerprints.json"
BASELINE_FILE=""
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
    --baseline)
      BASELINE_FILE="$2"
      shift 2
      ;;
    --status)
      MODE="status"
      shift
      ;;
    --stale)
      MODE="stale"
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
    --record)
      MODE="record"
      shift
      record_files=("$@")
      break
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
  local pairs=()
  for f in "$@"; do
    pairs+=("$f=$(fingerprint "$f")")
    echo "${pairs[-1]/=/ }" >> "$tmp"
  done
  mv "$tmp" "$PROGRESS_FILE"
  record_fingerprints "${pairs[@]}"
}

# Stores `path=fingerprint` pairs in the committed fingerprint file, dropping
# entries for files that no longer exist.
record_fingerprints() {
  node -e '
    const fs = require("node:fs");
    const [path, ...pairs] = process.argv.slice(1);
    const old = fs.existsSync(path) ? JSON.parse(fs.readFileSync(path, "utf8")) : {};
    for (const pair of pairs) {
      const i = pair.lastIndexOf("=");
      old[pair.slice(0, i)] = pair.slice(i + 1);
    }
    const kept = Object.keys(old).filter((f) => fs.existsSync(f)).sort();
    fs.writeFileSync(path, JSON.stringify(Object.fromEntries(kept.map((f) => [f, old[f]])), null, 2) + "\n");
  ' "$FINGERPRINT_FILE" "$@"
}

# Prints the files among `remaining` that the report already shows with no
# surviving or uncovered mutants, at the fingerprint they still have.
clean_and_unchanged() {
  [[ -f "$INCREMENTAL_FILE" && -f "$FINGERPRINT_FILE" && "${#remaining[@]}" -gt 0 ]] || return 0
  local f pairs=()
  for f in "${remaining[@]}"; do
    pairs+=("$f=$(fingerprint "$f")")
  done
  node -e '
    const fs = require("node:fs");
    const [reportPath, fingerprintPath, ...pairs] = process.argv.slice(1);
    const report = JSON.parse(fs.readFileSync(reportPath, "utf8"));
    const recorded = JSON.parse(fs.readFileSync(fingerprintPath, "utf8"));
    for (const pair of pairs) {
      const i = pair.lastIndexOf("=");
      const [file, hash] = [pair.slice(0, i), pair.slice(i + 1)];
      const mutants = report.files[file]?.mutants;
      if (recorded[file] === hash && mutants !== undefined &&
          !mutants.some((m) => m.status === "Survived" || m.status === "NoCoverage"))
        console.log(file);
    }
  ' "$INCREMENTAL_FILE" "$FINGERPRINT_FILE" "${pairs[@]}"
}

# Takes results from $BASELINE_FILE for the files it holds at their current
# fingerprint (per $FINGERPRINT_FILE) while the progress file has another
# fingerprint or none for them, and records those files as covered. Prints
# the files it took (in a subshell; the caller reloads the progress).
adopt_baseline() {
  [[ -f "$BASELINE_FILE" && -f "$FINGERPRINT_FILE" ]] || return 0
  local f current pairs=()
  for f in "${all_files[@]}"; do
    current="$(fingerprint "$f")"
    [[ "${done_hash[$f]:-}" == "$current" ]] || pairs+=("$f=$current")
  done
  [[ "${#pairs[@]}" -gt 0 ]] || return 0
  local adopted=()
  mapfile -t adopted < <(node -e '
    const fs = require("node:fs");
    const [baselinePath, reportPath, fingerprintPath, ...pairs] = process.argv.slice(1);
    const baseline = JSON.parse(fs.readFileSync(baselinePath, "utf8"));
    const recorded = JSON.parse(fs.readFileSync(fingerprintPath, "utf8"));
    const report = fs.existsSync(reportPath)
      ? JSON.parse(fs.readFileSync(reportPath, "utf8"))
      : { ...baseline, files: {} };
    const adopted = [];
    for (const pair of pairs) {
      const i = pair.lastIndexOf("=");
      const [file, hash] = [pair.slice(0, i), pair.slice(i + 1)];
      if (recorded[file] === hash && baseline.files[file] !== undefined) {
        report.files[file] = baseline.files[file];
        adopted.push(file);
      }
    }
    if (adopted.length > 0) fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
    for (const file of adopted) console.log(file);
  ' "$BASELINE_FILE" "$INCREMENTAL_FILE" "$FINGERPRINT_FILE" "${pairs[@]}")
  [[ "${#adopted[@]}" -gt 0 ]] || return 0
  record_progress "${adopted[@]}"
  printf '%s\n' "${adopted[@]}"
}

# Prints the files in the report whose results were recorded at a fingerprint
# other than their current one: the progress line's, or else the one in
# $FINGERPRINT_FILE. A file with neither counts as up to date.
stale_files() {
  [[ -f "$INCREMENTAL_FILE" ]] || return 0
  local f pairs=()
  for f in "${all_files[@]}"; do
    pairs+=("$f=$(fingerprint "$f")=${done_hash[$f]:--}")
  done
  node -e '
    const fs = require("node:fs");
    const [reportPath, fingerprintPath, ...pairs] = process.argv.slice(1);
    const report = JSON.parse(fs.readFileSync(reportPath, "utf8"));
    const committed = fs.existsSync(fingerprintPath)
      ? JSON.parse(fs.readFileSync(fingerprintPath, "utf8"))
      : {};
    for (const pair of pairs) {
      const [progress, current, ...rest] = pair.split("=").reverse();
      const file = rest.reverse().join("=");
      if (report.files[file] === undefined) continue;
      const recorded = progress !== "-" ? progress : committed[file];
      if (recorded !== undefined && recorded !== current) console.log(file);
    }
  ' "$INCREMENTAL_FILE" "$FINGERPRINT_FILE" "${pairs[@]}"
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

if [[ "$MODE" == "stale" ]]; then
  stale_files
  exit 0
fi

if [[ "$MODE" == "run" && -n "$BASELINE_FILE" ]]; then
  # Run in a subshell, so reload the progress it recorded.
  mapfile -t adopted < <(adopt_baseline)
  load_progress
  if [[ "${#adopted[@]}" -gt 0 ]]; then
    echo "Took ${#adopted[@]} file(s) from $BASELINE_FILE, whose results there are current:"
    printf '  %s\n' "${adopted[@]}"
  fi
fi

compute_remaining

if [[ "$MODE" == "status" ]]; then
  echo "Mutation batch progress: $done_count/$total files covered."
  if [[ "${#remaining[@]}" -gt 0 ]]; then
    echo "Next up: ${remaining[0]}"
  fi
  exit 0
fi

if [[ "$MODE" == "record" ]]; then
  [[ "${#record_files[@]}" -gt 0 ]] || { echo "--record needs at least one file" >&2; exit 1; }
  for f in "${record_files[@]}"; do
    [[ -f "$f" ]] || { echo "No such file: $f" >&2; exit 1; }
  done
  record_progress "${record_files[@]}"
  echo "Recorded ${#record_files[@]} file(s) at their current fingerprint."
  exit 0
fi

mapfile -t skipped < <(clean_and_unchanged)
if [[ "${#skipped[@]}" -gt 0 ]]; then
  if [[ "$MODE" == "dry-run" ]]; then
    echo "Would skip ${#skipped[@]} file(s) already clean and unchanged:"
  else
    echo "Skipping ${#skipped[@]} file(s) already clean and unchanged:"
    record_progress "${skipped[@]}"
  fi
  printf '  %s\n' "${skipped[@]}"
  # Leave them out of the batches below, also in a dry run.
  for f in "${skipped[@]}"; do done_hash["$f"]="$(fingerprint "$f")"; done
  compute_remaining
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

# Keeps the report small enough to commit and cache, however the run ends.
trap 'node scripts/mutation-slim-report.mjs "$INCREMENTAL_FILE"' EXIT

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
    # A stopped Stryker still writes the mutants it got through into the
    # report, so keep a copy to put back: a half-run batch isn't recorded and
    # would otherwise show up as findings. Stryker is run directly, not via
    # npx, so timeout waits for it to finish writing before the EXIT trap
    # slims the report; npx exits at once and Stryker would overwrite it.
    [[ -f "$INCREMENTAL_FILE" ]] && cp "$INCREMENTAL_FILE" "$INCREMENTAL_FILE.pre-batch"
    timeout --kill-after=60 "$left" \
      node_modules/.bin/stryker run --mutate "$batch_joined" --incremental --concurrency "$CONCURRENCY" || status=$?
    if [[ "$status" -eq 124 || "$status" -eq 137 ]]; then
      if [[ -f "$INCREMENTAL_FILE.pre-batch" ]]; then
        mv "$INCREMENTAL_FILE.pre-batch" "$INCREMENTAL_FILE"
      else
        rm -f "$INCREMENTAL_FILE"
      fi
      echo "Time budget of $MAX_MINUTES minutes used up mid-batch; it will be re-run next time."
      echo "Stopped at $done_count/$total files covered."
      exit 0
    fi
    rm -f "$INCREMENTAL_FILE.pre-batch"
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
