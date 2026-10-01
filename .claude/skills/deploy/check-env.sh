#!/usr/bin/env bash
# Compares the KEY NAMES of a deploy .env against its .env.example.
# Never prints values -- only key names and whether each is set.
#
# Usage: check-env.sh <dir containing .env and .env.example>
#   e.g. check-env.sh deploy        (Oracle VM / docker compose)
#        check-env.sh deploy/azure  (Azure Container Apps)
#
# A key whose value is blank in .env.example is treated as optional (the
# Azure example leaves GMAIL_SMTP_* and *_CUSTOM_DOMAIN blank on purpose).
# Exit code: 0 = every required key present and non-empty, 1 = something
# required is missing/empty, 2 = usage error / file missing.
set -euo pipefail

dir=${1:?usage: check-env.sh <dir>}
example="$dir/.env.example"
env="$dir/.env"
[[ -f "$example" ]] || { echo "missing: $example" >&2; exit 2; }
[[ -f "$env" ]] || { echo "missing: $env (copy $example and fill it in)" >&2; exit 2; }

# key -> "set" | "empty" (value itself is discarded)
parse() {
  sed -E -n 's/^[[:space:]]*(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*=[[:space:]]*(.*)$/\2 \3/p' "$1" |
    while read -r key value; do
      value=${value%%#*}; value=${value//[[:space:]\"\']/}
      if [[ -n "$value" ]]; then echo "$key set"; else echo "$key empty"; fi
    done
}

declare -A ex cur
while read -r k s; do ex[$k]=$s; done < <(parse "$example")
while read -r k s; do cur[$k]=$s; done < <(parse "$env")

for k in "${!ex[@]}"; do
  required=$([[ ${ex[$k]} == set ]] && echo required || echo optional)
  if [[ -z "${cur[$k]+x}" ]]; then
    echo "MISSING  ($required) $k"
  elif [[ ${cur[$k]} == empty ]]; then
    echo "EMPTY    ($required) $k"
  else
    echo "ok       ($required) $k"
  fi
done | sort -k3
for k in "${!cur[@]}"; do
  if [[ -z "${ex[$k]+x}" ]]; then echo "EXTRA    (not in .env.example) $k"; fi
done | sort
status=0
for k in "${!ex[@]}"; do
  [[ ${ex[$k]} == set ]] || continue
  [[ -n "${cur[$k]+x}" && ${cur[$k]} == set ]] || status=1
done
exit $status
