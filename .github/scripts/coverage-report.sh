#!/usr/bin/env bash
# Usage: coverage-report.sh <name> <cobertura.xml>
#
# Writes the coverage totals of a Cobertura report to the job summary and, on a pull request,
# keeps one comment per <name> up to date (found again by a hidden marker), so each push updates
# it instead of adding a new one. Needs GH_TOKEN with pull-requests: write and PR_NUMBER on a PR.
# Report only: no threshold fails the build.
set -euo pipefail

name="$1"
report="$2"
marker="<!-- coverage:${name} -->"

# The totals are attributes of the root <coverage> element, which both coverlet and Vitest write.
root_element="$(tr '\n' ' ' < "$report" | grep -oE '<coverage [^>]*>' | head -n 1)"
attribute() { grep -oE " $1=\"[^\"]*\"" <<<"$root_element" | cut -d'"' -f2; }
cell() { awk -v rate="$(attribute "$1")" -v covered="$(attribute "$2")" -v valid="$(attribute "$3")" \
  'BEGIN { printf "%.1f%% (%s/%s)", rate * 100, covered, valid }'; }

run_url="${GITHUB_SERVER_URL:-}/${GITHUB_REPOSITORY:-}/actions/runs/${GITHUB_RUN_ID:-}"
body="${marker}
### ${name} coverage

| Lines | Branches |
| --- | --- |
| $(cell line-rate lines-covered lines-valid) | $(cell branch-rate branches-covered branches-valid) |

Full report: the coverage artifact of [this run](${run_url})."

echo "$body" >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"

if [[ "${GITHUB_EVENT_NAME:-}" != "pull_request" || -z "${PR_NUMBER:-}" ]]; then
  exit 0
fi

comments="repos/${GITHUB_REPOSITORY}/issues/${PR_NUMBER}/comments"
existing="$(gh api "$comments" --paginate \
  --jq ".[] | select(.user.login == \"github-actions[bot]\" and (.body | startswith(\"${marker}\"))) | .id" | head -n 1)"

if [[ -n "$existing" ]]; then
  gh api --method PATCH "repos/${GITHUB_REPOSITORY}/issues/comments/${existing}" -f body="$body" >/dev/null
else
  gh api --method POST "$comments" -f body="$body" >/dev/null
fi
