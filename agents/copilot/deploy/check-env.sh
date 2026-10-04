#!/usr/bin/env bash
# Compares a deploy .env against its .env.example by KEY NAME, and flags
# values that are still the .env.example placeholder.
# Never prints values -- only key names and a status per key.
#
# Usage: check-env.sh <dir containing .env and .env.example>
#   e.g. check-env.sh deploy        (Oracle VM / docker compose)
#        check-env.sh deploy/azure  (Azure Container Apps)
#
# Status per key:
#   ok           set, and not the placeholder
#   EMPTY        present but blank
#   MISSING      not in .env
#   PLACEHOLDER  identical to the .env.example value, where that value is a
#                placeholder (contains "change", "yourdomain" or "your-"),
#                e.g. KEYCLOAK_ADMIN_CLI_SECRET=change-me. Values are compared
#                in memory only.
#   EXTRA        in .env but not in .env.example
# A key whose value is blank in .env.example is optional (the Azure example
# leaves GMAIL_SMTP_* and *_CUSTOM_DOMAIN blank on purpose).
#
# Exit code: 0 = every required key is set and none is a placeholder, except
# KEYCLOAK_ADMIN_CLI_SECRET (a placeholder there is legitimate on a first boot,
# so it is reported but doesn't fail), 1 = something required is missing,
# empty or a placeholder, 2 = usage error / file missing.
set -euo pipefail

dir=${1:?usage: check-env.sh <dir>}
example="$dir/.env.example"
env="$dir/.env"
[[ -f "$example" ]] || { echo "missing: $example" >&2; exit 2; }
[[ -f "$env" ]] || { echo "missing: $env (copy $example and fill it in)" >&2; exit 2; }

# Fills the named associative array with key -> normalised value (inline
# comments, whitespace and quotes stripped). Values stay in this process.
parse() {
  local -n out=$2
  local key value
  while read -r key value; do
    value=${value%%#*}; value=${value//[[:space:]\"\']/}
    out[$key]=$value
  done < <(sed -E -n 's/^[[:space:]]*(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*=[[:space:]]*(.*)$/\2 \3/p' "$1")
}

is_placeholder() { [[ ${1,,} == *change* || ${1,,} == *yourdomain* || ${1,,} == *your-* ]]; }

declare -A ex=() cur=()
parse "$example" ex
parse "$env" cur

status=0
for k in "${!ex[@]}"; do
  required=$([[ -n ${ex[$k]} ]] && echo required || echo optional)
  if [[ -z "${cur[$k]+x}" ]]; then
    state=MISSING
  elif [[ -z ${cur[$k]} ]]; then
    state=EMPTY
  elif [[ ${cur[$k]} == "${ex[$k]}" ]] && is_placeholder "${ex[$k]}"; then
    state=PLACEHOLDER
  else
    state=ok
  fi
  printf '%-12s (%s) %s\n' "$state" "$required" "$k"
done | sort -k3
for k in "${!ex[@]}"; do
  [[ -n ${ex[$k]} ]] || continue
  if [[ -z "${cur[$k]+x}" || -z ${cur[$k]} ]]; then status=1; continue; fi
  if [[ ${cur[$k]} == "${ex[$k]}" ]] && is_placeholder "${ex[$k]}" && [[ $k != KEYCLOAK_ADMIN_CLI_SECRET ]]; then
    status=1
  fi
done
for k in "${!cur[@]}"; do
  if [[ -z "${ex[$k]+x}" ]]; then printf '%-12s (not in .env.example) %s\n' EXTRA "$k"; fi
done | sort
if [[ -n "${ex[KEYCLOAK_ADMIN_CLI_SECRET]+x}" && "${cur[KEYCLOAK_ADMIN_CLI_SECRET]:-}" == "${ex[KEYCLOAK_ADMIN_CLI_SECRET]}" ]]; then
  echo "note: KEYCLOAK_ADMIN_CLI_SECRET is still the first-boot placeholder; the API's Keycloak admin calls fail until the real buddy-admin-cli secret is set."
fi
exit $status
