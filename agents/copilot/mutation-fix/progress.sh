#!/usr/bin/env bash
# Prints one status line for a running Stryker run every INTERVAL seconds, for a Monitor to relay.
# Shared by the mutation-fix (StrykerJS) and mutation-fix-backend (Stryker.NET) skills.
#
# Usage: progress.sh <log> [interval-seconds, default 300] [stall-seconds, default 600]
#
# The run must write its exit code to <log>.exit when it ends, e.g.
#   some-stryker-command > run.log 2>&1; echo $? > run.log.exit
# The script exits after printing a final line once that file exists.
set -uo pipefail

log="$1"
interval="${2:-300}"
stall="${3:-600}"
exit_file="$log.exit"
started=$(date +%s)
# Elapsed time counts from the log's creation, so it stays right when the Monitor is re-armed.
[[ -f "$log" ]] && born=$(stat -c %W "$log" 2>/dev/null) && (( born > 0 )) && started=$born

minutes() { echo "$(( $1 / 60 ))m"; }

# Last progress line in the log. Both Stryker flavours redraw progress with \r and ANSI codes.
progress() {
  local text
  text=$(tr '\r' '\n' < "$log" 2>/dev/null | sed 's/\x1b\[[0-9;?]*[A-Za-z]//g')
  # StrykerJS: "Mutation testing 71% (elapsed: ~1h 3m, remaining: ~25m) 320/450 tested (138 survived, 0 timed out)"
  # Stryker.NET: any line with "<n> / <total>" next to "mutant"/"tested", else the last non-blank line.
  { grep -E 'Mutation testing [0-9]+%' <<<"$text" ||
    grep -Ei '(mutant|tested).*[0-9]+ ?/ ?[0-9]+|[0-9]+ ?/ ?[0-9]+.*(mutant|tested)' <<<"$text" ||
    grep -Ev '^[[:space:]]*$' <<<"$text"; } | tail -1 | sed 's/^[[:space:]]*//' | cut -c1-180
}

# The last "Running batch X-Y of Z" line, if this is a frontend batch run.
batch() { grep -Eo 'Running batch [0-9]+-[0-9]+ of [0-9]+' "$log" 2>/dev/null | tail -1; }

report() {
  local now line b age=""
  now=$(date +%s)
  if [[ ! -f "$log" ]]; then
    echo "[$(date +%H:%M)] waiting $(minutes $((now - started))): no log yet at $log"
    return
  fi
  line=$(progress)
  b=$(batch)
  local idle=$(( now - $(stat -c %Y "$log") ))
  (( idle >= stall )) && age=" | WARNING: no log output for $(minutes "$idle"), may be hung"
  echo "[$(date +%H:%M)] running $(minutes $((now - started)))${b:+ | $b} | ${line:-no progress yet}$age"
}

while true; do
  # Poll every few seconds so the final line arrives promptly, but report only every $interval.
  for (( waited = 0; waited < interval; waited += 5 )); do
    if [[ -f "$exit_file" ]]; then
      line=$(progress)
      echo "[$(date +%H:%M)] finished with exit $(cat "$exit_file") after $(minutes $(( $(date +%s) - started ))) | ${line:-no progress line}"
      exit 0
    fi
    sleep 5
  done
  report
done
